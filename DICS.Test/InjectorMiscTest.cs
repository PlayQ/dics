using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Smoke/contract coverage for under-tested public API surfaces:
    /// <see cref="Injector.RootCandidates"/>, <see cref="IDicsMeasurement"/> call order,
    /// and the cross-injector plan-use guard.
    /// </summary>
    public class InjectorMiscTest
    {
        public class A
        {
            public B Dep;
            public A(B dep) { Dep = dep; }
        }
        public class B { }
        public class C { }

        private static Injector NewInjector(Module module) =>
            new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty, module.Freeze());

        // RootCandidates returns the keys nothing else references. Three bindings:
        // A depends on B; C alone. References ⊇ {B}. So candidates = {A, C}.
        [Test]
        public void RootCandidates_ReturnsKeysWithNoIncomingEdges()
        {
            var module = new InlineModule(m =>
            {
                m.Make<B>().From().Instance(new B());
                m.Make<A>().From().Functoid(IFunctoid.Lift((B b) => new A(b)));
                m.Make<C>().From().Instance(new C());
            });

            var roots = NewInjector(module).RootCandidates();
            Assert.That(roots, Contains.Item(Key.Of<A>()));
            Assert.That(roots, Contains.Item(Key.Of<C>()));
            Assert.That(roots, Does.Not.Contain(Key.Of<B>()),
                "B is referenced by A; it must not appear in the root-candidate set");
        }

        // Custom IDicsMeasurement records the sequence of calls.
        // Observed contract from Producer.Produce / Injector.Produce:
        //   - StartTotal is opened first (Injector.Produce sets up the outer using-handler).
        //   - Inside, Planner.Plan calls Start("Plan").
        //   - Then Producer.Produce calls Start("PreProduce"), and during DoProduce calls
        //     Start(key) for each key, plus Start("FilterReadyKeys") and Start("PostProduce").
        //   - Finally, StartTotal handle is disposed.
        //
        // The test fixes the bracketing: first event is "StartTotal", last event is
        // "DisposeTotal"; per-key Start(Key) handles are disposed before the total disposes;
        // and every Start has a matching Dispose recorded in LIFO-compatible order (no
        // leak, no double-dispose). Doc-bug discoveries are surfaced via Surprises in the
        // task report rather than altering the assertions.
        [Test]
        public void Measurement_CallOrder_BracketsStartTotalAroundAllSpans()
        {
            var module = new InlineModule(m =>
                m.Make<int>().From().Instance(7));

            var rec = new RecordingMeasurementForOrder();
            var injector = new Injector(ILocator.Empty, rec, "order", module.Freeze());
            injector.Produce(Key.Of<int>());

            var events = rec.Events;

            Assert.That(events.Count, Is.GreaterThan(0));
            Assert.That(events[0], Is.EqualTo("Start:Total"),
                "StartTotal must be the first event");
            Assert.That(events[events.Count - 1], Is.EqualTo("Dispose:Total"),
                "StartTotal.Dispose must be the last event");

            // Every Start is matched by a Dispose at some later index.
            var openStarts = new Stack<string>();
            foreach (var ev in events)
            {
                if (ev.StartsWith("Start:")) openStarts.Push(ev.Substring("Start:".Length));
                else if (ev.StartsWith("Dispose:"))
                {
                    var name = ev.Substring("Dispose:".Length);
                    Assert.That(openStarts.Count, Is.GreaterThan(0),
                        $"Dispose:{name} without an open Start");
                    var top = openStarts.Pop();
                    Assert.That(top, Is.EqualTo(name),
                        $"Dispose:{name} must close the most recently opened Start, not Start:{top} (LIFO bracketing)");
                }
            }
            Assert.That(openStarts.Count, Is.EqualTo(0),
                "Every Start must have a matching Dispose");

            // At least one Start(Key) was recorded for the produced int key.
            Assert.That(events, Has.Some.EqualTo($"Start:Key({Key.Of<int>()})"),
                "Per-key Start(Key) must be invoked at least once during Produce");
        }

        // Cross-injector plan-use guard. Producer.Produce throws DicsBug when
        // a plan made by injector A is fed to injector B. The check fires in both Debug
        // and Release configurations (the former Debug.Assert was promoted to a
        // runtime throw).
        [Test]
        public void Produce_RejectsPlanFromOtherInjector()
        {
            var module = new InlineModule(m =>
                m.Make<int>().From().Instance(1));

            var injectorA = NewInjector(module);
            var injectorB = NewInjector(module);

            var planA = injectorA.Plan(Key.Of<int>());

            Assert.Throws<DicsBug>(() => injectorB.Produce(planA),
                "Producer must reject a foreign plan with DicsBug under all configurations");
        }
    }

    /// <summary>
    /// Records every Start / Dispose call as a flat event stream, preserving the order
    /// in which the producer opens and closes timing spans.
    /// </summary>
    internal sealed class RecordingMeasurementForOrder : IDicsMeasurement
    {
        public List<string> Events { get; } = new();

        public IDisposable StartTotal()
        {
            Events.Add("Start:Total");
            return new EventDisposable(this, "Total");
        }

        public IDisposable Start(Key key)
        {
            var label = $"Key({key})";
            Events.Add($"Start:{label}");
            return new EventDisposable(this, label);
        }

        public IDisposable Start(string key)
        {
            Events.Add($"Start:{key}");
            return new EventDisposable(this, key);
        }

        public string PlanToString(Plan plan) => string.Empty;
        public string PlanToTrace(Plan plan) => string.Empty;

        private sealed class EventDisposable : IDisposable
        {
            private readonly RecordingMeasurementForOrder _owner;
            private readonly string _label;
            public EventDisposable(RecordingMeasurementForOrder owner, string label)
            {
                _owner = owner;
                _label = label;
            }
            public void Dispose() => _owner.Events.Add($"Dispose:{_label}");
        }
    }
}
