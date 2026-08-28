using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DICS.Generator
{
    [Generator]
    public class LiftFactoryGenerator : IIncrementalGenerator
    {
        public static readonly string LiftFactory = "global::DICS.Attribute.LiftFactory";
        public static readonly string LocalAttr = "global::DICS.Attribute.Local";


        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var explicitly = context.FindTaggedTypes(LiftFactory);
            var inferred = context.FindTypesWithMemberAttribute(LocalAttr);

            var combined = explicitly.Collect()
                .Combine(inferred.Collect())
                .SelectMany((pair, _) =>
                {
                    var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                    var result = new List<ISymbol>();
                    foreach (var s in pair.Left)
                        if (s != null && seen.Add(s)) result.Add(s);
                    foreach (var s in pair.Right)
                    {
                        if (s is not INamedTypeSymbol nts) continue;
                        if (nts.HasAnyClassLevelLift()) continue;
                        if (seen.Add(nts)) result.Add(nts);
                    }
                    return result;
                });

            context.RegisterSourceOutput(combined, GenerateCode!);
        }


        private static void GenerateCode(SourceProductionContext spc, ISymbol classSymbol)
        {
            var className = classSymbol.Name;
            var nts = (INamedTypeSymbol)classSymbol;

            var reprs = ExtractReprs(spc, nts);

            var args = nts.RenderTypeArgs();

            var globalParams = reprs.Where(p => !p.IsLocal).ToList();
            FieldRulesChecker.CheckConflicts(spc, nts, globalParams.ToList<IGenericKeyed>());

            var locParams = reprs.Where(p => p.IsLocal).ToList();
            var locSig = locParams.Select(p => $"{p.Fqn} {p.ParamName}").Join(", ");
            var fullLocSig = new List<string> { "Func<ILocator, ILocator> _transformLocator", locSig }
                .Where(s => s.Any()).Join(", ");


            var names = globalParams.Select(p => p.KeyName).Join(",\n");
            var keys = globalParams.Select(f => f.Tpe.FqnNotNull())
                .Select((s, i) => $"var k{i} = IFunctoid.KeyN<{s}>(names, {i});")
                .Join("\n");
            var sigs = globalParams.Select((s, i) => $"k{i}").Join(",\n");

            var initParents = nts.InitializableParents(spc);
            var sups = initParents
                .Select(p => $"(({p.Fqn()})_instance).Initialize(_locator, {p.Fqn()}.SignatureStatic );")
                .Join("\n");

            var fqn = classSymbol.Fqn();

            var isInitializer = IsInitializer(nts);

            string body;
            if (isInitializer)
            {
                var gidx = 0;
                var argslist = reprs.Select(p =>
                {
                    var ret = p.IsLocal
                        ? $"_instance.{p.ParamName} = {p.ParamName};"
                        : $"_instance.{p.ParamName} = IFunctoid.Get<{p.Tpe.FqnNotNull()}>(_locator, FactoryFunctoid.SignatureStatic.Args[{gidx}]);";
                    if (!p.IsLocal)
                        gidx++;
                    return ret;
                }).Join("\n");

                body = $@"var _instance = ({fqn}) this._extractor!.Invoke(_locator);
{sups}
{argslist}
return _instance;";
            }
            else
            {
                var gidx = 0;
                var argslist = reprs.Select(p =>
                {
                    var ret = p.IsLocal
                        ? p.ParamName
                        : $"IFunctoid.Get<{p.Tpe.FqnNotNull()}>(_locator, FactoryFunctoid.SignatureStatic.Args[{gidx}])";
                    if (!p.IsLocal)
                        gidx++;
                    return ret;
                }).Join(",\n");
                body = $@"return new {fqn}(
{argslist.Shift(4)}
);";
            }


            var defn = $@"{nts.DeclaredAccessibility.ToCSharpKeyword()}{nts.ToExtraTypeModifiers()} partial {nts.Keyword()} {className}{args}
{{
    public static FactoryFunctoid LiftFactoryFunctoid()
    {{
        return new FactoryFunctoid();
    }}

    public class FactoryFunctoid : IGeneratedFactoryFunctoid<{className}{args}> 
    {{
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Sig MakeSignature() 
        {{
            return SignatureStatic;
        }}
    
        public new static readonly Sig SignatureStatic = MakeSignatureStatic(); 
    
        private static Sig MakeSignatureStatic() 
        {{
            string?[] names = new string?[] {{ 
                {names.Shift(16).Trim()} 
            }};
    
            {keys.Shift(12).Trim()}
            return Sig.Of(
                    {sigs.Shift(20).Trim()}
                )
                ;
        }}


        public IAbstractGeneratedFactory Make(ILocator args, IFunctoid? extractor)
        {{
            return new Factory(args, extractor);
        }}
    }}

    public class Factory : IGeneratedFactory<{className}{args}> {{
        private ILocator _locator;
        private IFunctoid? _extractor;

        public Factory(ILocator locator, IFunctoid? extractor)
        {{
            _locator = locator;
            _extractor = extractor;
        }}

        public {fqn} Create({fullLocSig})
        {{
            var _locator = _transformLocator(this._locator);
            {body.Shift(12).Trim()} 
        }}

        public {fqn} Create({locSig})
        {{
            var _locator = this._locator;
            {body.Shift(12).Trim()} 
        }}
    }}
}}";

            var source = classSymbol.WrapIntoNamespace(defn);

            spc.AddSource(classSymbol.UniqueFilename("Factory"), source);
        }

        private static List<FactoryParamRepr> ExtractReprs(SourceProductionContext spc, INamedTypeSymbol nts)
        {
            var isInitializer = IsInitializer(nts);
            if (isInitializer)
            {
                var fieldSymbols = nts.FieldSymbols(spc);
                var ret = fieldSymbols.Select(p =>
                {
                    var na = p.Symbol.GetAttributeByName(LiftConstructorGenerator.IdAttr);
                    var nme = na != null ? $"\"{na.ConstructorArguments.First().Value}\"" : "null";
                    var isLocal = p.Symbol.GetAttributeByName(LocalAttr) != null;
                    return new FactoryParamRepr(p.Tpe, p.Tpe.Fqn(), nme, p.Name, isLocal);
                }).ToList();

                return ret;
            }
            else
            {
                var bestCtor = nts.BestConstructor();
                var ret = bestCtor.Parameters.Select(p =>
                {
                    var na = p.GetAttributeByName(LiftConstructorGenerator.IdAttr);
                    var nme = na != null ? $"\"{na.ConstructorArguments.First().Value}\"" : "null";
                    var isLocal = p.GetAttributeByName(LocalAttr) != null;
                    return new FactoryParamRepr(p.Type, p.Type.Fqn(), nme, p.Name, isLocal);
                }).ToList();

                return ret;
            }
        }

        private static bool IsInitializer(INamedTypeSymbol nts)
        {
            // Explicit FactoryKind argument wins when the class-level attribute is present.
            var ann = nts.GetAttributeByName(LiftFactory);
            if (ann != null && ann.ConstructorArguments.Length >= 1)
            {
                var explicitKind = ann.ConstructorArguments[0].Value;
                if (explicitKind != null) return (int)explicitKind == 1;
            }
            // No attribute (inference path) or attribute with no kind: auto-detect.
            return HasAnyInjectMember(nts);
        }

        private static bool HasAnyInjectMember(INamedTypeSymbol nts)
        {
            foreach (var member in nts.GetMembers())
                if (member is IFieldSymbol or IPropertySymbol)
                    if (member.GetAttributeByName(LiftInitializerGenerator.InjectAttr) != null)
                        return true;
            return false;
        }

        private record FactoryParamRepr(ITypeSymbol Tpe, string Fqn, string KeyName, string ParamName, bool IsLocal)
            : IGenericKeyed
        {
            public string Name()
            {
                return ParamName;
            }

            public ITypeSymbol KeyTpe()
            {
                return Tpe;
            }

            string IGenericKeyed.KeyName()
            {
                return KeyName;
            }
        }
    }
}