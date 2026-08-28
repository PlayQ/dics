using DICS.Attribute;
using NUnit.Framework;

// ReSharper disable PartialTypeWithSinglePart

namespace DICS.Test
{
    [LiftConstructor]
    public partial class MyTestClass
    {
        // Parameterless: the generator should emit Lift() whose signature has zero args.
    }


    [LiftConstructor]
    public partial class Person
    {
        public Person(string firstName, [Id("test")] string lastName, MyTestClass mtc)
        {
        }
    }

    [LiftConstructor]
    public partial class Robot
    {
        public Robot(int id)
        {
        }

        public Robot(int id, string modelName)
        {
        }
    }

    public class GeneratorTest
    {
        // Empty class generates a zero-arg functoid.
        [Test]
        public void LiftConstructor_EmptyClass_ProducesZeroArgFunctoid()
        {
            var functoid = MyTestClass.Lift();
            Assert.That(functoid.Underlying(), Is.EqualTo(typeof(MyTestClass)));
            Assert.That(functoid.Signature().Args.Count, Is.EqualTo(0));
        }

        // [Id("test")] on the second parameter routes it to a named key.
        [Test]
        public void LiftConstructor_IdAttribute_MapsParameterToNamedKey()
        {
            var functoid = Person.Lift();
            var sig = functoid.Signature();

            Assert.That(sig.Args.Count, Is.EqualTo(3));
            Assert.That(sig.Args[0], Is.EqualTo(Key.Of<string>()),       "firstName: unnamed string");
            Assert.That(sig.Args[1], Is.EqualTo(Key.Of<string>("test")), "lastName [Id(\"test\")]: named string");
            Assert.That(sig.Args[2], Is.EqualTo(Key.Of<MyTestClass>()),  "mtc: unnamed MyTestClass");
        }

        // BestConstructor() picks the highest-arity overload. Robot has Robot(int) and
        // Robot(int, string); the lifted functoid must use the 2-arg one.
        [Test]
        public void LiftConstructor_PicksHighestArityConstructor()
        {
            var functoid = Robot.Lift();
            var sig = functoid.Signature();
            Assert.That(sig.Args.Count, Is.EqualTo(2),
                "BestConstructor() must select Robot(int, string), not Robot(int)");
            Assert.That(sig.Args[0], Is.EqualTo(Key.Of<int>()));
            Assert.That(sig.Args[1], Is.EqualTo(Key.Of<string>()));
        }

        // End-to-end: lifted functoid for a parameterless class produces a real instance
        // through an Injector.
        [Test]
        public void LiftConstructor_EmptyClass_RoundTripsThroughInjector()
        {
            var module = new InlineModule(m => m.Make<MyTestClass>().From().Functoid(MyTestClass.Lift()));
            var injector = new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty, module.Freeze());
            var loc = injector.Produce(Key.Of<MyTestClass>());

            Assert.That(loc.Get<MyTestClass>(), Is.Not.Null);
        }
    }

}
