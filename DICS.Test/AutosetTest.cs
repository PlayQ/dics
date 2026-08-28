using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Regression tests for autoset collection.
    ///
    /// Background: <see cref="Binding"/> sub-records used to pass <c>Key.GetType()</c>
    /// (i.e. <c>typeof(Key)</c>) as their <c>ImplType</c>. <see cref="Planner"/>'s
    /// Preprocess step filters autoset candidates via
    /// <c>elementType.IsAssignableFrom(binding.ImplType)</c>, so those bindings were
    /// silently excluded from every autoset whose element type was not assignable
    /// from the <c>Key</c> class itself.
    ///
    /// The most visible symptom: a <c>Ref</c>/alias binding (<see cref="Binding.ToKey"/>)
    /// was never collected into an <c>Auto()</c> autoset, even though the alias's
    /// declared type is assignable to the autoset's element type. Note that at the
    /// runtime-set level the symptom can be hidden by HashSet deduplication when the
    /// alias and its source resolve to the same instance — so this test asserts at
    /// the binding level (the level at which the bug lives).
    /// </summary>
    public class AutosetTest
    {
        public interface IThing { }
        public interface ISpecificThing : IThing { }
        public class SpecificThingA : ISpecificThing { }

        private class AutosetRefModule : Module
        {
            public AutosetRefModule()
            {
                Make<ISpecificThing>().From().Instance(new SpecificThingA());

                // Alias: IThing#alias -> ISpecificThing.
                // Pre-fix, this Binding.ToKey carried ImplType = typeof(Key), excluding it
                // from the autoset of IThing. Post-fix, ImplType = typeof(IThing).
                Make<IThing>().Named("alias").From().Ref<ISpecificThing>();

                Make<ISet<IThing>>().From().Auto();
            }
        }

        [Test]
        public void ToKeyBinding_ImplTypeIsAliasDeclaredType_NotKeyClass()
        {
            var module = new AutosetRefModule().Freeze();
            var toKey = module.Bindings.OfType<Binding.ToKey>().Single();

            // Pre-fix: typeof(Key). Post-fix: typeof(IThing) — the alias's declared Tpe.
            Assert.That(toKey.ImplType, Is.EqualTo(typeof(IThing)),
                "ToKey.ImplType must be the alias key's declared Tpe (so Planner.Preprocess can collect it into matching autosets), not the C# type of the Key record.");
        }

        [Test]
        public void Autoset_CollectsAliasBinding_AtPlanLevel()
        {
            // ImplType eligibility only — not Planner.Preprocess. Preprocess kind-filters
            // ToKey so a local alias is not a second autoset element; this test still
            // locks the ImplType correction.
            var module = new AutosetRefModule().Freeze();
            var autoset = module.Bindings.OfType<Binding.CreateAutoset>().Single();

            var collected = module.Bindings
                .Where(b => autoset.ElementType.IsAssignableFrom(b.ImplType))
                .Select(b => b.Key)
                .ToHashSet();

            // The direct ISpecificThing binding plus the IThing#alias binding both qualify.
            Assert.That(collected, Contains.Item(Key.Of<ISpecificThing>()),
                "Direct ISpecificThing binding should be collected into the IThing autoset.");
            Assert.That(collected, Contains.Item(Key.Of<IThing>("alias")),
                "Pre-fix, the Ref-aliased IThing#alias binding was excluded because Binding.ToKey.ImplType was typeof(Key); post-fix it must be collected.");
        }

        // Regression: Binding.ToKey.ImplType is the alias's declared Tpe, so
        // Planner.Preprocess collected both a disposable producer and its
        // Aliased<Parent>() ToKey into Auto() IList<IDisposable>. IList does not
        // de-duplicate by identity, so MonoModule.OnDestroy disposed the same
        // instance twice. ISet hid this (HashSet). Did not reproduce on master,
        // where ToKey.ImplType was typeof(Key).
        //
        // Taxonomy: Behavioral-Active × Blackbox × Group. Origin: regression.
        public class ParentDisposable : IDisposable
        {
            public int DisposeCount;
            public void Dispose() => DisposeCount++;
        }

        public class ChildDisposable : ParentDisposable
        {
            public static IFunctoid GetFunctoid() => IFunctoid.Lift(() => new ChildDisposable());
        }

        private static Injector InjectorFor(Module module) =>
            new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty, module.Freeze());

        [Test]
        public void AliasedDisposable_AppearsOnceInAutoIList()
        {
            var module = new InlineModule(m =>
            {
                m.Make<ChildDisposable>().From().Functoid(ChildDisposable.GetFunctoid()).Aliased<ParentDisposable>();
                m.Make<IList<IDisposable>>().From().Auto();
            });

            // Request the alias key as well as the list: MonoModule scenes that
            // .Aliased<Parent>() also expose Parent as a root or dependency.
            // Producing only the list would no longer materialize Parent, because
            // the autoset no longer depends on the alias key.
            var loc = InjectorFor(module).Produce(
                Key.Of<IList<IDisposable>>(),
                Key.Of<ChildDisposable>(),
                Key.Of<ParentDisposable>());
            var disposables = loc.Get<IList<IDisposable>>().ToList();
            var child = loc.Get<ChildDisposable>();
            var parent = loc.Get<ParentDisposable>();

            Assert.That(parent, Is.SameAs(child),
                "Aliased<ParentDisposable>() must be the same instance as ChildDisposable");
            Assert.That(disposables.Count, Is.EqualTo(1),
                "Auto() IList<IDisposable> must not list the same instance once per alias key");
            Assert.That(disposables[0], Is.SameAs(child));

            // Mirror MonoModule.OnDestroy: snapshot, reverse, dispose each entry.
            disposables.Reverse();
            foreach (var disposable in disposables)
                disposable.Dispose();

            Assert.That(child.DisposeCount, Is.EqualTo(1),
                "Dispose must run once; a second IList entry is a double-dispose");
        }

        [Test]
        public void RefDisposable_AppearsOnceInAutoIList()
        {
            var module = new InlineModule(m =>
            {
                m.Make<ChildDisposable>().From().Functoid(ChildDisposable.GetFunctoid());
                m.Make<ParentDisposable>().From().Ref<ChildDisposable>();
                m.Make<IList<IDisposable>>().From().Auto();
            });

            var loc = InjectorFor(module).Produce(
                Key.Of<IList<IDisposable>>(),
                Key.Of<ChildDisposable>(),
                Key.Of<ParentDisposable>());
            var disposables = loc.Get<IList<IDisposable>>().ToList();

            Assert.That(loc.Get<ParentDisposable>(), Is.SameAs(loc.Get<ChildDisposable>()));
            Assert.That(disposables.Count, Is.EqualTo(1),
                "Ref<ChildDisposable>() is Binding.ToKey, same double-collect as Aliased");
            Assert.That(disposables[0], Is.SameAs(loc.Get<ChildDisposable>()));

            disposables.Reverse();
            foreach (var disposable in disposables)
                disposable.Dispose();

            Assert.That(loc.Get<ChildDisposable>().DisposeCount, Is.EqualTo(1));
        }
    }
}
