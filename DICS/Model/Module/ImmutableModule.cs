using System.Collections.Generic;
using System.Linq;

namespace DICS
{
    public record ImmutableModule(
        IList<Binding> Bindings,
        IDictionary<Key, ISet<Key>> ExtraDeps,
        ISet<Key> PrivateBindings)
    {
        /// <summary>
        ///     Most likely it's a bad idea to use this method.
        /// </summary>
        public ISet<Key> AllKeys()
        {
            return Bindings.Select(b => b.Key).ToHashSet();
        }

        public static ImmutableModule Merge(IEnumerable<ImmutableModule> modules)
        {
            var asList = modules.ToList();
            return new ImmutableModule(
                asList.SelectMany(m => m.Bindings).ToList(),
                asList.Select(m => m.ExtraDeps).Aggregate(
                    new Dictionary<Key, ISet<Key>>(),
                    (a, b) => CollectionOps.MergeDictionaries(a, b)
                ),
                asList.SelectMany(m => m.PrivateBindings).ToHashSet()
            );
        }

        public static ImmutableModule Merge(IEnumerable<Module> modules)
        {
            var frozen = modules.Select(m => m.Freeze()).ToList();
            return new ImmutableModule(
                frozen.SelectMany(m => m.Bindings).ToList(),
                frozen.Select(m => m.ExtraDeps).Aggregate(
                    new Dictionary<Key, ISet<Key>>(),
                    (a, b) => CollectionOps.MergeDictionaries(a, b)
                ),
                frozen.SelectMany(m => m.PrivateBindings).ToHashSet()
            );
        }
    }
}