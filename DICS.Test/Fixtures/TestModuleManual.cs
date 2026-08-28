using System.Collections.Generic;

namespace DICS.Test.Fixtures
{
    public class TestModuleManual : Module
    {
        public TestModuleManual(bool addCycles = false)
        {
            if (addCycles)
            {
                Make<int>().Named("xxx").From().Instance(1).AddDependency<int>("fake");
                Make<int>().Named("fake").From().Instance(42).AddDependency<int>("xxx");
            }
            else
            {
                Make<int>().Named("xxx").From().Instance(1);
                Make<int>().Named("fake").From().Instance(42);
            }

            Make<int>().Named("dup").From().Ref<int>("fake");

            Make<ISet<ITestSuper>>().Named("test-autoset").From().Auto();

            Make<ISet<string>>().Named("test-set")
                .Add().Instance("string1")
                .Add().Instance("string2");


            Make<ISet<string>>().Named("test-set-empty").From().Empty();

            Make<TestDep>()
                .From()
                .Instance(new TestDep())
                .Aliased<ITestDep>("testname")
                .Aliased<ITestSuper>()
                .AddToSetOf<ITestSuper>();

            Make<BehaviourCreator>().From().Instance(new BehaviourCreator());

            Make<TestClass>()
                .From()
                .Functoid(
                    IFunctoid.Lift(
                        (ITestDep a, int b, ITestSuper c, ISet<string> d, ISet<string> e,
                                MagicMutableDicsReference<LocatorMeta> f,
                                MagicMutableDicsReference<ILocator> g) =>
                            new TestClass(a, b, c, d, e, f, g),
                        new[] { "testname", "xxx", null, "test-set", "test-set-empty" }
                    )
                )
                .AddDependency<int>("fake");

            Make<IUnsafeFactory<TestClass4>>().Named("test-factory").From().UntypedFactory()
                .Using()
                .Functoid(
                    IFunctoid.Lift(
                        (TestClass a, int b) => new TestClass4(a, b))
                );

            Make<TestBehaviour>().From().Lifecycle(
                IFunctoid.Lift(
                    (BehaviourCreator c) =>
                        c.Create<TestBehaviour>()
                ),
                IInitializer.Lift(
                    (TestBehaviour c, Sig _, TestClass d) => c.Initialize(d)
                )
            );
        }
    }
}