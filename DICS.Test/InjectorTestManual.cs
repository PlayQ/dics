using System;
using System.Collections.Generic;
using DICS.Test.Fixtures;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Behaviour tests for the manually-wired module (no source generator).
    /// One test per behaviour; the underlying module is shared.
    /// </summary>
    public class InjectorTestManual
    {
        private Injector _injector = null!;
        private ILocator _loc = null!;

        [SetUp]
        public void SetUp()
        {
            var module = new TestModuleManual();
            _injector = new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty,
                module.Freeze());

            _loc = _injector.Produce(
                Key.Of<TestClass>(),
                Key.Of<IUnsafeFactory<TestClass4>>("test-factory"),
                Key.Of<TestBehaviour>(),
                Key.Of<ISet<ITestSuper>>(),
                Key.Of<ISet<ITestSuper>>("test-autoset"),
                Key.Of<int>("dup")
            );
        }

        // TestClass is constructed with its declared deps and the [Local] empty set is empty.
        [Test]
        public void TestClass_BuiltWithDeclaredDeps()
        {
            var instance = _loc.Get<TestClass>();
            Assert.That(instance.Arg, Is.EqualTo(1));
            Assert.That(instance.StringsEmpty, Is.Empty);
            Assert.That(instance.Strings.Count, Is.EqualTo(2));
        }

        // Make<int>().Named("dup").From().Ref<int>("fake") aliases one named key to another.
        [Test]
        public void RefBinding_AliasesAnotherKey()
        {
            Assert.That(_loc.HasLocally(Key.Of<int>("fake")), Is.True);
            Assert.That(_loc.Get<int>("fake"), Is.EqualTo(42));
            Assert.That(_loc.Get<int>("dup"), Is.EqualTo(42),
                "dup should resolve to the same value as fake via Ref<int>");
        }

        // TestBehaviour uses Lifecycle(extractor, initializer); both pieces ran.
        [Test]
        public void Lifecycle_ExtractorThenInitializer_ProducesInitializedInstance()
        {
            Assert.That(_loc.HasLocally(Key.Of<TestBehaviour>()), Is.True);
            var beh = _loc.Get<TestBehaviour>();
            Assert.That(beh, Is.Not.Null);
        }

        // Autoset bound via Auto() collects every binding whose ImplType is assignable to the
        // element type. Here that means the single ITestSuper binding (TestDep).
        [Test]
        public void Autoset_CollectsAssignableBindings()
        {
            var autoset = _loc.Get<ISet<ITestSuper>>("test-autoset");
            Assert.That(autoset.Count, Is.EqualTo(1));
        }

        // Untyped factory: caller supplies local args via an ILocator.
        [Test]
        public void UntypedFactory_AcceptsLocalArgs()
        {
            var fac = _loc.Get<IUnsafeFactory<TestClass4>>("test-factory");
            var local = new LocatorImpl(ILocator.Empty, "local",
                new Instance(Key.Of<int>(), 100));
            var built = fac.Make(local);
            Assert.That(built, Is.Not.Null);
        }

        // Locator inheritance: a sub-module sees the parent locator's bindings.
        [Test]
        public void SubInjector_InheritsParentLocator()
        {
            var subModule = new TesSubModule();
            var subInjector = new Injector(_loc, IDicsMeasurement.FromDefault(), "sub",
                subModule.Freeze());
            var subLoc = subInjector.Produce(Key.Of<TestClass2>());

            Assert.That(subLoc.Get<TestClass2>().Dep.Arg, Is.EqualTo(1));
        }

        // The "+cycle" mode of TestModuleManual sets up a direct cycle. Planner rejects.
        [Test]
        public void CycleInBindings_RejectedAtPlanTime()
        {
            var module = new TestModuleManual(addCycles: true);
            var injector = new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty,
                module.Freeze());

            Assert.Throws<DicsPlanningException>(() => injector.Plan(Key.Of<TestClass>()));
        }
    }
}
