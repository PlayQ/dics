using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace DICS.Generator
{
    public interface IGenericKeyed
    {
        string Name();
        ITypeSymbol KeyTpe();
        string KeyName();
    }

    public class FieldRulesChecker
    {
        public static void CheckConflicts(SourceProductionContext spc, INamedTypeSymbol typeSymbol,
            List<IGenericKeyed> fields)
        {
            var bad = fields.IndexBy(f => (f.KeyTpe(), f.KeyName())).Where(kv => kv.Value.Count > 1).ToArray();
            if (bad.Any())
            {
                var diagnostic = Diagnostic.Create(
                    Descriptors.DICS0001_KeyConflicts,
                    typeSymbol.Locations.FirstOrDefault(),
                    typeSymbol.Name,
                    bad.Select(a => $"{a.Key}: {a.Value.Select(f => f.Name()).Join(",")}").ToList().Join(";")
                );

                spc.ReportDiagnostic(diagnostic);
            }
        }

        private static class Descriptors
        {
            internal static readonly DiagnosticDescriptor DICS0001_KeyConflicts =
                new(
                    "INJ005",
                    "Conflicting keys",
                    "Type '{0}' has multiple fields with same keys: {1}",
                    "DICS",
                    DiagnosticSeverity.Error,
                    true);
        }
    }
}