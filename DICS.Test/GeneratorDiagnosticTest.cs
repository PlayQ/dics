using System.Linq;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Per-rule tests for the DICS source-generator diagnostics. Each test compiles a
    /// minimal snippet that exercises exactly one rule and asserts the diagnostic ID.
    /// Includes negative tests for the inference heuristic — presence of [Inject] or
    /// [Local] on a class with no class-level Lift attribute must NOT raise the legacy
    /// "missing attribute" diagnostics.
    /// </summary>
    public class GeneratorDiagnosticTest
    {
        // INJ003: [LiftConstructor] and [LiftInitializer] on the same class are mutually exclusive.
        [Test]
        public void INJ003_LiftConstructorAndLiftInitializer_Conflict()
        {
            const string src = @"
using DICS.Attribute;
namespace T {
    [LiftConstructor]
    [LiftInitializer]
    public partial class Conflicted { [Inject] protected int X; }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ003"), Is.True,
                "INJ003 must fire when [LiftConstructor] and [LiftInitializer] are combined");
        }

        // INJ002: a parent with [Inject] fields and no class-level
        // [LiftInitializer] is now recognised as an inference-path initializer — the
        // generator emits an initializer for it implicitly — and INJ002 must NOT fire
        // on a [LiftInitializer]-tagged child that inherits from such a parent.
        // The InferencePathParent_DoesNotTrigger_INJ002 case covers the same contract.
        [Test]
        public void INJ002_InjectableParent_InferencePath_DoesNotFire()
        {
            const string src = @"
using DICS.Attribute;
namespace T {
    public partial class Parent { [Inject] protected int Dep; }

    [LiftInitializer]
    public partial class Child : Parent { }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ002"), Is.False,
                "INJ002 must not fire when the parent has [Inject] (inference covers it)");
        }

        // INJ015: [Inject] on a private field of a non-sealed class doesn't work across assemblies.
        [Test]
        public void INJ015_PrivateInjectField_OnNonSealedClass()
        {
            const string src = @"
using DICS.Attribute;
namespace T {
    [LiftInitializer]
    public partial class Open { [Inject] private int _x; public int Read() => _x; }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ015"), Is.True,
                "INJ015 must fire when an [Inject] field is private on a non-sealed class");
        }

        // INJ015: same field, but on a sealed class — private is fine; no diagnostic.
        [Test]
        public void INJ015_PrivateInjectField_OnSealedClass_DoesNotFire()
        {
            const string src = @"
using DICS.Attribute;
namespace T {
    [LiftInitializer]
    public sealed partial class Closed { [Inject] private int _x; public int Read() => _x; }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ015"), Is.False,
                "INJ015 must not fire when the enclosing class is sealed");
        }

        // INJ021: CreateAsync must end with CancellationToken when [LiftConstructor] is present.
        [Test]
        public void INJ021_CreateAsync_MissingCancellationToken_Fires()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading.Tasks;
namespace T {
    [LiftConstructor]
    public partial class Bad {
        private Bad() {}
        public static Task<Bad> CreateAsync() => Task.FromResult(new Bad());
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ021"), Is.True,
                "INJ021 must fire when CreateAsync's last parameter is not CancellationToken");
        }

        // INJ021: well-formed CreateAsync — last param IS CancellationToken — does not fire.
        [Test]
        public void INJ021_CreateAsync_WithCancellationToken_DoesNotFire()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading;
using System.Threading.Tasks;
namespace T {
    [LiftConstructor]
    public partial class Good {
        private Good() {}
        public static Task<Good> CreateAsync(CancellationToken ct) => Task.FromResult(new Good());
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ021"), Is.False);
        }

        // INJ023: InitializeAsync must take exactly one CancellationToken parameter.
        [Test]
        public void INJ023_InitializeAsync_WrongSignature_Fires()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading.Tasks;
namespace T {
    [LiftInitializer]
    public partial class Bad {
        [Inject] protected int X;
        public Task InitializeAsync() => Task.CompletedTask;
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ023"), Is.True,
                "INJ023 must fire when InitializeAsync does not take a single CancellationToken");
        }

        // INJ024: InitializeAsync must return Task (or a Task-derived type). The generated
        // Initialize(loc, sig, ct) returns InitializeAsync(ct) as its Task, so a void or
        // ValueTask return only surfaces as a CS conversion error inside generated code.
        [Test]
        public void INJ024_InitializeAsync_VoidReturn_Fires()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading;
namespace T {
    [LiftInitializer]
    public partial class Bad {
        [Inject] protected int X;
        public void InitializeAsync(CancellationToken ct) { }
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ024"), Is.True,
                "INJ024 must fire when InitializeAsync returns void. Diagnostics: " + Dump(result));
            Assert.That(result.Diagnostics.Any(d => d.Id.StartsWith("CS")), Is.False,
                "Rejecting the method must leave the generated code compilable. Diagnostics: "
                + Dump(result));
        }

        // INJ024: ValueTask is awaitable but not assignable to Task, so it is rejected too.
        [Test]
        public void INJ024_InitializeAsync_ValueTaskReturn_Fires()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading;
using System.Threading.Tasks;
namespace T {
    [LiftInitializer]
    public partial class Bad {
        [Inject] protected int X;
        public ValueTask InitializeAsync(CancellationToken ct) => default;
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ024"), Is.True,
                "INJ024 must fire when InitializeAsync returns ValueTask. Diagnostics: " + Dump(result));
            Assert.That(result.Diagnostics.Any(d => d.Id.StartsWith("CS")), Is.False,
                "Rejecting the method must leave the generated code compilable. Diagnostics: "
                + Dump(result));
        }

        // Task and Task-derived (Task<T>) returns are both assignable to the generated
        // method's Task return, so neither is rejected.
        [Test]
        public void INJ024_InitializeAsync_TaskReturn_DoesNotFire()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading;
using System.Threading.Tasks;
namespace T {
    [LiftInitializer]
    public partial class Good {
        [Inject] protected int X;
        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ024"), Is.False, Dump(result));
            Assert.That(result.Diagnostics.Any(d => d.Id.StartsWith("CS")), Is.False, Dump(result));
        }

        [Test]
        public void INJ024_InitializeAsync_TaskOfTReturn_DoesNotFire()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading;
using System.Threading.Tasks;
namespace T {
    [LiftInitializer]
    public partial class Good {
        [Inject] protected int X;
        public Task<int> InitializeAsync(CancellationToken ct) => Task.FromResult(1);
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ024"), Is.False, Dump(result));
            Assert.That(result.Diagnostics.Any(d => d.Id.StartsWith("CS")), Is.False, Dump(result));
        }

        // INJ025: CreateAsync must return an awaitable whose result is the declaring type.
        // The generated LiftAsync() awaits CreateAsync(...) inside a
        // Func<ILocator, CancellationToken, Task<Self>>, so void has nothing to await.
        [Test]
        public void INJ025_CreateAsync_VoidReturn_Fires()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading;
namespace T {
    [LiftConstructor]
    public partial class Bad {
        private Bad() {}
        public static void CreateAsync(CancellationToken ct) {}
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ025"), Is.True,
                "INJ025 must fire when CreateAsync returns void. Diagnostics: " + Dump(result));
            Assert.That(result.Diagnostics.Any(d => d.Id.StartsWith("CS")), Is.False,
                "Rejecting the method must leave the generated code compilable. Diagnostics: "
                + Dump(result));
        }

        // INJ025: a non-generic Task awaits to no value, so there is nothing to hand back
        // as the produced instance.
        [Test]
        public void INJ025_CreateAsync_NonGenericTaskReturn_Fires()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading;
using System.Threading.Tasks;
namespace T {
    [LiftConstructor]
    public partial class Bad {
        private Bad() {}
        public static Task CreateAsync(CancellationToken ct) => Task.CompletedTask;
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ025"), Is.True,
                "INJ025 must fire when CreateAsync returns a non-generic Task. Diagnostics: "
                + Dump(result));
            Assert.That(result.Diagnostics.Any(d => d.Id.StartsWith("CS")), Is.False,
                "Rejecting the method must leave the generated code compilable. Diagnostics: "
                + Dump(result));
        }

        // INJ025: awaiting yields a value, but not one assignable to the declaring type.
        [Test]
        public void INJ025_CreateAsync_UnrelatedResult_Fires()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading;
using System.Threading.Tasks;
namespace T {
    [LiftConstructor]
    public partial class Bad {
        private Bad() {}
        public static Task<int> CreateAsync(CancellationToken ct) => Task.FromResult(1);
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ025"), Is.True,
                "INJ025 must fire when CreateAsync's result is not the declaring type. Diagnostics: "
                + Dump(result));
            Assert.That(result.Diagnostics.Any(d => d.Id.StartsWith("CS")), Is.False,
                "Rejecting the method must leave the generated code compilable. Diagnostics: "
                + Dump(result));
        }

        [Test]
        public void INJ025_CreateAsync_TaskOfSelf_DoesNotFire()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading;
using System.Threading.Tasks;
namespace T {
    [LiftConstructor]
    public partial class Good {
        private Good() {}
        public static Task<Good> CreateAsync(CancellationToken ct) => Task.FromResult(new Good());
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ025"), Is.False, Dump(result));
            Assert.That(result.Diagnostics.Any(d => d.Id.StartsWith("CS")), Is.False, Dump(result));
        }

        // ValueTask<Self> is awaitable and its result is the declaring type, so the emitted
        // lambda compiles. The rule must not reject what the generator can already lift.
        [Test]
        public void INJ025_CreateAsync_ValueTaskOfSelf_DoesNotFire()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading;
using System.Threading.Tasks;
namespace T {
    [LiftConstructor]
    public partial class Good {
        private Good() {}
        public static ValueTask<Good> CreateAsync(CancellationToken ct)
            => new ValueTask<Good>(new Good());
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ025"), Is.False, Dump(result));
            Assert.That(result.Diagnostics.Any(d => d.Id.StartsWith("CS")), Is.False, Dump(result));
        }

        // A result assignable to the declaring type — a subclass — also lifts.
        [Test]
        public void INJ025_CreateAsync_TaskOfDerived_DoesNotFire()
        {
            const string src = @"
using DICS.Attribute;
using System.Threading;
using System.Threading.Tasks;
namespace T {
    public class Derived : Good { public Derived() {} }

    [LiftConstructor]
    public partial class Good {
        public Good() {}
        public static Task<Derived> CreateAsync(CancellationToken ct)
            => Task.FromResult(new Derived());
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ025"), Is.False, Dump(result));
            Assert.That(result.Diagnostics.Any(d => d.Id.StartsWith("CS")), Is.False, Dump(result));
        }

        private static string Dump(GeneratorDiagnosticHarness.Result result) =>
            string.Join(" | ", result.Diagnostics.Select(d => d.Id + ": " + d.GetMessage()));

        // Inference path: a class with [Inject] but NO class-level Lift attribute must not
        // raise the legacy INJ001 "missing [LiftInitializer]" diagnostic — the generator
        // infers it.
        [Test]
        public void INJ001_LegacyDiagnostic_DoesNotFire_OnInferredInitializer()
        {
            const string src = @"
using DICS.Attribute;
namespace T {
    public partial class Inferred { [Inject] protected int Dep; }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ001"), Is.False,
                "INJ001 must not fire when [Inject] alone is sufficient (inference covers it)");
        }

        // Inference path: a class with [Local] but no class-level Lift attribute must not
        // raise the legacy INJ004 "missing [LiftFactory]" diagnostic.
        [Test]
        public void INJ004_LegacyDiagnostic_DoesNotFire_OnInferredFactory()
        {
            const string src = @"
using DICS.Attribute;
namespace T {
    public partial record Inferred([Local] int N);
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ004"), Is.False,
                "INJ004 must not fire when [Local] alone is sufficient (inference covers it)");
        }

        // INJ001-P (which still uses ID 'INJ001'): a non-abstract child inherits [Inject]
        // from a parent but declares no DI signal of its own — still an error.
        [Test]
        public void INJ001P_ChildInheritsInject_WithoutOwnSignal_Fires()
        {
            const string src = @"
using DICS.Attribute;
namespace T {
    [LiftInitializer]
    public partial class P { [Inject] protected int X; }

    public partial class C : P { }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ001"), Is.True,
                "INJ001-P must still fire when a non-abstract child has no own DI signal");
        }

        // A nested partial class tagged [LiftConstructor]. The generator must
        // wrap the emitted partial in shells matching the type's containing types,
        // otherwise the emitted partial refers to a type that doesn't exist at the
        // namespace level.
        [Test]
        public void NestedClass_LiftConstructor_CompilesCleanly()
        {
            const string src = @"
using DICS.Attribute;
namespace T {
    public partial class Container {
        [LiftConstructor]
        public partial class Inner {
            private Inner() {}
        }
    }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            var errors = result.Diagnostics
                .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .ToList();
            Assert.That(errors, Is.Empty,
                "Nested [LiftConstructor] class must produce a compilable partial wrapped in its containing types. Errors: "
                + string.Join("; ", errors.Select(e => e.Id + ": " + e.GetMessage())));
        }

        // Modifier preservation. The most acute case is 'static partial class':
        // every partial declaration of a static class must itself be 'static' (CS0262
        // otherwise). Use [LiftFactory] because it emits no instance-only members and
        // no interface implementation on the outer partial — its emitted body is a
        // static factory method plus nested classes, which a static outer class
        // permits. Pre-fix the generator emits 'partial class' (no 'static') for a
        // static outer, triggering CS0262.
        // Modifier preservation. The generator's emitted partial declarations
        // (both for the target type and for any containing-type wrap shells) must
        // repeat the user's 'static' / 'sealed' / 'abstract' modifiers. C# 9+ merges
        // partials leniently — a non-static wrap of a static outer compiles — so the
        // most reliable observable is a textual assertion on the generated source.
        // Pre-fix the generator emits 'public partial class Inner' for a sealed
        // primary; post-fix it must emit 'public sealed partial class Inner'.
        [Test]
        public void ModifierPreservation_GeneratedSourceCarriesSealedAndStatic()
        {
            const string src = @"
using DICS.Attribute;
namespace T {
    public static partial class Outer {
        [LiftConstructor]
        public sealed partial class Inner {
            private Inner() {}
        }
    }
}";
            // Run the generator directly so we can inspect its emitted trees.
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(src);
            var trustedAssemblies =
                ((string?)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
                .Split(System.IO.Path.PathSeparator, System.StringSplitOptions.RemoveEmptyEntries);
            var refs = trustedAssemblies
                .Select(p => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(p))
                .Cast<Microsoft.CodeAnalysis.MetadataReference>()
                .Append(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(
                    typeof(DICS.Attribute.LiftConstructor).Assembly.Location))
                .ToList();
            var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
                "harness-d012-" + System.Guid.NewGuid().ToString("N"),
                new[] { tree },
                refs,
                new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                    Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
            var driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(
                new DICS.Generator.LiftConstructorGenerator());
            driver = (Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver)driver
                .RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
            var runResult = driver.GetRunResult();
            var generatedTexts = runResult.GeneratedTrees
                .Select(t => t.GetText().ToString())
                .ToList();
            var joined = string.Join("\n----\n", generatedTexts);
            // The Inner's own partial must carry 'sealed' (matching the user's primary).
            Assert.That(joined, Does.Contain("sealed partial class Inner"),
                "Generator must repeat 'sealed' modifier on the emitted partial of a sealed class. Generated:\n" + joined);
            // The containing-type wrap shell for Outer must carry 'static' (matching the user's primary).
            Assert.That(joined, Does.Contain("static partial class Outer"),
                "Generator must repeat 'static' modifier on the wrap shell of a static containing class. Generated:\n" + joined);
        }

        // A parent class with [Inject] field and no class-level [LiftInitializer]
        // (inference path) — its [LiftInitializer]-tagged child must not be reported as
        // "missing the [LiftInitializer] attribute on the parent" because the generator
        // emits one for the parent via inference.
        [Test]
        public void InferencePathParent_DoesNotTrigger_INJ002()
        {
            const string src = @"
using DICS.Attribute;
namespace T {
    public partial class InferredParent { [Inject] protected int Dep; }

    [LiftInitializer]
    public partial class ExplicitChild : InferredParent { }
}";
            var result = GeneratorDiagnosticHarness.Run(src);
            Assert.That(result.Fired("INJ002"), Is.False,
                "INJ002 must not fire when the parent is an inference-path [LiftInitializer]");
        }

        // The dead INJ001 and INJ004 descriptors in Validator.cs were removed
        // because the inference heuristic made them unreachable. Assert via reflection
        // that neither descriptor identifier remains as a private static field on the
        // Validator's nested Descriptors type.
        [Test]
        public void DeadValidatorDescriptors_AreRemoved()
        {
            var validatorAsm = typeof(DICS.Generator.InjectionValidatorGenerator).Assembly;
            var descriptors = validatorAsm
                .GetType("DICS.Generator.InjectionValidatorGenerator+Descriptors",
                    throwOnError: false);
            Assert.That(descriptors, Is.Not.Null, "Descriptors nested type must still exist");
            var fields = descriptors!.GetFields(
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance);
            var names = fields.Select(f => f.Name).ToList();
            Assert.That(names, Does.Not.Contain("DICS0001_MissingInjectableAttribute"),
                "Dead descriptor DICS0001_MissingInjectableAttribute must be removed");
            Assert.That(names, Does.Not.Contain("DICS0001_MissingLiftFac"),
                "Dead descriptor DICS0001_MissingLiftFac must be removed");
        }
    }
}
