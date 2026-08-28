using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Contract under test: traversing <c>IList&lt;IDisposable&gt;</c> the way
    /// <c>MonoModule.OnDestroy</c> does (snapshot, <c>Reverse()</c>, dispose each)
    /// must dispose in reverse-topological order — every dependent strictly before
    /// any of the dependencies it was constructed from.
    ///
    /// Taxonomy: Behavioral-Active × Blackbox × Group.
    /// </summary>
    public class DisposalOrderTest
    {
        /// <summary>Records its own disposal into a shared log.</summary>
        public class Tracked : IDisposable
        {
            private readonly List<string> _log;
            public readonly string Name;

            public Tracked(string name, List<string> log)
            {
                Name = name;
                _log = log;
            }

            public void Dispose() => _log.Add(Name);
            public override string ToString() => Name;
        }

        public class Dep : Tracked
        {
            public Dep(List<string> log) : base("dep", log) { }
        }

        public class Mid : Tracked
        {
            public readonly Dep Dep;

            public Mid(Dep dep, List<string> log) : base("mid", log) => Dep = dep;
        }

        public class Consumer : Tracked
        {
            public readonly Mid Mid;

            public Consumer(Mid mid, List<string> log) : base("consumer", log) => Mid = mid;
        }

        private static Injector InjectorFor(Module module) =>
            new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty, module.Freeze());

        /// <summary>Mirrors <c>MonoModule.OnDestroy</c>: snapshot, reverse, dispose each entry.</summary>
        private static void DisposeAll(ILocator loc)
        {
            var disposables = loc.Get<IList<IDisposable>>().ToList();
            disposables.Reverse();
            foreach (var disposable in disposables)
                disposable.Dispose();
        }

        private static ILocator ProduceChain(List<string> log, bool dependentsFirst)
        {
            var module = new InlineModule(m =>
            {
                m.Make<List<string>>().From().Instance(log);

                Action bindConsumer = () =>
                    m.Make<Consumer>().From()
                        .Functoid(IFunctoid.Lift((Mid mid, List<string> l) => new Consumer(mid, l)));
                Action bindMid = () =>
                    m.Make<Mid>().From()
                        .Functoid(IFunctoid.Lift((Dep dep, List<string> l) => new Mid(dep, l)));
                Action bindDep = () =>
                    m.Make<Dep>().From().Functoid(IFunctoid.Lift((List<string> l) => new Dep(l)));

                if (dependentsFirst)
                {
                    bindConsumer();
                    bindMid();
                    bindDep();
                }
                else
                {
                    bindDep();
                    bindMid();
                    bindConsumer();
                }

                m.Make<IList<IDisposable>>().From().Auto();
            });

            return InjectorFor(module).Produce(Key.Of<IList<IDisposable>>(), Key.Of<Consumer>());
        }

        // Control: module-binding order already coincides with topological order.
        [Test]
        public void DisposalIsReverseTopological_WhenBindingOrderMatchesTopology()
        {
            var log = new List<string>();
            DisposeAll(ProduceChain(log, dependentsFirst: false));

            Assert.That(log, Is.EqualTo(new[] { "consumer", "mid", "dep" }),
                "Each dependent must be disposed before the dependencies it holds");
        }

        // Same graph, bindings declared dependents-first. Disposal order is a property
        // of the dependency graph, not of the order the module happens to declare
        // bindings in, so the expected sequence is identical to the control.
        [Test]
        public void DisposalIsReverseTopological_WhenBindingOrderIsInverted()
        {
            var log = new List<string>();
            DisposeAll(ProduceChain(log, dependentsFirst: true));

            Assert.That(log, Is.EqualTo(new[] { "consumer", "mid", "dep" }),
                "Each dependent must be disposed before the dependencies it holds");
        }

        // Diamond: two independent dependents of one shared dependency. The shared
        // dependency must outlive both, regardless of binding order. The relative
        // order of the two unrelated dependents is unconstrained.
        public class Left : Tracked
        {
            public Left(Dep dep, List<string> log) : base("left", log) { }
        }

        public class Right : Tracked
        {
            public Right(Dep dep, List<string> log) : base("right", log) { }
        }

        [Test]
        public void SharedDependency_IsDisposedAfterAllOfItsDependents()
        {
            var log = new List<string>();
            var module = new InlineModule(m =>
            {
                m.Make<List<string>>().From().Instance(log);
                m.Make<Left>().From().Functoid(IFunctoid.Lift((Dep d, List<string> l) => new Left(d, l)));
                m.Make<Dep>().From().Functoid(IFunctoid.Lift((List<string> l) => new Dep(l)));
                m.Make<Right>().From().Functoid(IFunctoid.Lift((Dep d, List<string> l) => new Right(d, l)));
                m.Make<IList<IDisposable>>().From().Auto();
            });

            DisposeAll(InjectorFor(module).Produce(
                Key.Of<IList<IDisposable>>(), Key.Of<Left>(), Key.Of<Right>()));

            Assert.That(log, Has.Count.EqualTo(3));
            Assert.That(log.IndexOf("dep"), Is.EqualTo(2),
                "The shared dependency must be disposed last, after both dependents");
        }

        // The invariant restated against the thing it is actually derived from: the plan
        // materializes one instantiation order, the producer builds in it, the autoset
        // lists its elements in it, and disposal is its reverse. Asserted against
        // construction order observed at runtime, not against a re-derivation of it.
        [Test]
        public void SetOrderIsTheInstantiationOrder_AndDisposalIsItsReverse()
        {
            var constructed = new List<string>();
            var disposed = new List<string>();

            var module = new InlineModule(m =>
            {
                m.Make<List<string>>().Named("constructed").From().Instance(constructed);
                m.Make<List<string>>().Named("disposed").From().Instance(disposed);

                // Declared dependents-first, so binding order alone would be wrong.
                foreach (var name in new[] { "c", "b", "a" })
                {
                    var captured = name;
                    var binding = m.Make<Tracked>().Named(captured).From().Functoid(
                        IFunctoid.Lift((List<string> built, List<string> log) =>
                        {
                            built.Add(captured);
                            return new Tracked(captured, log);
                        }, new[] { "constructed", "disposed" }));

                    // a <- b <- c
                    if (captured == "c") binding.AddDependency<Tracked>("b");
                    if (captured == "b") binding.AddDependency<Tracked>("a");
                }

                m.Make<IList<IDisposable>>().From().Auto();
            });

            var injector = InjectorFor(module);
            var roots = new[]
            {
                Key.Of<IList<IDisposable>>(),
                Key.Of<Tracked>("a"), Key.Of<Tracked>("b"), Key.Of<Tracked>("c")
            };

            var plan = injector.Plan(roots);
            var loc = injector.Produce(plan);
            var listed = loc.Get<IList<IDisposable>>().Cast<Tracked>().Select(t => t.Name).ToList();

            Assert.That(constructed, Is.EqualTo(new[] { "a", "b", "c" }),
                "The producer instantiates dependencies before dependents");
            Assert.That(listed, Is.EqualTo(constructed),
                "The autoset lists its elements in the order they were instantiated");
            Assert.That(
                plan.InstantiationOrder.Where(k => k.Tpe == typeof(Tracked)).Select(k => k.Name),
                Is.EqualTo(constructed),
                "Runtime construction order is the plan's materialized order, not a re-derivation");

            DisposeAll(loc);

            var reversed = new List<string>(constructed);
            reversed.Reverse();
            Assert.That(disposed, Is.EqualTo(reversed),
                "Disposal is the reverse of the instantiation order");
        }

        // The async producer schedules concurrently instead of walking
        // Plan.InstantiationOrder, so the invariant has to be asserted separately there.
        // The set is still listed in the plan's order, so its reverse stays a safe
        // teardown order regardless of how the tasks interleaved.
        [Test]
        public async Task AsyncProduce_DisposalIsStillReverseTopological()
        {
            var log = new List<string>();
            var module = new InlineModule(m =>
            {
                m.Make<List<string>>().From().Instance(log);
                m.Make<Consumer>().From()
                    .Functoid(IFunctoid.Lift((Mid mid, List<string> l) => new Consumer(mid, l)));
                m.Make<Mid>().From()
                    .Functoid(IFunctoid.Lift((Dep dep, List<string> l) => new Mid(dep, l)));
                m.Make<Dep>().From().Functoid(IFunctoid.Lift((List<string> l) => new Dep(l)));
                m.Make<IList<IDisposable>>().From().Auto();
            });

            var loc = await InjectorFor(module)
                .ProduceAsync(CancellationToken.None, Key.Of<IList<IDisposable>>(), Key.Of<Consumer>());

            DisposeAll(loc);

            Assert.That(log, Is.EqualTo(new[] { "consumer", "mid", "dep" }));
        }

        // DefaultDicsMeasurement has recorded the producer's actual instantiation order
        // since a852ea0 ("instantiation order", 2025-01-21) — first on LocatorMeta, today
        // as DefaultDicsMeasurement.InstantiationOrder, consumed by TraceGen. It observes
        // the traversal after the fact; Plan.InstantiationOrder now drives it. Pin the two
        // together so a change to either cannot silently make the profile lie.
        [Test]
        public void MeasuredOrder_EqualsThePlannedOrder()
        {
            var log = new List<string>();
            var module = new InlineModule(m =>
            {
                m.Make<List<string>>().From().Instance(log);
                m.Make<Consumer>().From()
                    .Functoid(IFunctoid.Lift((Mid mid, List<string> l) => new Consumer(mid, l)));
                m.Make<Mid>().From()
                    .Functoid(IFunctoid.Lift((Dep dep, List<string> l) => new Mid(dep, l)));
                m.Make<Dep>().From().Functoid(IFunctoid.Lift((List<string> l) => new Dep(l)));
                m.Make<IList<IDisposable>>().From().Auto();
            });

            var measurement = IDicsMeasurement.FromDefault();
            var injector = new Injector(ILocator.Empty, measurement, string.Empty, module.Freeze());
            var plan = injector.Plan(Key.Of<IList<IDisposable>>(), Key.Of<Consumer>());
            injector.Produce(plan);

            Assert.That(measurement.InstantiationOrder, Is.EqualTo(plan.InstantiationOrder),
                "What the producer actually did must equal what the plan prescribed");
        }

        // Ordering-only edge declared via AddDependency<U>() carries no constructor
        // argument, so the dependency graph is the only source of truth here. Declared
        // dependent-first on purpose: module order alone would invert the disposal.
        [Test]
        public void AddDependencyEdge_ConstrainsDisposalOrder()
        {
            var log = new List<string>();
            var module = new InlineModule(m =>
            {
                m.Make<Tracked>().Named("b").From().Instance(new Tracked("b", log))
                    .AddDependency<Tracked>("a");
                m.Make<Tracked>().Named("a").From().Instance(new Tracked("a", log));
                m.Make<IList<IDisposable>>().From().Auto();
            });

            DisposeAll(InjectorFor(module).Produce(
                Key.Of<IList<IDisposable>>(), Key.Of<Tracked>("a"), Key.Of<Tracked>("b")));

            Assert.That(log, Is.EqualTo(new[] { "b", "a" }),
                "An AddDependency edge orders disposal even with no constructor dependency");
        }

        // The ordering guarantee is a partial order: elements with no dependency
        // relation must keep module-binding order, so a module can still choose the
        // disposal order of unrelated components. Complements
        // AutosetCollectionTest.IListAuto_PreservesModuleBindingOrder, which asserts
        // the same for the list itself rather than for the disposal sequence.
        [Test]
        public void UnrelatedElements_KeepReversedModuleBindingOrder()
        {
            var log = new List<string>();
            var module = new InlineModule(m =>
            {
                m.Make<Tracked>().Named("a").From().Instance(new Tracked("a", log));
                m.Make<Tracked>().Named("b").From().Instance(new Tracked("b", log));
                m.Make<Tracked>().Named("c").From().Instance(new Tracked("c", log));
                m.Make<IList<IDisposable>>().From().Auto();
            });

            DisposeAll(InjectorFor(module).Produce(
                Key.Of<IList<IDisposable>>(),
                Key.Of<Tracked>("a"), Key.Of<Tracked>("b"), Key.Of<Tracked>("c")));

            Assert.That(log, Is.EqualTo(new[] { "c", "b", "a" }));
        }

        // Every element of a longer chain, disposed once and in order. Guards against a
        // topological rewrite that drops or duplicates elements.
        [Test]
        public void EveryElementOfAChain_IsDisposedExactlyOnce()
        {
            var log = new List<string>();
            var loc = ProduceChain(log, dependentsFirst: true);
            var listed = loc.Get<IList<IDisposable>>();

            DisposeAll(loc);

            Assert.That(listed.Count, Is.EqualTo(3),
                "dep, mid and consumer are collected; the shared log is not IDisposable");
            Assert.That(log, Is.EqualTo(new[] { "consumer", "mid", "dep" }));
        }

    }
}
