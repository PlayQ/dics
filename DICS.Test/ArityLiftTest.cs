using System;
using NUnit.Framework;

namespace DICS.Test
{
    public class ArityLiftTest
    {
        public record A1(int v);
        public record A2(int v);
        public record A3(int v);
        public record A4(int v);
        public record A5(int v);
        public record A6(int v);
        public record A7(int v);

        public record Result7(A1 a1, A2 a2, A3 a3, A4 a4, A5 a5, A6 a6, A7 a7);

        public class Holder7
        {
            public A1? a1;
            public A2? a2;
            public A3? a3;
            public A4? a4;
            public A5? a5;
            public A6? a6;
            public A7? a7;
        }

        private static readonly string?[] Names7 =
            { "n1", "n2", "n3", "n4", "n5", "n6", "n7" };

        [Test]
        public void Lift_SevenArg_Functoid_NamesAndInvocation()
        {
            Func<A1, A2, A3, A4, A5, A6, A7, Result7> f =
                (a1, a2, a3, a4, a5, a6, a7) => new Result7(a1, a2, a3, a4, a5, a6, a7);

            var functoid = IFunctoid.Lift(f, Names7);
            var sig = functoid.Signature();

            // Signature shape
            Assert.That(sig.Args.Count, Is.EqualTo(7));
            Assert.That(sig.Args[0], Is.EqualTo(Key.Of<A1>("n1")));
            Assert.That(sig.Args[1], Is.EqualTo(Key.Of<A2>("n2")));
            Assert.That(sig.Args[2], Is.EqualTo(Key.Of<A3>("n3")));
            Assert.That(sig.Args[3], Is.EqualTo(Key.Of<A4>("n4")));
            Assert.That(sig.Args[4], Is.EqualTo(Key.Of<A5>("n5")));
            Assert.That(sig.Args[5], Is.EqualTo(Key.Of<A6>("n6")));
            Assert.That(sig.Args[6], Is.EqualTo(Key.Of<A7>("n7")));

            // The bug symptom: Args[5] and Args[6] were equal (same name/type collision)
            Assert.That(sig.Args[5].Name, Is.Not.EqualTo(sig.Args[6].Name));

            // Build a locator stocked with seven distinct values keyed by the seven named keys.
            // This also catches body-of-lambda mismatch (wrong Get<Tx>(loc, kx) pairing).
            var loc = new LocatorImpl(ILocator.Empty, "stub",
                new Instance(Key.Of<A1>("n1"), new A1(1)),
                new Instance(Key.Of<A2>("n2"), new A2(2)),
                new Instance(Key.Of<A3>("n3"), new A3(3)),
                new Instance(Key.Of<A4>("n4"), new A4(4)),
                new Instance(Key.Of<A5>("n5"), new A5(5)),
                new Instance(Key.Of<A6>("n6"), new A6(6)),
                new Instance(Key.Of<A7>("n7"), new A7(7)));

            var result = (Result7)functoid.Invoke(loc);
            Assert.That(result.a1.v, Is.EqualTo(1));
            Assert.That(result.a2.v, Is.EqualTo(2));
            Assert.That(result.a3.v, Is.EqualTo(3));
            Assert.That(result.a4.v, Is.EqualTo(4));
            Assert.That(result.a5.v, Is.EqualTo(5));
            Assert.That(result.a6.v, Is.EqualTo(6));
            Assert.That(result.a7.v, Is.EqualTo(7));
        }

        [Test]
        public void Lift_SevenArg_Initializer_NamesAndInvocation()
        {
            Action<Holder7, Sig, A1, A2, A3, A4, A5, A6, A7> f =
                (self, sig, a1, a2, a3, a4, a5, a6, a7) =>
                {
                    self.a1 = a1;
                    self.a2 = a2;
                    self.a3 = a3;
                    self.a4 = a4;
                    self.a5 = a5;
                    self.a6 = a6;
                    self.a7 = a7;
                };

            var initializer = IInitializer.Lift<A1, A2, A3, A4, A5, A6, A7, Holder7>(f, Names7);
            var sig = initializer.Signature();

            Assert.That(sig.Args.Count, Is.EqualTo(7));
            Assert.That(sig.Args[0], Is.EqualTo(Key.Of<A1>("n1")));
            Assert.That(sig.Args[1], Is.EqualTo(Key.Of<A2>("n2")));
            Assert.That(sig.Args[2], Is.EqualTo(Key.Of<A3>("n3")));
            Assert.That(sig.Args[3], Is.EqualTo(Key.Of<A4>("n4")));
            Assert.That(sig.Args[4], Is.EqualTo(Key.Of<A5>("n5")));
            Assert.That(sig.Args[5], Is.EqualTo(Key.Of<A6>("n6")));
            Assert.That(sig.Args[6], Is.EqualTo(Key.Of<A7>("n7")));

            // Bug symptom: Args[5] and Args[6] previously had identical keys.
            Assert.That(sig.Args[5].Name, Is.Not.EqualTo(sig.Args[6].Name));

            var loc = new LocatorImpl(ILocator.Empty, "stub",
                new Instance(Key.Of<A1>("n1"), new A1(11)),
                new Instance(Key.Of<A2>("n2"), new A2(22)),
                new Instance(Key.Of<A3>("n3"), new A3(33)),
                new Instance(Key.Of<A4>("n4"), new A4(44)),
                new Instance(Key.Of<A5>("n5"), new A5(55)),
                new Instance(Key.Of<A6>("n6"), new A6(66)),
                new Instance(Key.Of<A7>("n7"), new A7(77)));

            var holder = new Holder7();
            initializer.Initialize(holder, loc);

            Assert.That(holder.a1!.v, Is.EqualTo(11));
            Assert.That(holder.a2!.v, Is.EqualTo(22));
            Assert.That(holder.a3!.v, Is.EqualTo(33));
            Assert.That(holder.a4!.v, Is.EqualTo(44));
            Assert.That(holder.a5!.v, Is.EqualTo(55));
            Assert.That(holder.a6!.v, Is.EqualTo(66));
            Assert.That(holder.a7!.v, Is.EqualTo(77));
        }
    }
}
