using System.Collections.Generic;
using NUnit.Framework;

namespace DICS.Test
{
    /// <summary>
    /// Direct unit tests for <see cref="LocatorImpl"/>. Exercises the documented
    /// <see cref="ILocator.Remap"/> contract: a sub-locator that translates a caller-
    /// declared key to the underlying storage key on lookup.
    /// </summary>
    public class LocatorImplTest
    {
        // H20: Remap contract probe.
        //
        // The mapping passed to Remap is interpreted by the codebase as
        // {callerKey -> storedKey}: when callsite (e.g. a Functoid with renamed args)
        // declares the dep under callerKey, the locator must translate it to storedKey
        // for the lookup. This matches Functoid.RenameArgs, which builds a mapping
        // from the original Sig.Args to the renamed Sig.Args before delegating to
        // FunctoidFromLocator with Remap.
        //
        // So to alias an existing "int" to the name "alias", the mapping must be
        // { Key.Of<int>("alias") -> Key.Of<int>() }, NOT the inverse.
        [Test]
        public void Remap_ResolvesAliasedKeyThroughParent()
        {
            var root = new LocatorImpl(ILocator.Empty, "root");
            root.Put(Key.Of<int>(), 7);
            root.Put(Key.Of<string>(), "x");

            var mapping = new Dictionary<Key, Key>
            {
                [Key.Of<int>("alias")] = Key.Of<int>(),
            };
            var sub = root.Remap(mapping);

            // Caller looks up using the alias-side key; sub translates to the underlying
            // key and finds it in the parent.
            Assert.That(sub.Resolve<int>("alias"), Is.EqualTo(7),
                "Resolve<int>(\"alias\") must translate via the mapping and return the parent's int=7");

            // Non-aliased keys still resolve through the parent chain unchanged.
            Assert.That(sub.Resolve<string>(), Is.EqualTo("x"),
                "Non-mapped keys resolve through the parent chain unchanged");
        }

        // HasLocally on a remap result reflects what is in the sub-locator's *own*
        // storage — and a fresh Remap result has no local storage at all. The aliasing
        // is a *lookup* concern, not a *presence* concern. This test pins the current
        // contract.
        [Test]
        public void Remap_HasLocally_ReflectsLocalStorage_NotParentChain()
        {
            var root = new LocatorImpl(ILocator.Empty, "root");
            root.Put(Key.Of<int>(), 7);

            var mapping = new Dictionary<Key, Key>
            {
                [Key.Of<int>("alias")] = Key.Of<int>(),
            };
            var sub = root.Remap(mapping);

            // The sub-locator's own storage is empty: it was constructed via
            // `new LocatorImpl(this, _ownerName, new Instance[]{})`. Neither the alias
            // key nor the underlying key live in `sub`'s `_values`.
            Assert.That(sub.HasLocally(Key.Of<int>()), Is.False,
                "Sub-locator's own storage is empty; the underlying int lives in the parent");
            Assert.That(sub.HasLocally(Key.Of<int>("alias")), Is.False,
                "Sub-locator's own storage is empty; alias resolution is a parent-chain concern, not local");

            // Has() walks the parent chain. The non-aliased key is found directly in
            // the parent. The aliased key is NOT seen via Has() because Has() passes
            // the caller-side key unchanged when descending — only TryResolve translates
            // through Mapped on the descent. Pinning this asymmetry as the current
            // contract; if it diverges from what the docs imply, that's a separate
            // concern (see also TryResolve below, which does translate).
            Assert.That(sub.Has(Key.Of<int>()), Is.True,
                "Has() walks the parent chain and finds the underlying int directly");

            // TryResolve translates the alias and finds the underlying value in the parent.
            Assert.That(sub.TryResolve<int>(Key.Of<int>("alias"), out var aliasValue), Is.True,
                "TryResolve translates the alias via Mapped() and finds the underlying int");
            Assert.That(aliasValue, Is.EqualTo(7));
        }
    }
}
