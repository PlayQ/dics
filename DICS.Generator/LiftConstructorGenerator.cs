using System.Linq;
using Microsoft.CodeAnalysis;

namespace DICS.Generator
{
    public record ParamRepr(ITypeSymbol Tpe, string ParamName, string KeyName, int KeyIndex) : IGenericKeyed
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

    [Generator]
    public class LiftConstructorGenerator : IIncrementalGenerator
    {
        public static readonly string LiftConstructorAttr = "global::DICS.Attribute.LiftConstructor";
        public static readonly string IdAttr = "global::DICS.Attribute.Id";
        private const string AsyncFactoryMethodName = "CreateAsync";
        private const string CancellationTokenFqn = "global::System.Threading.CancellationToken";
        private const string TasksNamespaceFqn = "global::System.Threading.Tasks";


        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classDeclarations = context.FindTaggedTypes(LiftConstructorAttr);

            context.RegisterSourceOutput(classDeclarations, GenerateCode!);
        }


        private static void GenerateCode(SourceProductionContext spc, ISymbol classSymbol)
        {
            var className = classSymbol.Name;
            var nts = (INamedTypeSymbol)classSymbol;
            if (classSymbol.GetAttributeByName(LiftInitializerGenerator.LiftInitAttr) != null)
                spc.Fail(new IssueDescriptor(nts));

            var bestCtor = nts.BestConstructor();

            var fields = bestCtor.Parameters.Select((p, i) =>

            {
                var idattr = p.GetAttributeByName(IdAttr);
                var nme = idattr != null ? $"\"{idattr.ConstructorArguments.First().Value}\"" : "null";

                return new ParamRepr(p.Type, p.Name, nme, i);
            }).ToList();

            FieldRulesChecker.CheckConflicts(spc, nts, fields.ToList<IGenericKeyed>());

            var args = nts.RenderTypeArgs();

            var keys = fields
                .Select(f => $"var k{f.KeyIndex} = IFunctoid.KeyN<{f.Tpe.FqnNotNull()}>(names, {f.KeyIndex});")
                .Join("\n");

            var paramss = fields.Select(f => $"IFunctoid.Get<{f.Tpe.FqnNotNull()}>(loc, k{f.KeyIndex})").Join(",\n");

            var sigs = fields.Select(f => $"k{f.KeyIndex}").Join(",\n");

            var fqn = classSymbol.Fqn();

            var names = fields.Select(f => f.KeyName).Join(",\n");

            var syncBlock = $@"[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new Sig MakeSignature()
    {{
        return SignatureStatic;
    }}

    public new static readonly Sig SignatureStatic = MakeSignatureStatic();

    private static Sig MakeSignatureStatic()
    {{
        string?[] names = new string?[] {{
            {names.Shift(12).Trim()}
        }};

        {keys.Shift(8).Trim()}
        return Sig.Of(
                {sigs.Shift(16).Trim()}
            );
    }}

    public new static IFunctoid Lift() {{
        string?[] names = new string?[] {{
            {names.Shift(16).Trim()}
        }};

        {keys.Shift(8).Trim()}

        return new FunctoidFromLocator<{fqn}>(
            loc => new {fqn}(
                {paramss.Shift(16).Trim()}
            ),
            SignatureStatic
        );
    }}";

            var asyncBlock = TryBuildAsyncBlock(spc, nts, fqn);

            var defn = $@"{nts.DeclaredAccessibility.ToCSharpKeyword()}{nts.ToExtraTypeModifiers()} partial {nts.Keyword()} {className}{args}
{{
    {syncBlock.Shift(4).Trim()}
{asyncBlock}
}}";

            var source = classSymbol.WrapIntoNamespace(defn);
            spc.AddSource(classSymbol.UniqueFilename("Constructor"), source);
        }

        private static string TryBuildAsyncBlock(SourceProductionContext spc, INamedTypeSymbol nts, string fqn)
        {
            var factoryMethod = nts.GetMembers(AsyncFactoryMethodName)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsStatic);
            if (factoryMethod == null) return string.Empty;

