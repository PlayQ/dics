namespace DICS
{
    public interface IMutableReferenceZygote
    {
        IInternalMutableReference Create();
    }


    internal class MutableReferenceZygoteImpl<T> : IMutableReferenceZygote
    {
        public IInternalMutableReference Create()
        {
            return new MagicMutableDicsReference<T>();
        }
    }

    public interface IInternalMutableReference
    {
        void SetUnsafe(object newValue);
    }

    public class MagicMutableDicsReference<T> : IInternalMutableReference
    {
        private bool _initialized;
        private T? _value;

        public MagicMutableDicsReference()
        {
            _value = default;
        }

        void IInternalMutableReference.SetUnsafe(object newValue)
        {
            if (_initialized)
                throw new DicsRuntimeException($"Mutable reference to {typeof(T)} is already initialized");
            _value = (T?)newValue;
            _initialized = true;
        }

        public T? Get()
        {
            if (!_initialized)
                throw new DicsRuntimeException($"Mutable reference to {typeof(T)} is not yet initialized");
            return _value;
        }

        internal void Set(T? newValue)
        {
            if (_initialized)
                throw new DicsRuntimeException($"Mutable reference to {typeof(T)} is already initialized");
            _value = newValue;
            _initialized = true;
        }
    }
}