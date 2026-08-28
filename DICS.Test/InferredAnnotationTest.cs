using DICS.Test.Fixtures;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Exercises the annotation-omission heuristic: presence of [Inject] alone implies
    /// [LiftInitializer]; presence of [Local] alone implies [LiftFactory]; both can
    /// coexist on the same class.
    /// </summary>
    public class InferredAnnotationTest
    {
        private static Injector NewInjector(Module module) =>
            new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty, module.Freeze());

        // 1. A class with only [Inject] members (no [LiftInitializer]) still gets
        //    a generated LiftInitializer() and can be bound through .Lifecycle(...).
        [Test]
        public void InferredLiftInitializer_Works()
        {
            var dep = new Fixtures.TestDep();
            var module = new InlineModule(m =>
            {
                m.Make<ITestSuper>().From().Instance(dep);
                m.Make<System.Collections.Generic.ISet<string>>().Named("test-set")
                    .Add().Instance("alpha").Add().Instance("beta");

                m.Make<InferredInitializerOnly>().From().Lifecycle(
                    IFunctoid.Lift(() => new InferredInitializerOnly()),
                    InferredInitializerOnly.LiftInitializer()
                );
            });

            var loc = NewInjector(module).Produce(Key.Of<InferredInitializerOnly>());
            var instance = loc.Get<InferredInitializerOnly>();

            Assert.That(instance.GetDep(), Is.SameAs(dep));
            Assert.That(instance.StringCount(), Is.EqualTo(2));
        }

        // 2. A record with only [Local] on a primary-constructor parameter (no
        //    [LiftFactory]) still gets a generated FactoryFunctoid.
        [Test]
        public void InferredLiftFactory_Works()
        {
            var dep = new Fixtures.TestDep();
            var module = new InlineModule(m =>
            {
                m.Make<ITestSuper>().From().Instance(dep);

                m.Make<InferredFactoryRecord.Factory>().From().TypedFactory().Using()
                    .Functoid(InferredFactoryRecord.LiftFactoryFunctoid());
            });

            var loc = NewInjector(module).Produce(Key.Of<InferredFactoryRecord.Factory>());
            var factory = loc.Get<InferredFactoryRecord.Factory>();
            var made = factory.Create(7);

            Assert.That(made.Dep, Is.SameAs(dep));
            Assert.That(made.Number, Is.EqualTo(7));
        }

        // 3. A class with both [Inject] and [Local] members and no class-level Lift
        //    attribute gets both LiftInitializer() and a Factory.
        [Test]
        public void Inferred_BothInitializerAndFactory_Coexist()
        {
            var dep = new Fixtures.TestDep();
            var module = new InlineModule(m =>
            {
                m.Make<ITestSuper>().From().Instance(dep);

                m.Make<InferredInitializerAndFactory>().From().Lifecycle(
                    IFunctoid.Lift(() => new InferredInitializerAndFactory()),
                    InferredInitializerAndFactory.LiftInitializer()
                );
            });

            // Direct binding path (LiftInitializer).
            var loc = NewInjector(module).Produce(Key.Of<InferredInitializerAndFactory>());
            var direct = loc.Get<InferredInitializerAndFactory>();
            Assert.That(direct.GetDep(), Is.SameAs(dep));

            // Factory path on the same class.
            var factoryModule = new InlineModule(m =>
            {
                m.Make<ITestSuper>().From().Instance(dep);
                m.Make<InferredInitializerAndFactory.Factory>().From().TypedFactory().Using()
                    .Lifecycle(
                        IFunctoid.Lift(() => new InferredInitializerAndFactory()),
                        InferredInitializerAndFactory.LiftFactoryFunctoid()
                    );
            });
            var floc = NewInjector(factoryModule).Produce(Key.Of<InferredInitializerAndFactory.Factory>());
            var built = floc.Get<InferredInitializerAndFactory.Factory>().Create(42);
            Assert.That(built.GetDep(), Is.SameAs(dep));
            Assert.That(built.GetNumber(), Is.EqualTo(42));
        }
    }

}
