using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DICS.Generator
{
    [Generator]
    public class InjectionValidatorGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var typeDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => IsTypeDeclaration(node),
                    static (syntaxContext, ct) => GetTypeSymbol(syntaxContext))
                .Where(static symbol => symbol is not null)!;

            context.RegisterSourceOutput(typeDeclarations, static (sourceProductionContext, typeSymbol) =>
            {
                if (typeSymbol != null)
                {
                    var hasInjectable = typeSymbol.HasLiftInitializer();
                    var hasLiftFac = typeSymbol.HasLiftFac();

                    var hasInjectField = HasInjectField(typeSymbol);
                    var hasLocalField = HasLocalField(typeSymbol);

                    var injectableParents = typeSymbol.ExtractParents().Where(HasInjectField);

                    var isAbstract = typeSymbol is { IsAbstract: true };

                    // INJ001 ([Inject] without [LiftInitializer]) and INJ004 ([Local]
                    // without [LiftFactory]) are no longer necessary: presence of the
                    // member-level marker is now sufficient — the generator infers
                    // [LiftInitializer] / [LiftFactory] for the class.

                    // INJ001-P still applies: a non-abstract child that has *no* local
                    // DI signal of its own but inherits [Inject] fields from a parent
                    // still needs an explicit [LiftInitializer] or [LiftFactory], because
                    // inference only triggers on the *child*'s own member markers.
                    if (!isAbstract && injectableParents.Any()
                        && !(hasInjectable || hasLiftFac || hasInjectField || hasLocalField))
                    {
                        var diagnostic = Diagnostic.Create(
                            Descriptors.DICS0001_MissingInjectableAttributeP,
                            typeSymbol.Locations.FirstOrDefault(),
                            typeSymbol.Name);

                        sourceProductionContext.ReportDiagnostic(diagnostic);
                    }
                }
            });
        }

        private static bool HasInjectField(INamedTypeSymbol typeSymbol)
        {
            return HasFieldsWith(typeSymbol, LiftInitializerGenerator.InjectAttr);
        }

        private static bool HasLocalField(INamedTypeSymbol typeSymbol)
        {
            return HasFieldsWith(typeSymbol, LiftFactoryGenerator.LocalAttr);
        }

        private static bool HasFieldsWith(INamedTypeSymbol typeSymbol, string id)
        {
            var hasFields = typeSymbol
                .GetMembers()
                .OfType<IFieldSymbol>()
                .Any(fieldSymbol =>
                    fieldSymbol
                        .GetAttributes()
                        .Any(a =>
                            a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                            id));
            var hasProps = typeSymbol
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Any(fieldSymbol =>
                    fieldSymbol
                        .GetAttributes()
                        .Any(a =>
                            a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                            id));
            return hasFields || hasProps;
        }


        private static bool IsTypeDeclaration(SyntaxNode node)
        {
            if (node is TypeDeclarationSyntax tds &&
                tds is ClassDeclarationSyntax or RecordDeclarationSyntax)
                return true;
            return false;
        }

        private static INamedTypeSymbol? GetTypeSymbol(GeneratorSyntaxContext context)
        {
            var typeSyntax = (TypeDeclarationSyntax)context.Node;
            return context.SemanticModel.GetDeclaredSymbol(typeSyntax);
        }

        private static class Descriptors
        {
            internal static readonly DiagnosticDescriptor DICS0001_MissingInjectableAttributeP =
                new(
                    "INJ001",
                    $"Missing [{LiftInitializerGenerator.LiftInitAttr}] Attribute",
                    "Type '{0}' has at least one injectable parent but is missing the [LiftInitializer] attribute.",
                    "DICS",
                    DiagnosticSeverity.Error,
                    true);
        }
    }
}