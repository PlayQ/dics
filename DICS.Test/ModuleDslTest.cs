using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Focused behaviour tests for the module DSL operators —
    /// <c>.Aliased&lt;U&gt;()</c>, <c>.AddToSetOf&lt;U&gt;()</c>,
    /// and <c>.AddDependency&lt;U&gt;()</c>.
    /// </summary>
    public class ModuleDslTest
    {
        public interface IAnimal { }
        public class Dog : IAnimal { }

        private static Injector NewInjector(Module module) =>
            new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty, module.Freeze());

        // M23a: .Aliased<U>() makes the binding reachable under Key.Of<U>() and the alias
        // resolves to the same instance as the original key (one-and-the-same singleton).
        [Test]
        public void Aliased_ProducesSameInstance_UnderAliasKey()
        {
            var module = new InlineModule(m =>
            {
                m.Make<Dog>().From().Instance(new Dog()).Aliased<IAnimal>();
            });

            var loc = NewInjector(module).Produce(Key.Of<Dog>(), Key.Of<IAnimal>());

            var concrete = loc.Get<Dog>();
            var alias = loc.Get<IAnimal>();
            Assert.That(concrete, Is.Not.Null);
            Assert.That(alias, Is.SameAs(concrete),
                "Aliased<IAnimal>() must resolve to the same instance as the original Dog binding");
        }

        // Regression: both WordMakeSetImpl.Functoid overloads registered the element's
        // producer under SetKey instead of the minted element key, so the set key carried
        // a ToFunctoid and a CreateSet at once and planning threw
        // DicsBug("Inconsistent set definition"). Only .Add().Instance() was exercised
        // before, which binds the element key correctly.
        //
        // Taxonomy: Behavioral-Active × Blackbox × Atomic. Origin: regression.
        [Test]
        public void AddFunctoid_BindsTheElementKey_NotTheSetKey()
        {
            var module = new InlineModule(m =>
            {
                m.Make<ISet<IAnimal>>().Named("pets").Add().Functoid(IFunctoid.Lift(() => new Dog()));
            });

            var set = NewInjector(module).Produce(Key.Of<ISet<IAnimal>>("pets")).Get<ISet<IAnimal>>("pets");

            Assert.That(set.Count, Is.EqualTo(1));
            Assert.That(set, Has.All.InstanceOf<Dog>());
        }

        // Same defect through the (Func<ILocator, T>, Sig) overload.
        [Test]
        public void AddLocatorFunctoid_BindsTheElementKey_NotTheSetKey()
        {
            var module = new InlineModule(m =>
            {
                m.Make<ISet<IAnimal>>().Named("pets").Add()
                    .Functoid(_ => new Dog(), Sig.GetEmpty());
            });

            var set = NewInjector(module).Produce(Key.Of<ISet<IAnimal>>("pets")).Get<ISet<IAnimal>>("pets");

            Assert.That(set.Count, Is.EqualTo(1));
            Assert.That(set, Has.All.InstanceOf<Dog>());
        }

        // M23b: .AddToSetOf<U>() adds the bound key to Key.Of<ISet<U>>() autoset.
        // We pair this with .From().Empty() on the set so the planner has a concrete
        // CreateSet binding to merge into.
        [Test]
        public void AddToSetOf_PlacesKeyInAutoset()
        {
            var module = new InlineModule(m =>
            {
                m.Make<ISet<IAnimal>>().From().Empty();
                m.Make<Dog>().From().Instance(new Dog()).AddToSetOf<IAnimal>();
            });

            var loc = NewInjector(module).Produce(Key.Of<ISet<IAnimal>>());
            var set = loc.Get<ISet<IAnimal>>();
            Assert.That(set.Count, Is.EqualTo(1));
            Assert.That(set, Has.Member(loc.Get<Dog>()));
        }

        // M23c: .AddDependency<U>() forces an ordering edge that would otherwise be missed.
        // Build a graph where without the explicit dep, the "late" key could be produced
        // before its semantic prerequisite. We observe via instantiation order recorded in
        // the side-effect-visible counter inside the functoids.
        [Test]
        public void AddDependency_AffectsInstantiationOrder()
        {
            var order = new List<string>();

            var module = new InlineModule(m =>
            {
                m.Make<string>().Named("a").From().Functoid(
                    IFunctoid.Lift(() =>
                    {
                        lock (order) order.Add("a");
                        return "A";
                    }));
                m.Make<string>().Named("b").From().Functoid(
                    IFunctoid.Lift(() =>
                    {
                        lock (order) order.Add("b");
                        return "B";
                    })).AddDependency<string>("a");
            });

            var loc = NewInjector(module).Produce(Key.Of<string>("a"), Key.Of<string>("b"));
            Assert.That(loc.Get<string>("a"), Is.EqualTo("A"));
            Assert.That(loc.Get<string>("b"), Is.EqualTo("B"));
            Assert.That(order, Is.EqualTo(new List<string> { "a", "b" }),
                "AddDependency<string>(\"a\") must force 'a' to be produced before 'b'");
        }

        // M24a: child-injector override of a parent locator binding.
        // Investigate semantics: with the same key bound at both levels, the child Injector
        // gets a Plan that schedules the child's own binding; the parent locator's value
        // remains untouched. Resolve from the produced (child) locator yields the child's
        // binding.
        [Test]
        public void ChildInjector_OverridesParentLocatorBinding()
        {
            var parentModule = new InlineModule(m =>
                m.Make<int>().Named("x").From().Instance(1));
            var parentLoc = NewInjector(parentModule).Produce(Key.Of<int>("x"));

            var childModule = new InlineModule(m =>
                m.Make<int>().Named("x").From().Instance(2));
            var childInjector = new Injector(parentLoc, IDicsMeasurement.FromDefault(), "child",
                childModule.Freeze());
            var childLoc = childInjector.Produce(Key.Of<int>("x"));

            // Child's own binding wins on resolve from the child locator.
            Assert.That(childLoc.Get<int>("x"), Is.EqualTo(2),
                "Child injector's local binding takes precedence over the inherited parent value");
            // Parent locator is unchanged.
            Assert.That(parentLoc.Get<int>("x"), Is.EqualTo(1),
                "Parent locator's binding remains visible at the parent");
        }

        // M24b: .Private() blocks the inheritance chain. A child injector binds an Import
        // for the same key; trying to resolve via Has() or Resolve through the parent chain
        // must NOT cross the privacy boundary.
        [Test]
        public void PrivateBinding_BlocksChildResolutionThroughParentChain()
        {
            var parentModule = new InlineModule(m =>
                m.Make<string>().From().Instance("secret").Private());
            var parentInjector = NewInjector(parentModule);

            var parentLoc = parentInjector.Produce(Key.Of<string>());

            // Sanity: from the parent locator's own resolver perspective the key is reachable.
            Assert.That(parentLoc.HasLocally(Key.Of<string>()), Is.True);
            Assert.That(parentLoc.IsPrivateExternallyVisible(Key.Of<string>()), Is.True,
                "The parent's binding must be marked private after Produce");

            // A child constructed with the parent locator as its parent cannot cross the
            // privacy boundary via the resolver protocol.
            var childLoc = new LocatorImpl(parentLoc, "child");
            // child has no local binding for string; Resolve should fail because the parent
            // binding is private to its own resolver.
            Assert.That(childLoc.TryResolve<string>(Key.Of<string>(), out _), Is.False,
                "Private binding in the parent must not be reachable from a child locator");
            Assert.That(childLoc.Has(Key.Of<string>()), Is.False,
                "Has() through the parent chain must respect privacy");
        }
    }

    // Surface AbstractLocator.IsPrivate for tests via an extension.
    internal static class LocatorTestExtensions
    {
        public static bool IsPrivateExternallyVisible(this ILocator loc, Key key)
        {
            if (loc is AbstractLocator a) return a.IsPrivate(key);
            return false;
        }
    }
}
