using System.Collections.Generic;

namespace DICS.Test.Fixtures
{
    public class TestModuleAuto : Module
    {
        public TestModuleAuto(bool addCycles = false)
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

            Make<ISet<ITestSuper>>().Named("test-autoset").From().Auto();
            Make<ISet<ITestSuper>>().From().Auto();

            Make<IList<ITestSuper>>().Named("test-autolist").From().Auto();
            Make<IList<ITestSuper>>().From().Auto();

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
                    TestClass.Lift()
                        .AddFakeDependencies(Key.Of<int>("fake"))
                );

            Make<IUnsafeFactory<TestClass4>>().Named("test-factory")
                .From()
                .UntypedFactory()
                .Using()
                .Functoid(
                    TestClass4.Lift()
                );

            Make<TestBehaviour>().From().Lifecycle(
                IFunctoid.Lift(
                    (BehaviourCreator c) =>
                        c.Create<TestBehaviour>()
                ),
                TestBehaviour.LiftInitializer()
            );


            Make<IUnsafeFactory<TestClass5>>().From()
                .UntypedFactory()
                .Using()
                .Lifecycle(
                    IFunctoid.Lift(() => new TestClass5()),
                    TestClass5.LiftInitializer()
                );


            Make<IFoo>().From().Functoid(MyFoo.Lift());
            Make<ISet<IFoo>>().Named("my-foos").From().Auto();

            Make<MyFooer>().From().Functoid(MyFooer.Lift().RenameArgs(new[] { "my-foos" }));

            Make<string>().From().Instance("test");
            Make<string>().Named("test-super").From().Instance("test");
            Make<string>().Named("test-sub").From().Instance("test");
            Make<string>().Named("test-prop").From().Instance("test");
            Make<int>().Named("test-sub").From().Instance(43);
            Make<int>().From().Instance(44);

            Make<SomeSub>().From().Lifecycle(IFunctoid.Lift(() => new SomeSub()), SomeSub.LiftInitializer());

            Make<string>().Named("test-private").From().Instance("xxx").Private();

            Make<TestGenFac.Factory>().From().TypedFactory().Using()
                .Functoid(TestGenFac.LiftFactoryFunctoid());

            Make<TestGenInit.Factory>().From().TypedFactory().Using()
                .Lifecycle(
                    IFunctoid.Lift(() => new TestGenInit()),
                    TestGenInit.LiftFactoryFunctoid()
                );
        }
    }
}