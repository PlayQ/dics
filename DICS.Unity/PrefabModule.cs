using UnityEngine;

namespace DICS.Unity
{
    /**
     * Prefab module is to be used on prefabs which need to provide their
     * own bindings.
     */
    [DefaultExecutionOrder(order: int.MinValue)]
    public abstract class PrefabModule: MonoModule
    {
        internal ILocator InstantiationLocator;
        internal GameObject TrueParent;

        protected override ILocator GetParentLocator()
        {
            if (InstantiationLocator != null)
            {
                return InstantiationLocator;
            }

            if (TrueParent)
            {
                TrueParent.HierarchyLocatorLookup(out InstantiationLocator);
            }
            else
            {
                gameObject.SceneLocatorLookup(out InstantiationLocator);
            }
            

            return InstantiationLocator;
        }
    }
}
