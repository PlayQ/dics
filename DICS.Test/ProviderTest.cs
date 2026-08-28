using System;
using NUnit.Framework;

namespace DICS.Test
{
    public class FunctoidTest
    {
        [Test]
        public void Lift_TwoArg_SignatureMatchesParameterTypes()
        {
            Func<int, string, TestRecord> f = (k, l) => new TestRecord(k, l);
            var functoid = IFunctoid.Lift(f);

            Assert.That(functoid.Underlying(), Is.EqualTo(typeof(TestRecord)));
            var sig = functoid.Signature();
            Assert.That(sig.Args.Count, Is.EqualTo(2));
            Assert.That(sig.Args[0], Is.EqualTo(Key.Of<int>()));
            Assert.That(sig.Args[1], Is.EqualTo(Key.Of<string>()));
        }

        [Test]
        public void Lift_TwoArg_NamesOverrideKeys()
        {
            Func<int, string, TestRecord> f = (k, l) => new TestRecord(k, l);
            var functoid = IFunctoid.Lift(f, names: new string?[] { "n1", "n2" });

            var sig = functoid.Signature();
            Assert.That(sig.Args[0], Is.EqualTo(Key.Of<int>("n1")));
            Assert.That(sig.Args[1], Is.EqualTo(Key.Of<string>("n2")));
        }

        [Test]
        public void Lift_TwoArg_InvokesAgainstLocator()
        {
            Func<int, string, TestRecord> f = (k, l) => new TestRecord(k, l);
            var functoid = IFunctoid.Lift(f);

            var loc = new LocatorImpl(ILocator.Empty, "stub",
                new Instance(Key.Of<int>(), 7),
                new Instance(Key.Of<string>(), "seven"));

            var result = (TestRecord)functoid.Invoke(loc);
            Assert.That(result.a, Is.EqualTo(7));
            Assert.That(result.b, Is.EqualTo("seven"));
        }

        [Test]
        public void Lift_ZeroArg_HasEmptySignature()
        {
            var functoid = IFunctoid.Lift(() => "hello");
            Assert.That(functoid.Signature().Args.Count, Is.EqualTo(0));
            Assert.That(functoid.Underlying(), Is.EqualTo(typeof(string)));
            Assert.That(functoid.Invoke(ILocator.Empty), Is.EqualTo("hello"));
        }

        [Test]
        public void Lift_AddFakeDependencies_ExtendsSignature()
        {
            Func<int, string> f = i => i.ToString();
            var functoid = IFunctoid.Lift(f).AddFakeDependencies(Key.Of<double>("extra"));

            var sig = functoid.Signature();
            Assert.That(sig.Args.Count, Is.EqualTo(2));
            Assert.That(sig.Args[1], Is.EqualTo(Key.Of<double>("extra")));
        }

        public record TestRecord(int a, string b);
    }
}
