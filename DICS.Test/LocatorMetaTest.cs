using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Exercises the always-available <see cref="LocatorMeta"/> binding and the
    /// <see cref="MagicMutableDicsReference{T}"/> back-fill performed by the Producer
    /// for <c>LocatorMeta</c> and <c>ILocator</c>.
    /// </summary>
    public class LocatorMetaTest
    {
        private static Injector NewInjector(Module module) =>
            new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty, module.Freeze());

        // LocatorMeta is added to every produced locator; it carries the originating Plan by ref equality.
        [Test]
        public void Producer_AlwaysEmits_LocatorMeta_WithOriginatingPlan()
        {
            var module = new InlineModule(m => m.Make<int>().From().Instance(7));

            var injector = NewInjector(module);
            var plan = injector.Plan(Key.Of<int>());
            var loc = injector.Produce(plan);

            Assert.That(loc.HasLocally(Key.Of<LocatorMeta>()), Is.True,
                "LocatorMeta must be present in every produced locator");

            var meta = loc.Get<LocatorMeta>();
            Assert.That(meta.Plan, Is.SameAs(plan),
                "LocatorMeta.Plan should ref-equal the Plan that was produced");
            Assert.That(meta.DicsMeasurement, Is.Not.Null);
        }

        // MagicMutableDicsReference<LocatorMeta> is a magic import: bind it as an import in a
        // module and the producer back-fills it with the LocatorMeta after production.
        [Test]
        public void MagicMutableReference_ToLocatorMeta_IsBackFilledByProducer()
        {
            var module = new InlineModule(m =>
            {
                m.Make<MagicMutableDicsReference<LocatorMeta>>().From().Import();
                m.Make<int>().From().Functoid(
                    IFunctoid.Lift((MagicMutableDicsReference<LocatorMeta> r) =>
                    {
                        // Inside the functoid we can capture the reference, but it is not yet
                        // populated; Get() would throw. The producer populates it after all
                        // functoids have run.
                        return 1;
                    })
                );
            });

            var loc = NewInjector(module).Produce(Key.Of<int>());
            var reference = loc.Get<MagicMutableDicsReference<LocatorMeta>>();

            var meta = reference.Get();
            Assert.That(meta, Is.Not.Null);
            Assert.That(meta!.Plan, Is.Not.Null);
        }

        // MagicMutableDicsReference<ILocator> resolves to the OWNING locator (not a parent).
        // The reference must be reachable from some root for the planner to include it.
        [Test]
        public void MagicMutableReference_ToILocator_IsBackFilledWithOwningLocator()
        {
            var module = new InlineModule(m =>
            {
                m.Make<MagicMutableDicsReference<ILocator>>().From().Import();
                m.Make<int>().From().Functoid(
                    IFunctoid.Lift((MagicMutableDicsReference<ILocator> r) => 1)
                );
            });

            var loc = NewInjector(module).Produce(Key.Of<int>());
            var reference = loc.Get<MagicMutableDicsReference<ILocator>>();

            var resolved = reference.Get();
            Assert.That(resolved, Is.SameAs(loc),
                "Back-filled ILocator should be the locator that was produced");
        }

        // Two separate produces — on the SAME injector — give two LocatorMetas, each
        // pointing at its own Plan. Regression test for a defect in
        // DefaultDicsMeasurement.Start(Key) which used Dictionary.Add and threw on the
        // second produce due to duplicate-key insertion.
        [Test]
        public void EachProduction_GetsItsOwn_LocatorMeta_SameInjector()
        {
            var module = new InlineModule(m => m.Make<int>().From().Instance(1));
            var injector = NewInjector(module);

            var planA = injector.Plan(Key.Of<int>());
            var planB = injector.Plan(Key.Of<int>());

            var locA = injector.Produce(planA);
            var locB = injector.Produce(planB);

            Assert.That(locA.Get<LocatorMeta>().Plan, Is.SameAs(planA));
            Assert.That(locB.Get<LocatorMeta>().Plan, Is.SameAs(planB));
            Assert.That(locA.Get<LocatorMeta>(), Is.Not.SameAs(locB.Get<LocatorMeta>()));
        }

        // Repeated Produce on the same injector accumulates per-key timings in the
        // measurement instead of throwing.
        [Test]
        public void RepeatedProduce_AccumulatesMeasurements()
        {
            var module = new InlineModule(m => m.Make<int>().From().Instance(1));
            var measurement = new DefaultDicsMeasurement();
            var injector = new Injector(ILocator.Empty, measurement, "rep", module.Freeze());

            Assert.DoesNotThrow(() =>
            {
                injector.Produce(Key.Of<int>());
                injector.Produce(Key.Of<int>());
                injector.Produce(Key.Of<int>());
            });

            // Per-key timings exist; the int key was observed during PostProduce dispatch.
            Assert.That(measurement.Timings.Count, Is.GreaterThan(0),
                "Measurement should have recorded at least one per-key timing");
        }
    }

}
