using System.Collections.Generic;

namespace DICS.Unity
{
    public interface IModuleWithRoots
    {
        public ISet<Key> RequiredRoots();
    }
}