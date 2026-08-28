using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DICS.Generator
{
    public record TypedSymbol(ISymbol Symbol, ITypeSymbol Tpe, string Name);

    public static class GeneratorTools
    {
        public static List<FieldRepr> ExtractFields(this INamedTypeSymbol nts, SourceProductionContext spc)
        {
            var fieldSymbols = nts.FieldSymbols(spc);

            var fields = fieldSymbols
                .Select((f, i) =>
                {
                    var name = f.Symbol.GetAttributeByName(LiftConstructorGenerator.IdAttr);
                    var nameRepr = name != null ? $"\"{name.ConstructorArguments.First().Value}\"" : "null";

                    // var keyCode = name != null ? $"Key.Of<{f.Type.Fqn()}>({nameRepr})" : $"Key.Of<{f.Type.Fqn()}>()";
                    var keyCode = $"sig.Args[{i}]";
                    var tpe = f.Tpe;
                    return new FieldRepr(f.Symbol, tpe, nameRepr, keyCode, i);
                })
                .ToList();
            return fields;
        }

        public static List<INamedTypeSymbol> InitializableParents(this INamedTypeSymbol nts,
            SourceProductionContext spc)
        {
            var parents = nts.ExtractParents();

            var initParents = new List<INamedTypeSymbol>();


            foreach (var namedTypeSymbol in parents)
            {
                var pfields = ExtractFields(namedTypeSymbol, spc).ToList();
                // The inference heuristic emits a [LiftInitializer] for any class that
                // has [Inject] members but no class-level Lift attribute. Treat such a
                // parent as already initializable; otherwise INJ002 would fire on every
                // child of an inference-path parent.
                var hasInjectable = namedTypeSymbol.HasLiftInitializer()
                                    || HasInjectMember(namedTypeSymbol);
                if (pfields.Any())
                {
                    if (!hasInjectable)
                    {
                        var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "INJ002",
                                $"Missing [{LiftInitializerGenerator.LiftInitAttr}] Attribute",
                                $"Type '{{0}}' has at least one field marked with [Inject] but is missing the [LiftInitializer] attribute, required because {nts.Name} inherits it.",
                                "DICS",
                                DiagnosticSeverity.Error,
                                true),
                            namedTypeSymbol.Locations.FirstOrDefault(),
                            namedTypeSymbol.Name
                        );

                        spc.ReportDiagnostic(diagnostic);
                    }

                    initParents.Add(namedTypeSymbol);
                }
            }

            return initParents;
        }

        public static List<TypedSymbol> FieldSymbols(this INamedTypeSymbol nts, SourceProductionContext spc)
        {
            var props = nts.GetMembers().OfType<IPropertySymbol>().ToList();
            var flds = nts.GetMembers().OfType<IFieldSymbol>().ToList();
            var all = new List<TypedSymbol>();

            foreach (var propertySymbol in props)
                all.Add(new TypedSymbol(propertySymbol, propertySymbol.Type, propertySymbol.Name));
            foreach (var fieldSymbol in flds) all.Add(new TypedSymbol(fieldSymbol, fieldSymbol.Type, fieldSymbol.Name));

            var fieldSymbols = all
                .Where(m => m.Symbol.GetAttributeByName(LiftInitializerGenerator.InjectAttr) != null).ToList();

            var badFields = fieldSymbols.Where(t => t.Symbol.DeclaredAccessibility == Accessibility.Private).ToList();

            if (badFields.Count > 0 && !nts.IsSealed)
            {
                var diagnostic = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "INJ015",
                        "Injectable field is private",
                        $"Type '{{0}}' has private fields marked with [Inject], that doesn't work across assemblies. Make these fields protected: {badFields.Select(f => f.Name).Join(",")}.",
                        "DICS",
                        DiagnosticSeverity.Error,
                        true),
                    nts.Locations.FirstOrDefault(),
                    nts.Name
                );

                spc.ReportDiagnostic(diagnostic);
            }

            return fieldSymbols;
        }

        public static string WrapIntoNamespace(this ISymbol classSymbol, string defn)
        {
            // Wrap in equivalent `partial` shells for every containing type so that a
            // generated partial declared for a nested type lands at the right scope.
            // Each shell repeats accessibility, the static/abstract/sealed modifiers,
            // the `partial` keyword, the class/record keyword, the name, and generic
            // parameters — but NOT base types or interfaces (the user's primary
            // declaration carries those).
            var wrapped = defn;
            var containing = classSymbol.ContainingType;
            while (containing != null)
            {
                var head =
                    $"{containing.DeclaredAccessibility.ToCSharpKeyword()}{containing.ToExtraTypeModifiers()} partial {containing.Keyword()} {containing.Name}{containing.RenderTypeArgs()}";
                wrapped = $@"{head}
{{
    {wrapped.Shift(4).Trim()}
}}";
                containing = containing.ContainingType;
            }

            var withNs = wrapped.InNs(classSymbol.ContainingNamespace());

            var source = $@"
#nullable enable
#pragma warning disable CS0109 // The new keyword is not required

using System;
using System.Runtime.CompilerServices;
using DICS;

{withNs}";
            return source;
        }

        public static IMethodSymbol BestConstructor(this INamedTypeSymbol nts)
        {
            var ctors = nts.InstanceConstructors;
            var bestCtor = ctors.OrderBy(c => c.Parameters.Length).Last();
            return bestCtor;
        }

        public static string Keyword(this INamedTypeSymbol nts)
        {
            return nts.IsRecord ? "record" : "class";
        }

        public static string UniqueFilename(this ISymbol classSymbol, string suffix)
        {
            var ns = ContainingNamespace(classSymbol);

            return $"{ns}--{classSymbol.Name}--{suffix}.g.cs";
        }

        public static string ContainingNamespace(this ISymbol classSymbol)
        {
            return classSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : classSymbol.ContainingNamespace.ToString();
        }

        public static List<INamedTypeSymbol> ExtractParents(this INamedTypeSymbol nts)
        {
            var res = new List<INamedTypeSymbol>();
            ExtractParents(nts, res);
            return res;
        }

        private static void ExtractParents(INamedTypeSymbol nts, List<INamedTypeSymbol> hier)
        {
            if (nts.BaseType != null)
            {
                hier.Add(nts.BaseType);
                hier.AddRange(nts.Interfaces);
                ExtractParents(nts.BaseType, hier);
            }
        }

        public static bool HasLiftInitializer(this INamedTypeSymbol? namedTypeSymbol)
        {
            return namedTypeSymbol != null && namedTypeSymbol
                .GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                          LiftInitializerGenerator.LiftInitAttr);
        }

        /// <summary>
        /// Mirrors the LiftInitializer inference heuristic at the symbol level: returns
        /// true when the type has any field or property tagged <c>[Inject]</c>. Used by
        /// <see cref="InitializableParents"/> to recognise parents that the generator
        /// implicitly treats as initializable even without a class-level
        /// <c>[LiftInitializer]</c>.
        /// </summary>
        public static bool HasInjectMember(this INamedTypeSymbol nts)
        {
            foreach (var m in nts.GetMembers())
            {
                if (m is IFieldSymbol or IPropertySymbol)
                    if (m.GetAttributeByName(LiftInitializerGenerator.InjectAttr) != null)
                        return true;
            }
            return false;
        }

        public static bool HasLiftFac(this INamedTypeSymbol? namedTypeSymbol)
        {
            return namedTypeSymbol != null && namedTypeSymbol
                .GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                          LiftFactoryGenerator.LiftFactory);
        }

        public static string ToCSharpKeyword(this Accessibility accessibility)
        {
            return accessibility switch
            {
                Accessibility.Private => "private",
                Accessibility.Protected => "protected",
                Accessibility.Internal => "internal",
                Accessibility.Public => "public",
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Returns the static/abstract/sealed/unsafe modifiers (with a leading space if
        /// any are present) in canonical order, suitable for insertion between
        /// <see cref="ToCSharpKeyword(Accessibility)"/> and the <c>partial</c> keyword
        /// when emitting a partial declaration for <paramref name="nts"/>. Every partial
        /// declaration of a class must repeat the same modifiers as the primary
        /// declaration; failing to do so triggers compile errors like CS0708 / CS0714 /
        /// CS0265.
        /// </summary>
        public static string ToExtraTypeModifiers(this INamedTypeSymbol nts)
        {
            var parts = new List<string>();
            if (nts.IsStatic) parts.Add("static");
            // Abstract before sealed in canonical order. A single class can't be both
            // (the compiler rejects it), so guarding against the impossible combination
            // is not necessary here.
            if (nts.IsAbstract && !nts.IsStatic) parts.Add("abstract");
            if (nts.IsSealed && !nts.IsStatic) parts.Add("sealed");
            return parts.Count == 0 ? string.Empty : " " + string.Join(" ", parts);
        }

        public static string RenderTypeArgs(this INamedTypeSymbol namedTypeSymbol)
        {
            var sb = new StringBuilder();
            if (namedTypeSymbol.TypeParameters.Length > 0)
            {
                sb.Append('<');
                for (var i = 0; i < namedTypeSymbol.TypeParameters.Length; i++)
                {
                    var typeParameter = namedTypeSymbol.TypeParameters[i];

                    var varianceText = typeParameter.Variance switch
                    {
                        VarianceKind.In => "in ",
                        VarianceKind.Out => "out ",
                        _ => string.Empty
                    };

                    sb.Append(varianceText);
                    sb.Append(typeParameter.Name);

                    if (i < namedTypeSymbol.TypeParameters.Length - 1) sb.Append(", ");
                }

                sb.Append('>');
            }

            return sb.ToString();
        }

        public static IncrementalValuesProvider<ISymbol?> FindTaggedTypes(
            this IncrementalGeneratorInitializationContext context,
            string fqn
        )
        {
            return context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) =>
                        node is TypeDeclarationSyntax { AttributeLists: { Count: > 0 } },
                    (ctx, _) =>
                    {
                        var classSyntax = (TypeDeclarationSyntax)ctx.Node;
                        var symbol = ctx.SemanticModel.GetDeclaredSymbol(classSyntax);

                        foreach (var attributeList in classSyntax.AttributeLists)
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var symbolInfo = ctx.SemanticModel.GetSymbolInfo(attribute);
                            if (symbolInfo.Symbol is IMethodSymbol attributeCtor)
                            {
                                var attributeName =
                                    attributeCtor.ContainingType.Fqn();
                                if (attributeName == fqn)
                                    return symbol;
                            }
                        }

                        return null;
                    })
                .Where(static symbol => symbol is not null)!;
        }

        /// <summary>
        /// Yields each type that contains at least one field, property, or parameter
        /// tagged with the given attribute. Returned symbol is the *containing type*,
        /// not the member itself. Use to drive class-level inference from member-level
        /// markers (e.g. infer [LiftInitializer] from [Inject] fields).
        /// </summary>
        public static IncrementalValuesProvider<ISymbol?> FindTypesWithMemberAttribute(
            this IncrementalGeneratorInitializationContext context,
            string memberAttrFqn)
        {
            return context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) =>
                        (node is FieldDeclarationSyntax fds && fds.AttributeLists.Count > 0)
                        || (node is PropertyDeclarationSyntax pds && pds.AttributeLists.Count > 0)
                        || (node is ParameterSyntax ps && ps.AttributeLists.Count > 0),
                    (ctx, _) =>
                    {
                        var lists = ctx.Node switch
                        {
                            FieldDeclarationSyntax f => f.AttributeLists,
                            PropertyDeclarationSyntax p => p.AttributeLists,
                            ParameterSyntax pr => pr.AttributeLists,
                            _ => default
                        };

                        foreach (var attributeList in lists)
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var symbolInfo = ctx.SemanticModel.GetSymbolInfo(attribute);
                            if (symbolInfo.Symbol is IMethodSymbol attributeCtor &&
                                attributeCtor.ContainingType.Fqn() == memberAttrFqn)
                            {
                                var typeDecl = ctx.Node.FirstAncestorOrSelf<TypeDeclarationSyntax>();
                                if (typeDecl != null)
                                    return (ISymbol?)ctx.SemanticModel.GetDeclaredSymbol(typeDecl);
                            }
                        }

                        return null;
                    })
                .Where(static symbol => symbol is not null)!;
        }

        /// <summary>
        /// Returns true if the type has any class-level Lift attribute. Used by the
        /// member-level inference path to skip classes that already declared their
        /// intent explicitly.
        /// </summary>
        public static bool HasAnyClassLevelLift(this INamedTypeSymbol nts)
        {
            return nts.GetAttributeByName(LiftConstructorGenerator.LiftConstructorAttr) != null
                || nts.GetAttributeByName(LiftInitializerGenerator.LiftInitAttr) != null
                || nts.GetAttributeByName(LiftFactoryGenerator.LiftFactory) != null;
        }

        public static string Fqn(this ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat
                .FullyQualifiedFormat);
        }

        public static string FqnNotNull(this ISymbol symbol)
        {
            var n = symbol.Fqn();
            if (n.EndsWith("?")) n = n.Substring(0, n.Length - 1);
            return n;
        }

        public static AttributeData? GetAttributeByName(this ISymbol symbol,
            string attributeName)
        {
            return symbol.GetAttributes().GetAttributeByName(attributeName);
        }

        public static AttributeData? GetAttributeByName(this ImmutableArray<AttributeData> attributes,
            string attributeName)
        {
            if (string.IsNullOrWhiteSpace(attributeName))
                throw new ArgumentException("Attribute name cannot be null or whitespace.", nameof(attributeName));

            return attributes.FirstOrDefault(attr =>
                attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == attributeName);
        }

        public static void Fail(this SourceProductionContext context,
            IssueDescriptor info)
        {
            var descriptor = new DiagnosticDescriptor(
                "INJ003",
                "Incompatible injection attributes",
                "Cannot combine [LiftInitializer] and [LiftConstructor]",
                "InjectionValidation",
                DiagnosticSeverity.Error,
                true
            );

            var diagnostic = Diagnostic.Create(
                descriptor,
                info.Decl.Locations.FirstOrDefault(),
                info.Decl.Locations.Skip(1),
                info.Decl.Name
            );

            context.ReportDiagnostic(diagnostic);
        }
    }

    public record IssueDescriptor(INamedTypeSymbol Decl);
}