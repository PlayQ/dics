using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DICS
{
    public class LocatorImpl : AbstractLocator, IMutableLocator
    {
        private readonly ILocator _parent;
        private readonly HashSet<Key> _privateBindings = new();
        private readonly ConcurrentDictionary<Key, object> _values = new();
        private IDictionary<Key, Key>? _mapping;

        public LocatorImpl(ILocator parent, string ownerName) : base(ownerName)
        {
            _parent = parent;
        }

        public LocatorImpl(ILocator parent, ISet<Key> privateBindings, string ownerName) : base(ownerName)
        {
            _parent = parent;
            _privateBindings.UnionWith(privateBindings);
        }

        public LocatorImpl(ILocator parent, string ownerName, params Instance[] instances) : base(ownerName)
        {
            _parent = parent;
            foreach (var instance in instances) Put(instance.Key, instance.Value);
        }

        public void Put<T>(Key key, T value) where T : notnull
        {
            if (!_values.TryAdd(key, value))
                throw new DicsRuntimeException($"Key {key} is already present in locator for module {_ownerName}!");
        }

        public override bool IsPrivate(Key key)
        {
            var realKey = Mapped(key);
            return _privateBindings.Contains(realKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override Key Mapped(Key key)
        {
            if (_mapping != null && _mapping.TryGetValue(key, out var remapped)) return remapped;

            return key;
        }

        /// <summary>
        ///     Don't use this method if you don't know exactly what you are doing!
        /// </summary>
        public void SetMapping(IDictionary<Key, Key> mapping)
        {
            _mapping = mapping;
        }

        public override bool HasLocally(Key key)
        {
            var realKey = Mapped(key);
            return _values.ContainsKey(realKey);
        }

        public override ILocator Remap(IDictionary<Key, Key> mapping)
        {
            var sub = new LocatorImpl(this, _ownerName, new Instance[] { });
            sub.SetMapping(mapping);
            return sub;
        }

        public override bool TryGet<T>(Key key, out T result)
        {
            var realKey = Mapped(key);
            var found = _values.TryGetValue(realKey, out var foundValue);
            if (found)
            {
                Debug.Assert(foundValue! is T?,
                    $"{foundValue.GetType()} <!< {typeof(T?)}");

                // https://github.com/dotnet/roslyn/issues/76914
                // ReSharper disable once AssignNullToNotNullAttribute
                result = (T)foundValue;
            }
            else
            {
#pragma warning disable CS8601 // Possible null reference assignment.
                result = default;
#pragma warning restore CS8601 // Possible null reference assignment.
            }

            return found;
        }

        public override ILocator GetParent()
        {
            return _parent;
        }

        public override Instance[] DumpLocal()
        {
            return _values.ToList().Select(kv => new Instance(Mapped(kv.Key), kv.Value)).ToArray();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"LocatorImpl#{GetHashCode()} inherited from {_parent.GetHashCode()}\n");
            foreach (var (key, value) in _values)
            {
                var realKey = Mapped(key);
                if (realKey == key)
                    sb.Append($"- {key} = {value}\n");
                else
                    sb.Append($"- {key} (mapped to {realKey}) = {value}\n");
            }

            return sb.ToString();
        }
    }
}