using System.Collections.Generic;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Exercises the planner's rejection contract. Every test here asserts that planning
    /// (or, where applicable, production) fails for the documented reason. None of these
    /// should ever succeed silently.
    /// </summary>
    public class PlanningFailureTest
    {
        private static Injector NewInjector(Module module) =>
            new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty, module.Freeze());

        // A root key with no binding and no parent-locator import: the plan is built but
        // the producer fails when it tries to import the key from an empty parent.
        [Test]
        public void Producing_RootWithNoBinding_FailsWithProducerException()
        {
            var module = new InlineModule(_ => { /* nothing bound */ });
            var injector = NewInjector(module);

            Assert.Throws<DicsProducerException>(() => injector.Produce(Key.Of<int>("unbound")));
        }

        // A functoid depends on a key that is neither bound nor importable from the
        // parent locator. Production fails when the import is attempted.
        [Test]
        public void Producing_FunctoidWithUnsatisfiedDep_FailsWithProducerException()
        {
            var module = new InlineModule(m =>
                m.Make<string>().From().Functoid(
                    IFunctoid.Lift((int i) => i.ToString())
                )
            );
            var injector = NewInjector(module);

            Assert.Throws<DicsProducerException>(() => injector.Produce(Key.Of<string>()));
        }

        // Cycle: A depends on B, B depends on A. Detected at planning time.
        [Test]
        public void Planning_DirectCycle_RaisesPlanningException()
        {
            var module = new InlineModule(m =>
            {
                m.Make<int>().Named("a").From().Instance(0).AddDependency<int>("b");
                m.Make<int>().Named("b").From().Instance(0).AddDependency<int>("a");
            });
            var injector = NewInjector(module);

            Assert.Throws<DicsPlanningException>(() => injector.Plan(Key.Of<int>("a")));
        }

        // Longer cycle: A -> B -> C -> A.
        [Test]
        public void Planning_TransitiveCycle_RaisesPlanningException()
        {
            var module = new InlineModule(m =>
            {
                m.Make<int>().Named("a").From().Instance(0).AddDependency<int>("b");
                m.Make<int>().Named("b").From().Instance(0).AddDependency<int>("c");
                m.Make<int>().Named("c").From().Instance(0).AddDependency<int>("a");
            });
            var injector = NewInjector(module);

            Assert.Throws<DicsPlanningException>(() => injector.Plan(Key.Of<int>("a")));
        }

        // Two bindings on the same key, gated on the SAME axis but with different points.
        // Selecting neither point should leave both candidates in play → planner rejects.
        [Test]
        public void Planning_TwoBindingsOnSameAxisNoSelection_RaisesPlanningException()
        {
            var module = new InlineModule(m =>
            {
                m.Make<string>().In(EnvPoint.Dev).From().Instance("dev");
                m.Make<string>().In(EnvPoint.Prod).From().Instance("prod");
            });
            var injector = NewInjector(module);

            Assert.Throws<DicsPlanningException>(() =>
                injector.Plan(new HashSet<Key> { Key.Of<string>() }, new HashSet<IAxisPoint>()));
        }

        // Two points of the SAME axis declared in config: ValidatePoints rejects.
        [Test]
        public void Planning_ConflictingAxisPointsInConfig_RaisesPlanningException()
        {
            var module = new InlineModule(m => m.Make<string>().From().Instance("x"));
            var injector = NewInjector(module);

            Assert.Throws<DicsPlanningException>(() =>
                injector.Plan(new HashSet<Key> { Key.Of<string>() },
                              new HashSet<IAxisPoint> { EnvPoint.Dev, EnvPoint.Prod }));
        }

        // Three bindings on the same key, each gated on a distinct point of one axis.
        // Selecting exactly one point must yield the value bound to that point.
        // Reproduces the defect where Resolve returned value.First() instead of
        // filtered.First() after axis filtering, making selection non-deterministic.
        [Test]
        public void Planning_AxisSelection_PicksBindingMatchingActivePoint()
        {
            var module = new InlineModule(m =>
            {
                m.Make<string>().In(EnvPoint.Dev).From().Instance("dev-value");
                m.Make<string>().In(EnvPoint.Prod).From().Instance("prod-value");
                m.Make<string>().In(EnvPoint.Staging).From().Instance("staging-value");
            });
            var injector = NewInjector(module);

            var plan = injector.Plan(
                new HashSet<Key> { Key.Of<string>() },
                new HashSet<IAxisPoint> { EnvPoint.Staging });
            var locator = injector.Produce(plan);

            Assert.That(locator.Get<string>(), Is.EqualTo("staging-value"));
        }
    }

    internal record EnvPoint(string Point) : IAxisPoint
    {
        public string AxisName() => "Env";
        public string PointName() => Point;

        public static readonly EnvPoint Dev = new("Dev");
        public static readonly EnvPoint Prod = new("Prod");
        public static readonly EnvPoint Staging = new("Staging");
    }

}
