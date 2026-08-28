using System.Collections.Generic;
using DICS.Attribute;
using NUnit.Framework;

namespace DICS.Test
{
    public interface IFoo
    {
        void Bar();
    }

    [LiftConstructor]
    public partial class MyFoo : IFoo
    {
        public int BarCalls { get; private set; }
        public void Bar() => BarCalls++;
    }

    [LiftConstructor]
    public partial class MyFooer
    {
        private readonly ISet<IFoo> _foos;

        public MyFooer(ISet<IFoo> foos)
        {
            // At this point the set is not populated yet because the producer is still
            // wiring keys. We hold the reference; it is populated by the time someone
            // calls Bars() (i.e. after Produce returns).
            _foos = foos;
        }

        public int FooCountAtConstruction { get; private init; }

        public void Bars()
        {
            foreach (var foo in _foos) foo.Bar();
        }

        public ISet<IFoo> Foos => _foos;
    }

    public class InitializerPatternDemoModule : Module
    {
        public InitializerPatternDemoModule()
        {
            Make<IFoo>().From().Functoid(MyFoo.Lift());
            Make<ISet<IFoo>>().Named("my-foos").From().Auto();
            Make<MyFooer>().From().Functoid(MyFooer.Lift().RenameArgs(new[] { "my-foos" }));
        }
    }

    public class InitializerPatternDemo
    {
        [Test]
        public void Autoset_PopulatedByProductionTime_NotConstructionTime()
        {
            var module = new InitializerPatternDemoModule();
            var injector = new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty,
                module.Freeze());
            var locator = injector.Produce(Key.Of<MyFooer>(), Key.Of<IFoo>());

            var fooer = locator.Get<MyFooer>();
            Assert.That(fooer.Foos.Count, Is.EqualTo(1),
                "By the time Produce returns, the autoset should contain the single IFoo");

            fooer.Bars();
            var foo = (MyFoo)locator.Get<IFoo>();
            Assert.That(foo.BarCalls, Is.EqualTo(1),
                "MyFoo.Bar should be invoked exactly once via the autoset iteration");
        }
    }
}
