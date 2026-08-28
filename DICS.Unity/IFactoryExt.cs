using UnityEngine;

namespace DICS.Unity
{
    public static class IFactoryExt
    {
        private const string LOCATOR_NAME = "FactoryParameters";
        public static T MakeComponent<T>(this IUnsafeFactory<T> factory) where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            return factory.Make(ILocator.Empty);
        }
        
        public static T MakeComponent<T>(this IUnsafeFactory<T> factory, GameObject prefab) where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            var locator = new LocatorImpl(ILocator.Empty, LOCATOR_NAME);
            locator.Put(Key.Of<GameObject>(), prefab);
            return factory.Make(locator);
        }
        
        public static T MakeComponent<T>(this IUnsafeFactory<T> factory, Transform parent) where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            var locator = new LocatorImpl(ILocator.Empty, LOCATOR_NAME);
            locator.Put(Key.Of<Transform>(), parent);
            return factory.Make(locator);
        }
        
        public static T MakeComponent<T>(this IUnsafeFactory<T> factory, GameObject prefab, Transform parent) 
            where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            var locator = new LocatorImpl(ILocator.Empty, LOCATOR_NAME);
            locator.Put(Key.Of<GameObject>(), prefab);
            locator.Put(Key.Of<Transform>(), parent);
            return factory.Make(locator);
        }
        
        public static T MakeComponent<T, T1>(this IUnsafeFactory<T> factory, GameObject prefab, Transform parent, T1 param1) 
            where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            var locator = new LocatorImpl(ILocator.Empty, LOCATOR_NAME);
            locator.Put(Key.Of<GameObject>(), prefab);
            locator.Put(Key.Of<Transform>(), parent);
            locator.Put(Key.Of<T1>(), param1);
            return factory.Make(locator);
        }
        
        public static T MakeComponent<T, T1>(this IUnsafeFactory<T> factory, 
            GameObject prefab, Transform parent, GameObject instantiationParent, T1 param1)
            where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            var locator = new LocatorImpl(ILocator.Empty, LOCATOR_NAME);
            locator.Put(Key.Of<GameObject>(), prefab);
            locator.Put(Key.Of<Transform>(), parent);
            locator.Put(Key.Of<GameObject>(ModuleExt.INSTANTIATION_PARENT_TAG), instantiationParent);
            locator.Put(Key.Of<T1>(), param1);
            return factory.Make(locator);
        }
        
        public static T MakeComponent<T, T1, T2>(this IUnsafeFactory<T> factory, GameObject prefab, Transform parent, T1 param1, T2 param2) where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            var locator = new LocatorImpl(ILocator.Empty, LOCATOR_NAME);
            locator.Put(Key.Of<GameObject>(), prefab);
            locator.Put(Key.Of<Transform>(), parent);
            locator.Put(Key.Of<T1>(), param1);
            locator.Put(Key.Of<T2>(), param2);
            return factory.Make(locator);
        }
        
        public static T MakeComponent<T, T1, T2, T3>(this IUnsafeFactory<T> factory, GameObject prefab, Transform parent, T1 param1, T2 param2, T3 param3) where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            var locator = new LocatorImpl(ILocator.Empty, LOCATOR_NAME);
            locator.Put(Key.Of<GameObject>(), prefab);
            locator.Put(Key.Of<Transform>(), parent);
            locator.Put(Key.Of<T1>(), param1);
            locator.Put(Key.Of<T2>(), param2);
            locator.Put(Key.Of<T3>(), param3);
            return factory.Make(locator);
        }
        
        public static T MakeComponent<T, T1, T2, T3, T4>(this IUnsafeFactory<T> factory, GameObject prefab, Transform parent, T1 param1, T2 param2, T3 param3, T4 param4) where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            var locator = new LocatorImpl(ILocator.Empty, LOCATOR_NAME);
            locator.Put(Key.Of<GameObject>(), prefab);
            locator.Put(Key.Of<Transform>(), parent);
            locator.Put(Key.Of<T1>(), param1);
            locator.Put(Key.Of<T2>(), param2);
            locator.Put(Key.Of<T3>(), param3);
            locator.Put(Key.Of<T4>(), param4);
            return factory.Make(locator);
        }
        
        public static T MakeComponent<T, T1, T2, T3, T4, T5>(this IUnsafeFactory<T> factory, GameObject prefab, Transform parent, T1 param1, T2 param2, T3 param3, T4 param4, T5 param5) where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            var locator = new LocatorImpl(ILocator.Empty, LOCATOR_NAME);
            locator.Put(Key.Of<GameObject>(), prefab);
            locator.Put(Key.Of<Transform>(), parent);
            locator.Put(Key.Of<T1>(), param1);
            locator.Put(Key.Of<T2>(), param2);
            locator.Put(Key.Of<T3>(), param3);
            locator.Put(Key.Of<T4>(), param4);
            locator.Put(Key.Of<T5>(), param5);
            return factory.Make(locator);
        }
        
        
        public static T MakeComponentWithParent<T>(this IUnsafeFactory<T> factory, 
            GameObject prefab, GameObject trueParent) where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            var locator = new LocatorImpl(ILocator.Empty, LOCATOR_NAME);
            locator.Put(Key.Of<GameObject>(), prefab);
            locator.Put(Key.Of<GameObject>(ModuleExt.INSTANTIATION_PARENT_TAG), trueParent);
            return factory.Make(locator);
        }
        public static T MakeComponentWithParent<T>(this IUnsafeFactory<T> factory, 
            GameObject prefab, Transform parent, GameObject trueParent) where T: notnull, MonoBehaviour, ILifecycleComponent
        {
            var locator = new LocatorImpl(ILocator.Empty, LOCATOR_NAME);
            locator.Put(Key.Of<GameObject>(), prefab);
            locator.Put(Key.Of<Transform>(), parent);
            locator.Put(Key.Of<GameObject>(ModuleExt.INSTANTIATION_PARENT_TAG), trueParent);
            return factory.Make(locator);
        }
    }
}