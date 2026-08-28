using System.Collections.Generic;

namespace DICS
{
    public interface ICollectionInstance
    {
        void Add(object o);
        object Retrieve();
    }

    internal class SetInstanceImpl<T> : ICollectionInstance
    {
        private readonly HashSet<T> _theSet;

        public SetInstanceImpl()
        {
            _theSet = new HashSet<T>();
        }

        public void Add(object o)
        {
            _theSet.Add((T)o);
        }

        public object Retrieve()
        {
            return _theSet;
        }
    }

    internal class ListInstanceImpl<T> : ICollectionInstance
    {
        private readonly List<T> _theSet;

        public ListInstanceImpl()
        {
            _theSet = new List<T>();
        }

        public void Add(object o)
        {
            var item = (T)o;
            for (var i = 0; i < _theSet.Count; i++)
                if (ReferenceEquals(_theSet[i], item))
                    return;
            _theSet.Add(item);
        }

        public object Retrieve()
        {
            return _theSet;
        }
    }

    public interface ISetZygote
    {
        ICollectionInstance Create();
    }


    internal class SetZygote<T> : ISetZygote
    {
        public ICollectionInstance Create()
        {
            return new SetInstanceImpl<T>();
        }
    }

    internal class ListZygote<T> : ISetZygote
    {
        public ICollectionInstance Create()
        {
            return new ListInstanceImpl<T>();
        }
    }
}