using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Regression tests for three defects:
    ///   - Injector.Extend silently dropped the parent module.
    ///   - Synthetic set-element key UID was derived from RuntimeHelpers.GetHashCode
    ///     which is not collision-free; replaced with a per-builder monotonic counter.
    ///   - DefaultDicsMeasurement used non-concurrent dictionaries and a bare List,
    ///     and so threw under parallel ProduceAsync.
    /// </summary>
    public class MiscRegressionTest
    {
        // Injector.Extend --------------------------------------------------------

        private class ExtendBaseModule : Module
        {
            public ExtendBaseModule()
            {
                // Parent module contributes the int=5 binding.
                Make<int>().From().Instance(5);
                // A binding that *depends* on the int binding, to prove parent
                // bindings remain reachable from the child injector.
                Make<string>().From().Functoid(IFunctoid.Lift((int n) => $"v={n}"));
            }
        }

        [Test]
        public void Extend_PreservesParentModuleBindings()
        {
            var parent = new Injector(
                ILocator.Empty, IDicsMeasurement.FromDefault(), "parent",
                new ExtendBaseModule().Freeze());

            // Child injector adds a runtime instance only; module-level bindings
            // (int=5, and string dependent on int) must remain available.
            var child = parent.Extend("child",
                new Instance(Key.Of<bool>(), true));

            var loc = child.Produce(Key.Of<int>(), Key.Of<string>(), Key.Of<bool>());

            Assert.That(loc.Get<int>(), Is.EqualTo(5),
                "Parent module's int binding must be visible through the child injector");
            Assert.That(loc.Get<string>(), Is.EqualTo("v=5"),
                "Parent module's string functoid (which depends on int) must resolve in the child");
            Assert.That(loc.Get<bool>(), Is.True,
                "Extension instance must also resolve");
        }

        // Synthetic set-element keys ---------------------------------------------

        private class LargeSetModule : Module
        {
            public LargeSetModule(int n)
            {
                var head = Make<ISet<int>>().From().Add();
                for (var i = 0; i < n; i++)
                {
                    head = head.Instance(i).Add();
                }
            }
        }

        [Test]
        public void LargeInstanceSet_HasDistinctSyntheticElementKeys()
        {
            const int n = 60;
            var module = new LargeSetModule(n).Freeze();

            // Pull out the synthetic per-element ToInstance bindings — those are
            // the ones whose Key is parameterised by the UID and parented by the
            // set key. Their names are what must not collide.
            var setKey = Key.Of<ISet<int>>();
            var elementInstanceKeys = module.Bindings
                .OfType<Binding.ToInstance>()
                .Select(b => b.Key)
                .Where(k => k.Prefix == setKey && k.Tpe == typeof(int))
                .ToList();

            Assert.That(elementInstanceKeys.Count, Is.EqualTo(n),
                $"Expected {n} synthetic element-instance bindings, got {elementInstanceKeys.Count}");

            var names = elementInstanceKeys.Select(k => k.Name).ToList();
            var unique = new HashSet<string?>(names);

            Assert.That(unique.Count, Is.EqualTo(names.Count),
                "Synthetic per-element key names must all be distinct; a duplicate would cause Freeze/Plan to throw 'Key already present'");
        }

        // Measurement under parallel produce -------------------------------------

        private class ParallelAsyncModule : Module
        {
            public ParallelAsyncModule(int n)
            {
                for (var i = 0; i < n; i++)
                {
                    var captured = i;
                    Make<int>().Named($"slot{captured}").From().AsyncFunctoid(
                        IAsyncFunctoid.Lift(async (System.Threading.CancellationToken ct) =>
                        {
                            await Task.Yield();
                            await Task.Delay(2, ct).ConfigureAwait(false);
                            return captured;
                        }));
                }
            }
        }

        [Test]
        public async Task DefaultMeasurement_SurvivesParallelAsyncProduce()
        {
            const int n = 10;
            var measurement = new DefaultDicsMeasurement();
            var injector = new Injector(
                ILocator.Empty, measurement, "parallel",
                new ParallelAsyncModule(n).Freeze());

            var roots = Enumerable.Range(0, n)
                .Select(i => Key.Of<int>($"slot{i}"))
                .ToHashSet<Key>();

            var loc = await injector.ProduceAsync(roots, new HashSet<IAxisPoint>())
                .ConfigureAwait(false);

            // All slots produced their distinct values.
            for (var i = 0; i < n; i++)
            {
                Assert.That(loc.Get<int>($"slot{i}"), Is.EqualTo(i));
            }

            // And per-key timings were recorded — at minimum the n slot keys.
            Assert.That(measurement.Timings.Count, Is.GreaterThanOrEqualTo(n),
                "Per-key timings should be recorded for each parallel slot");
        }
    }
}
