using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DICS
{
    public interface ILocator
    {
        public static readonly ILocator Empty = EmptyLocator.Get();
        Instance[] DumpLocal();
        ILocator GetParent();
        ILocator Inherited(params Instance[] instances);
        ILocator InheritedWithLocal(ILocator other);
        ILocator Remap(IDictionary<Key, Key> mapping);
        bool TryGet<T>(Key key, out T? result) where T : notnull;

        bool TryGet<T>(out T? result) where T : notnull;
        bool TryGet<T>(string name, out T? result) where T : notnull;

        T Get<T>(Key key) where T : notnull;
        T Get<T>() where T : notnull;
        T Get<T>(string name) where T : notnull;
        T Resolve<T>(Key key) where T : notnull;
        T Resolve<T>() where T : notnull;
        T Resolve<T>(string name) where T : notnull;

        bool HasLocally(Key key);
        bool Has(Key key);
        bool TryResolve<T>(Key key, out T? result) where T : notnull;
        bool TryResolve<T>(out T? result) where T : notnull;
        bool TryResolve<T>(string name, out T? result) where T : notnull;

        bool Has(Key key, ILocator resolver);
        bool TryResolve<T>(Key key, ILocator resolver, out T? result) where T : notnull;
        string GetName();
    }


    public abstract class AbstractLocator : ILocator
    {
        protected readonly string _ownerName;
        protected AbstractLocator(string ownerName)
        {
            _ownerName = ownerName;
        }
        
        public abstract Instance[] DumpLocal();
        public abstract ILocator GetParent();

        public abstract bool HasLocally(Key key);
        public abstract ILocator Remap(IDictionary<Key, Key> mapping);

        public abstract bool TryGet<T>(Key key, out T? result) where T : notnull;

        //
        public ILocator Inherited(params Instance[] instances)
        {
            return new LocatorImpl(this, _ownerName, instances);
        }

        public ILocator InheritedWithLocal(ILocator other)
        {
            return Inherited(other.DumpLocal());
        }

        //
        public bool TryGet<T>(out T? result) where T : notnull
        {
            var found = TryGet<T>(Key.Of<T>(), out var resultValue);
            result = resultValue;
            return found;
        }

        //
        public T Get<T>(Key key) where T : notnull
        {
            if (TryGet<T>(key, out var result)) return result!;

            throw new DicsRuntimeException($"Key {key} not found locally for owner {_ownerName}");
        }

        public T Get<T>() where T : notnull
        {
            return Get<T>(Key.Of<T>());
        }

        public T Get<T>(string name) where T : notnull
        {
            return Get<T>(Key.Of<T>(name));
        }

        //
        public T Resolve<T>(Key key) where T : notnull
        {
            if (TryResolve<T>(key, out var result)) return result!;

            throw new DicsRuntimeException($"Key {key} not found in locator hierarchy for owner {_ownerName}");
        }

        public T Resolve<T>() where T : notnull
        {
            return Resolve<T>(Key.Of<T>());
        }

        public T Resolve<T>(string name) where T : notnull
        {
            return Resolve<T>(Key.Of<T>(name));
        }


        //
        public bool Has(Key key)
        {
            return Has(key, this);
        }

        public bool TryResolve<T>(Key key, out T? result) where T : notnull
        {
            var found = TryResolve<T>(key, this, out var res);
            result = res;
            return found;
        }

        public bool TryResolve<T>(out T? result) where T : notnull
        {
            var found = TryResolve<T>(Key.Of<T>(), out var resultValue);
            result = resultValue;
            return found;
        }

        public bool TryResolve<T>(string name, out T? result) where T : notnull
        {
            var found = TryResolve<T>(Key.Of<T>(name), out var resultValue);
            result = resultValue;
            return found;
        }

        public bool TryGet<T>(string name, out T? result) where T : notnull
        {
            var found = TryGet<T>(Key.Of<T>(name), out var resultValue);
            result = resultValue;
            return found;
        }

        public bool Has(Key key, ILocator resolver)
        {
            var has = HasLocally(key);
            if (has)
                if (resolver == this || !IsPrivate(key))
                    return true;

            return GetParent() != this && GetParent().Has(key, resolver);
        }

        //
        public bool TryResolve<T>(Key key, ILocator resolver, out T? result) where T : notnull
        {
            var foundLocally = TryGet<T>(key, out var localValue);

            if (foundLocally)
                if (resolver == this || !IsPrivate(key))
                {
                    result = localValue;
                    return true;
                }

            if (GetParent() != this)
            {
                // renaming happened locally but we should pass renamed key to the parents
                var realKey = Mapped(key);
                var foundInParent = GetParent().TryResolve<T>(realKey, resolver, out var parentValue);
                result = parentValue;
                return foundInParent;
            }

            result = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract Key Mapped(Key key);


        public abstract bool IsPrivate(Key key);

        public string GetName() => _ownerName;
    }
}