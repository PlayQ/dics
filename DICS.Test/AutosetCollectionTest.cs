using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Produce-level contract for <c>Auto()</c> collection.
    /// Auto must gather locally produced instances whose type matches, once each,
    /// and must not treat aliases, factories, stubs, or parent imports as extra
    /// elements. IList Auto orders elements dependency-first and keeps module-binding
    /// order between elements that have no dependency relation, so MonoModule can
    /// dispose/tick in reverse construction order. See DisposalOrderTest for the
    /// dependency-driven half of that contract.
    ///
    /// Taxonomy: Behavioral-Active × Blackbox × Group. Origin: regression (the
    /// ImplType fix unmasked these; IList has no HashSet de-dupe).
    /// </summary>
    public class AutosetCollectionTest
    {
        public class D : IDisposable
        {
            public readonly string Name;
            public int DisposeCount;
            public D(string name) => Name = name;
            public void Dispose() => DisposeCount++;
            public override string ToString() => Name;
        }

        public class ChildD : D
        {
            public ChildD(string name) : base(name) { }
        }

        private static Injector InjectorFor(Module module) =>
            new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty, module.Freeze());

        private static Injector InjectorFor(ILocator parent, Module module) =>
            new Injector(parent, IDicsMeasurement.FromDefault(), "child", module.Freeze());

        [Test]
        public void Lifecycle_ExtractorAndInitialized_AppearOnceInAutoIList()
        {
            var module = new InlineModule(m =>
            {
                m.Make<D>().From().Lifecycle(
                    IFunctoid.Lift(() => new D("life")),
                    new InitializerFromLocator<D>((self, sig, loc) => { }, Sig.GetEmpty()));
                m.Make<IList<IDisposable>>().From().Auto();
            });

            var loc = InjectorFor(module).Produce(Key.Of<IList<IDisposable>>(), Key.Of<D>());
            var list = loc.Get<IList<IDisposable>>();
            var instance = loc.Get<D>();

            Assert.That(list.Count, Is.EqualTo(1),
                "Lifecycle extractor ToFunctoid + ToInitializer are one instance");
            Assert.That(list[0], Is.SameAs(instance));

            foreach (var d in list) d.Dispose();
            Assert.That(instance.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void FactoryOfDisposable_IsNotAnAutoIListElement()
        {
            var module = new InlineModule(m =>
            {
                m.Make<IUnsafeFactory<D>>().From().UntypedFactory().Using()
                    .Functoid(IFunctoid.Lift(() => new D("prod")));
                m.Make<IList<IDisposable>>().From().Auto();
            });

            var loc = InjectorFor(module).Produce(
                Key.Of<IList<IDisposable>>(),
                Key.Of<IUnsafeFactory<D>>());

            Assert.That(loc.Get<IList<IDisposable>>(), Is.Empty,
                "FactoryToFunctoid.ImplType is the product type; the factory object is not an IDisposable element");
            Assert.That(loc.Get<IUnsafeFactory<D>>().Make(ILocator.Empty).Name, Is.EqualTo("prod"));
        }

        [Test]
        public void Import_NotCollectedWhenIncludeImportsIsFalse()
        {
            var parent = InjectorFor(new InlineModule(m =>
                m.Make<D>().From().Instance(new D("parent")))).Produce(Key.Of<D>());

            var childMod = new InlineModule(m =>
            {
                m.Make<D>().From().Import();
                m.Make<IList<IDisposable>>().From().Auto();
            });

            var loc = InjectorFor(parent, childMod).Produce(Key.Of<IList<IDisposable>>(), Key.Of<D>());

            Assert.That(loc.Get<D>().Name, Is.EqualTo("parent"),
                "Import still resolves the parent instance");
            Assert.That(loc.Get<IList<IDisposable>>(), Is.Empty,
                "Auto() passes IncludeImports=false; child must not dispose the parent's instance");
        }

        [Test]
        public void TwoRefsToParent_NotCollectedIntoChildAutoIList()
        {
            var parent = InjectorFor(new InlineModule(m =>
                m.Make<ChildD>().From().Instance(new ChildD("shared")))).Produce(Key.Of<ChildD>());

            var childMod = new InlineModule(m =>
            {
                m.Make<D>().Named("a").From().Ref<ChildD>();
                m.Make<D>().Named("b").From().Ref<ChildD>();
                m.Make<IList<IDisposable>>().From().Auto();
            });

            var loc = InjectorFor(parent, childMod).Produce(
                Key.Of<IList<IDisposable>>(),
                Key.Of<D>("a"),
                Key.Of<D>("b"));

            Assert.That(loc.Get<D>("a"), Is.SameAs(parent.Get<ChildD>()));
            Assert.That(loc.Get<D>("b"), Is.SameAs(parent.Get<ChildD>()));
            Assert.That(loc.Get<IList<IDisposable>>(), Is.Empty,
                "ToKey is a name, not a locally produced instance; child must not dispose the parent");
        }

        [Test]
        public void ToDo_IsNotAnAutoIListElement()
        {
            var module = new InlineModule(m =>
            {
                m.Make<D>().ToDo("later");
                m.Make<IList<IDisposable>>().From().Auto();
            });

            var list = InjectorFor(module).Produce(Key.Of<IList<IDisposable>>()).Get<IList<IDisposable>>();
            Assert.That(list, Is.Empty,
                "ToDo.ImplType is the declared key type; a stub must not become an autoset element");
        }

        [Test]
        public void IListAuto_PreservesModuleBindingOrder()
        {
            var module = new InlineModule(m =>
            {
                m.Make<D>().Named("a").From().Instance(new D("a"));
                m.Make<D>().Named("b").From().Instance(new D("b"));
                m.Make<D>().Named("c").From().Instance(new D("c"));
                m.Make<IList<D>>().From().Auto();
            });

            var names = InjectorFor(module).Produce(Key.Of<IList<D>>()).Get<IList<D>>()
                .Select(x => x.Name).ToArray();

            Assert.That(names, Is.EqualTo(new[] { "a", "b", "c" }),
                "Nothing here depends on anything else, so the topological sort must be a no-op and leave module-binding order intact");
        }

        [Test]
        public void Import_CollectedWhenIncludeImportsIsTrue()
        {
            var parent = InjectorFor(new InlineModule(m =>
                m.Make<D>().From().Instance(new D("parent")))).Produce(Key.Of<D>());

            var childMod = new InlineModule(m =>
            {
                m.Make<D>().From().Import();
                m.AddBinding(new Binding.CreateAutoset(
                    Key.Of<IList<IDisposable>>(),
                    typeof(IDisposable),
                    new ListZygote<IDisposable>(),
                    new MutableReferenceZygoteImpl<IList<IDisposable>>(),
                    true,
                    Array.Empty<IAxisPoint>()));
            });

            var loc = InjectorFor(parent, childMod).Produce(Key.Of<IList<IDisposable>>(), Key.Of<D>());
            var list = loc.Get<IList<IDisposable>>();
            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(list[0], Is.SameAs(loc.Get<D>()));
        }

        [Test]
        public void DistinctInstances_AreNotCollapsedInAutoIList()
        {
            var module = new InlineModule(m =>
            {
                m.Make<D>().Named("a").From().Instance(new D("a"));
                m.Make<D>().Named("b").From().Instance(new D("b"));
                m.Make<IList<IDisposable>>().From().Auto();
            });

            var list = InjectorFor(module).Produce(Key.Of<IList<IDisposable>>()).Get<IList<IDisposable>>();
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list[0], Is.Not.SameAs(list[1]));
        }

        [Test]
        public void ISetAuto_StillContainsLocalProducer_WhenAliased()
        {
            var module = new InlineModule(m =>
            {
                m.Make<ChildD>().From().Instance(new ChildD("one")).Aliased<D>();
                m.Make<ISet<D>>().From().Auto();
            });

            var loc = InjectorFor(module).Produce(Key.Of<ISet<D>>(), Key.Of<ChildD>(), Key.Of<D>());
            var set = loc.Get<ISet<D>>();
            Assert.That(set.Count, Is.EqualTo(1));
            Assert.That(set.Single(), Is.SameAs(loc.Get<ChildD>()));
        }
    }
}