            var parameters = factoryMethod.Parameters;
            if (parameters.Length == 0 ||
                parameters[parameters.Length - 1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                != CancellationTokenFqn)
            {
                var diag = Diagnostic.Create(
                    new DiagnosticDescriptor("INJ021", $"{AsyncFactoryMethodName} must end with CancellationToken",
                        $"Type '{{0}}'.{AsyncFactoryMethodName} must have System.Threading.CancellationToken as its last parameter to enable async lifting.",
                        "DICS", DiagnosticSeverity.Error, true),
                    factoryMethod.Locations.FirstOrDefault(), nts.Name);
                spc.ReportDiagnostic(diag);
                return string.Empty;
            }

            if (!ReturnsAwaitableOfSelf(factoryMethod, nts))
            {
                var diag = Diagnostic.Create(
                    new DiagnosticDescriptor("INJ025",
                        $"{AsyncFactoryMethodName} must return an awaitable of the declaring type",
                        $"Type '{{0}}'.{AsyncFactoryMethodName} returns '{{1}}'; it must return an awaitable whose result is '{{0}}' (for example System.Threading.Tasks.Task<{{0}}>) to enable async lifting.",
                        "DICS", DiagnosticSeverity.Error, true),
                    factoryMethod.Locations.FirstOrDefault(), nts.Name,
                    factoryMethod.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                spc.ReportDiagnostic(diag);
                return string.Empty;
            }

            var depParams = parameters.Take(parameters.Length - 1).ToList();

            var asyncFields = depParams.Select((p, i) =>
            {
                var idattr = p.GetAttributeByName(IdAttr);
                var nme = idattr != null ? $"\"{idattr.ConstructorArguments.First().Value}\"" : "null";
                return new ParamRepr(p.Type, p.Name, nme, i);
            }).ToList();

            var names = asyncFields.Select(f => f.KeyName).Join(",\n");
            var keys = asyncFields
                .Select(f => $"var k{f.KeyIndex} = IAsyncFunctoid.KeyN<{f.Tpe.FqnNotNull()}>(names, {f.KeyIndex});")
                .Join("\n");
            var paramss = asyncFields
                .Select(f => $"IAsyncFunctoid.Get<{f.Tpe.FqnNotNull()}>(loc, k{f.KeyIndex})")
                .Join(",\n");
            var sigs = asyncFields.Select(f => $"k{f.KeyIndex}").Join(",\n");

            var argsForCall = (asyncFields.Any() ? paramss + ",\n" : "") + "ct";

            return $@"
    public new static readonly Sig AsyncSignatureStatic = MakeAsyncSignatureStatic();

    private static Sig MakeAsyncSignatureStatic()
    {{
        string?[] names = new string?[] {{
            {names.Shift(12).Trim()}
        }};

        {keys.Shift(8).Trim()}
        return Sig.Of(
            {sigs.Shift(12).Trim()}
        );
    }}

    public new static IAsyncFunctoid LiftAsync()
    {{
        string?[] names = new string?[] {{
            {names.Shift(12).Trim()}
        }};

        {keys.Shift(8).Trim()}

        return new AsyncFunctoidFromLocator<{fqn}>(
            async (loc, ct) => await {fqn}.{AsyncFactoryMethodName}(
                {argsForCall.Shift(16).Trim()}
            ).ConfigureAwait(false),
            AsyncSignatureStatic
        );
    }}";
        }

        /// <summary>
        /// The generated <c>LiftAsync()</c> awaits <c>CreateAsync(...)</c> inside a
        /// <c>Func&lt;ILocator, CancellationToken, Task&lt;Self&gt;&gt;</c>, so a return type that
        /// awaits to nothing (<c>void</c>, non-generic <c>Task</c>/<c>ValueTask</c>) or to an
        /// unrelated type would surface only as a conversion error inside generated code.
        /// A return type that is not a recognised Task/ValueTask is left to the compiler: a
        /// custom awaitable may well be liftable, and rejecting it here would break it.
        /// </summary>
        private static bool ReturnsAwaitableOfSelf(IMethodSymbol method, INamedTypeSymbol nts)
        {
            if (method.ReturnsVoid) return false;
            if (!IsTaskLike(method.ReturnType, out var awaited)) return true;

            return awaited != null && IsSelfOrDerived(awaited, nts);
        }

        /// <param name="awaited">
        /// The single type argument of the matched Task/ValueTask, or null when it has none.
        /// </param>
        private static bool IsTaskLike(ITypeSymbol tpe, out ITypeSymbol? awaited)
        {
            for (ITypeSymbol? current = tpe; current != null; current = current.BaseType)
            {
                if (current is not INamedTypeSymbol named) continue;
                if (named.Name != "Task" && named.Name != "ValueTask") continue;
                if (named.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    != TasksNamespaceFqn) continue;

                awaited = named.TypeArguments.Length == 1 ? named.TypeArguments[0] : null;
                return true;
            }

            awaited = null;
            return false;
        }

        private static bool IsSelfOrDerived(ITypeSymbol tpe, INamedTypeSymbol nts)
        {
            var self = nts.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            for (ITypeSymbol? current = tpe; current != null; current = current.BaseType)
                if (current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == self)
                    return true;

            return false;
        }
    }
}
