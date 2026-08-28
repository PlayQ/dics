using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DICS.Generator
{
    public record FieldRepr(ISymbol Field, ITypeSymbol Tpe, string KeyName, string KeyCode, int KeyIndex)
        : IGenericKeyed
    {
        public string Name()
        {
            return Field.Name;
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
    public class LiftInitializerGenerator : IIncrementalGenerator
    {
        public static readonly string LiftInitAttr = "global::DICS.Attribute.LiftInitializer";
        public static readonly string InjectAttr = "global::DICS.Attribute.Inject";
        private const string AsyncInitMethodName = "InitializeAsync";
        private const string CancellationTokenFqn = "global::System.Threading.CancellationToken";
        private const string TaskFqn = "global::System.Threading.Tasks.Task";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var explicitly = context.FindTaggedTypes(LiftInitAttr);
            var inferred = context.FindTypesWithMemberAttribute(InjectAttr);

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
            if (classSymbol.GetAttributeByName(LiftConstructorGenerator.LiftConstructorAttr) != null)
                spc.Fail(new IssueDescriptor(nts));

            var initParents = nts.InitializableParents(spc);

            // [Inject][Local] fields are supplied via a factory's Create(...) call, not
            // from the locator. Drop them here so the generated initializer does not
            // try to Resolve a key the user never intended to bind.
            var localFields = nts.ExtractFields(spc)
                .Where(f => f.Field.GetAttributeByName(LiftFactoryGenerator.LocalAttr) == null)
                .ToList();

            var fields = localFields;

            FieldRulesChecker.CheckConflicts(spc, nts, fields.ToList<IGenericKeyed>());

            var names = fields.Select(t => t.KeyName).Join(",\n");

            var sups = initParents.Select(p => $"(({p.Fqn()})this).Initialize(loc, {p.Fqn()}.SignatureStatic );")
                .Join("\n");

            var extends = initParents.Select(p => $".AppendUnchecked( {p.Fqn()}.SignatureStatic )")
                .Join("\n");

            var keys = fields
                .Select(f => $"var k{f.KeyIndex} = IInitializer.KeyN<{f.Tpe.FqnNotNull()}>(names, {f.KeyIndex});")
                .Join("\n");

            var paramss = localFields
                .Select(f => $"this.{f.Field.Name} = loc.Resolve<{f.Tpe.FqnNotNull()}>({f.KeyCode});")
                .Join("\n");

            var sigs = fields.Select(f => $"k{f.KeyIndex}").Join(",\n");

            var args = nts.RenderTypeArgs();

            var fqn = classSymbol.Fqn();

            var hasAsyncInit = HasAsyncInitMethod(spc, nts);
            var implementsInterfaces = hasAsyncInit
                ? "ILifecycleComponent, IAsyncLifecycleComponent"
                : "ILifecycleComponent";

            var syncBlock = $@"public new void Initialize(ILocator loc, Sig sig)
    {{
        {sups.Shift(8).Trim()}
        {paramss.Shift(8).Trim()}
    }}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            )
            {extends.Shift(12).Trim()}
            ;
    }}

    public new static IInitializer LiftInitializer() {{
        var theSig = MakeSignatureStatic();
        return new InitializerFromLocator<{fqn}>(
            (self, sig, loc) => self.Initialize(loc, sig),
            theSig
        );
    }}";

            var asyncBlock = hasAsyncInit ? BuildAsyncBlock(fqn) : string.Empty;

            var defn = $@"{nts.DeclaredAccessibility.ToCSharpKeyword()}{nts.ToExtraTypeModifiers()} partial {nts.Keyword()} {className}{args} : {implementsInterfaces}
{{
    {syncBlock.Shift(4).Trim()}
{asyncBlock}
}}";

            var source = classSymbol.WrapIntoNamespace(defn);
            spc.AddSource(classSymbol.UniqueFilename("Initializer"), source);
        }

        private static bool HasAsyncInitMethod(SourceProductionContext spc, INamedTypeSymbol nts)
        {
            var method = nts.GetMembers(AsyncInitMethodName)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => !m.IsStatic);
            if (method == null) return false;

            var parameters = method.Parameters;
            if (parameters.Length != 1 ||
                parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                != CancellationTokenFqn)
            {
                var diag = Diagnostic.Create(
                    new DiagnosticDescriptor("INJ023", $"{AsyncInitMethodName} must take a single CancellationToken",
                        $"Type '{{0}}'.{AsyncInitMethodName} must have exactly one parameter of type System.Threading.CancellationToken to enable async initialization lifting.",
                        "DICS", DiagnosticSeverity.Error, true),
                    method.Locations.FirstOrDefault(), nts.Name);
                spc.ReportDiagnostic(diag);
                return false;
            }

            if (!ReturnsTask(method))
            {
                var diag = Diagnostic.Create(
                    new DiagnosticDescriptor("INJ024", $"{AsyncInitMethodName} must return Task",
                        $"Type '{{0}}'.{AsyncInitMethodName} returns '{{1}}'; it must return System.Threading.Tasks.Task (or a Task-derived type) to enable async initialization lifting.",
                        "DICS", DiagnosticSeverity.Error, true),
                    method.Locations.FirstOrDefault(), nts.Name,
                    method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                spc.ReportDiagnostic(diag);
                return false;
            }

            return true;
        }

        /// <summary>
        /// The generated <c>Initialize(loc, sig, ct)</c> returns <c>InitializeAsync(ct)</c> as its
        /// own <c>Task</c>, so a return type that is not assignable to Task (<c>void</c>,
        /// <c>ValueTask</c>) would surface only as a conversion error inside generated code.
        /// </summary>
        private static bool ReturnsTask(IMethodSymbol method)
        {
            for (ITypeSymbol? tpe = method.ReturnType; tpe != null; tpe = tpe.BaseType)
                if (tpe.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == TaskFqn)
                    return true;

            return false;
        }

        private static string BuildAsyncBlock(string fqn)
        {
            return $@"
    public new System.Threading.Tasks.Task Initialize(ILocator loc, Sig sig, System.Threading.CancellationToken ct)
    {{
        this.Initialize(loc, sig);
        return this.{AsyncInitMethodName}(ct);
    }}

    public new static IAsyncInitializer LiftAsyncInitializer()
    {{
        return IAsyncInitializer.FromComponent<{fqn}>(SignatureStatic);
    }}";
        }
    }
}
