using System.Collections.Generic;
using DICS.Test.Fixtures;
using DICS.Tools;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Behaviour tests for the generator-driven module. The module is constructed and
    /// produced once per test; assertions are split per behaviour so failures point at
    /// the specific subsystem that regressed.
    /// </summary>
    public class InjectorTestAuto
    {
        private IDicsMeasurement _measurements = null!;
        private Injector _injector = null!;
        private ILocator _loc = null!;

        [SetUp]
        public void SetUp()
        {
            var module = new TestModuleAuto().Freeze();
            _measurements = IDicsMeasurement.FromDefault();
            _injector = new Injector(ILocator.Empty, _measurements, string.Empty, module);

            _loc = _injector.Produce(
                Key.Of<TestClass>(),
                Key.Of<IUnsafeFactory<TestClass4>>("test-factory"),
                Key.Of<TestBehaviour>(),
                Key.Of<ISet<ITestSuper>>(),
                Key.Of<ISet<ITestSuper>>("test-autoset"),
                Key.Of<IUnsafeFactory<TestClass5>>(),
                Key.Of<SomeSub>(),
                Key.Of<string>("test-private"),
                Key.Of<TestGenFac.Factory>(),
                Key.Of<TestGenInit.Factory>()
            );
        }

        // [Inject] fields including inherited ones (SomeSuper -> SomeSub) are resolved.
        [Test]
        public void Initializer_InjectsInheritedFields()
        {
            var ss = _loc.Get<SomeSub>();
            Assert.That(ss.DumpSuper(), Is.EqualTo("test:test:test"),
                "SomeSuper's three [Inject] string fields should be resolved");
            Assert.That(ss.DumpSub(), Is.EqualTo("43:44"),
                "SomeSub's two int fields should be resolved with named + default keys");
        }

        // Generator-emitted typed factory using FactoryKind.Constructor.
        [Test]
        public void TypedFactory_Constructor_CreatesInstanceWithLocalArg()
        {
            var factory = _loc.Get<TestGenFac.Factory>();
            var instance = factory.Create(99);
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.Arg, Is.EqualTo(99),
                "[Local] int Arg should be passed through Create(99)");
            // Dep2 is locator-resolved (not [Local]); TestModuleAuto binds ITestSuper to
            // the singleton TestDep instance via .Aliased<ITestSuper>(), so the factory
            // must hand back the same instance the parent locator holds.
            Assert.That(instance.Dep2, Is.Not.Null,
                "Locator-resolved Dep2 must not be null");
            Assert.That(instance.Dep2, Is.SameAs(_loc.Get<ITestSuper>()),
                "Dep2 must be the same instance the parent locator binds for ITestSuper");
        }

        // Generator-emitted typed factory using FactoryKind.Initializer + parent linkage.
        [Test]
        public void TypedFactory_Initializer_InjectsLocalsAndKeepsParentDepLink()
        {
            var factory = _loc.Get<TestGenInit.Factory>();
            var instance = factory.Create("bullshit", 265);
            Assert.That(instance.Dump(), Is.EqualTo("bullshit:265"),
                "Create(\"bullshit\", 265) should populate both [Local] fields");
            Assert.That(instance.DepCorrect(), Is.True,
                "Locator-driven Dep2 must equal the inherited Dep1");
        }

        // .Private() bindings are visible to direct holders but hidden from child locators.
        [Test]
        public void PrivateBinding_VisibleLocally_HiddenInChildLocator()
        {
            Assert.That(_loc.Resolve<string>("test-private"), Is.EqualTo("xxx"));

            var child = _loc.Inherited();
            Assert.That(child.Has(Key.Of<string>("test-private")), Is.False,
                "Private bindings must not leak into child locators");
        }

        // TestClass is fully wired and its [Local]-equivalent (empty set) is empty.
        [Test]
        public void TestClass_AllArgsResolved()
        {
            var instance = _loc.Get<TestClass>();
            Assert.That(instance.Arg, Is.EqualTo(1));
            Assert.That(instance.StringsEmpty, Is.Empty,
                "An empty set binding should resolve to an empty set, not a missing key");
        }

        // Untyped factory: caller supplies local args via an ILocator.
        [Test]
        public void UntypedFactory_AcceptsLocalArgsViaLocator()
        {
            var factory = _loc.Get<IUnsafeFactory<TestClass5>>();
            var localArgs = new LocatorImpl(ILocator.Empty, string.Empty,
                new Instance(Key.Of<int>(), 100));
            var instance = factory.Make(localArgs);
            Assert.That(instance, Is.Not.Null);
        }

        // PlanToTrace returns a chrome-trace JSON string directly; assert on it without a
        // disk round-trip.
        [Test]
        public void Measurement_PlanToTrace_ProducesValidJson()
        {
            var meta = _loc.Get<LocatorMeta>();
            var content = _measurements.PlanToTrace(meta.Plan);

            Assert.That(content, Does.StartWith("[").Or.StartWith("{"),
                "PlanToTrace should produce a JSON document");
            Assert.That(content.Length, Is.GreaterThan(2),
                "Trace document should contain at least one event");
        }
    }
}
