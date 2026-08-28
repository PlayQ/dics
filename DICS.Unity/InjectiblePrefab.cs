using System;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

namespace DICS.Unity
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(order: int.MinValue)]
    public sealed class InjectiblePrefab : MonoLifecycle
    { 
        internal ILocator InstantiationLocator;
        internal GameObject TrueParent;
        public ILocator Locator => InstantiationLocator;
        
        private bool isInitialized = false;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            
            var go = gameObject;
            
            // Nothing to initialize
            if (lifecycleComponents.Length == 0)
            {
                return;
            }
            
            // If we have a prefab module on us, do not do anything with regards to self-injection.
            if (TryGetComponent<PrefabModule>(out _))
            {
                return;
            }

            if (InstantiationLocator == null)
            {
                Debug.LogError($"Prefab {go.FullPath()} has {lifecycleComponents.Length} components for injection," +
                               $" but no locator is provided on Initialize. Most likely prefab was instantiated" +
                               $" with GameObject.Instantiate use InjectiblePrefab.Instantiate instead, fallback to scene locator");
                
                if (!gameObject.SceneLocatorLookup(out InstantiationLocator))
                {
                    Debug.LogError(
                        $"Prefab {go.name} in path {go.FullPath()} has {lifecycleComponents.Length} components for injection," +
                        $" but no locator is found");
                    return;
                }
            }

            if (lifecycleComponents.Any(c => c == null))
            {
                Debug.LogError($"Prefab {gameObject.FullPath()} with InjectiblePrefab component " +
                               "has some serialized references to ILifecycleComponents that are null." +
                               " Fallback to find lifecycleObjects in runtime");
                //Lookup references at runtime
                var injectibles = GetComponentsInChildren<ILifecycleComponent>(true);
                LifecycleInitializer.InitializeAll(injectibles, InstantiationLocator);
            }
            else
            {
                Initialize(InstantiationLocator);    
            }
        }

        private static void PostInstantiate(
            GameObject prefab,
            GameObject clone,
            LocatorSource locatorSource,
            bool reactivate,
            bool initialize,
            PrefabModule prefabModule,
            InjectiblePrefab injectiblePrefab
        )
        {
            ILocator locator = null;
            GameObject trueParent = null;

            switch (locatorSource)
            {
                case LocatorSource.CurrentScene currentScene:
                    clone.SceneLocatorLookup(out locator);
                    break;
                case LocatorSource.Reference reference:
                    locator = reference.Locator;
                    break;
                case LocatorSource.TrueParent parent:
                    trueParent = parent.Parent; 
                    trueParent.HierarchyLocatorLookup(out locator);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unexpected value: {locatorSource} for type: {locatorSource.GetType()}");
            }
            
            if (initialize)
            {
                // We need to initialize our prefab. Unfortunately, we had no prefab module or injectible prefab on it,
                // so we fallback onto the inefficient method.
                Debug.LogError($"Prefab {clone.FullPath()} injections are done via fallback. " +
                                 $"Attach an InjectiblePrefab or PrefabModule to avoid a performance hit.");
                var injectibles = clone.GetComponentsInChildren<ILifecycleComponent>(true);

                if (injectibles.Length > 0)
                {
                    if (locator == null)
                    {
                        Debug.LogError($"Prefab {clone.FullPath()} has {injectibles.Length} components for injection," +
                                       $" but no locator is found or provided on PostInstantiate.");
                        return;
                    }
                }
                
                LifecycleInitializer.InitializeAll(injectibles, locator);

                //add InjectiblePrefab to store found locator
                var injectiblePrefabRuntime = clone.AddComponent<InjectiblePrefab>();
                injectiblePrefabRuntime.InstantiationLocator = locator;
                injectiblePrefabRuntime.TrueParent = trueParent;
                //we have already done the initialization
                injectiblePrefabRuntime.isInitialized = true;
            }
            else
            {
                // Populate new instance with locator
                if (prefabModule != null)
                {
                    var clonePrefabModule = clone.GetComponent<PrefabModule>();
                    clonePrefabModule.InstantiationLocator = locator;
                    clonePrefabModule.TrueParent = trueParent;
                }

                if (injectiblePrefab != null)
                {
                    var cloneInjectiblePrefab = clone.GetComponent<InjectiblePrefab>();
                    cloneInjectiblePrefab.InstantiationLocator = locator;
                    cloneInjectiblePrefab.TrueParent = trueParent;
                    
                    //that's a workaround
                    //prefab may be instantiated as not active or not active in hierarchy,
                    //in the same time external code may access prefab
                    //components while they hasn't been initialized with injections.
                    //therefor we force initialization before awake
                    //
                    //can lead to problems in case when no locator provided and desired locator is located higher in
                    //hierarchy in prefab module that is not active in hierarchy, therefor no locator is created.
                    cloneInjectiblePrefab.Initialize();
                }
            }
            
            // If a prefab was active before instantiation, we need to reactivate it, along with the clone. 
            if (reactivate)
            {
                prefab.SetActive(true);
                clone.SetActive(true);
            }
        }
        
        /**
         * For instantiation with a provided locator, we need to set it onto the prefab,
         * in case it has a MonoLifecycle component on it (module or injectible prefab).
         */
        private static void PreInstantiate(
            GameObject go,
            out bool reactivate, out bool initialize, out PrefabModule prefabModule,
            out InjectiblePrefab injectiblePrefab)
        {
            //we want to postpone Awake being called on instantiated prefab until we do our post instantiate procedure
            //therefore we make sure prefab is disabled to prevent Awake from being called
            //we reactivate prefab and instance during post instantiate in case prefab was initially active.
            var wasActive = go.activeSelf;
            if (wasActive)
            {
                go.SetActive(false);
            }

            reactivate = wasActive;
            
            if (go.TryGetComponent(out prefabModule))
            {
                injectiblePrefab = null;
                initialize = false;
                // we don't care about injectible prefab as prefab module will take care of it 
                return;
            }

            if (go.TryGetComponent(out injectiblePrefab))
            {
                initialize = false;
                prefabModule = null;
                return;
            }
            
            initialize = true;
        }

        public static T Instantiate<T>(T prefab, LocatorSource locatorSource) where T : MonoBehaviour
        {   
            var go = prefab.gameObject;
            PreInstantiate(go, out var reactivate, out var initialize, out var prefabModule, out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab);
            PostInstantiate(go, clone.gameObject, locatorSource, reactivate, initialize, prefabModule,
                injectiblePrefab);
            return clone;
        }

        public static T Instantiate<T>(GameObject prefab, LocatorSource locatorSource) where T : MonoBehaviour
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab);
            PostInstantiate(prefab, clone, locatorSource, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone.GetComponent<T>();
        }

        public static T Instantiate<T>(T prefab, Transform parent, LocatorSource locatorSource) where T : MonoBehaviour
        {   
            var go = prefab.gameObject;
            PreInstantiate(go, out var reactivate, out var initialize, out var prefabModule, out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent);
            PostInstantiate(go, clone.gameObject, locatorSource, reactivate, initialize, prefabModule,
                injectiblePrefab);
            return clone;
        }

        public static T Instantiate<T>(GameObject prefab, Transform parent, LocatorSource locatorSource) where T : MonoBehaviour
        {   
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent);
            PostInstantiate(prefab, clone, locatorSource, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone.GetComponent<T>();
        }

        public static T Instantiate<T>(T prefab, Transform parent, bool instantiateInWorldSpace, LocatorSource locatorSource)
            where T : MonoBehaviour
        {
            var go = prefab.gameObject;
            PreInstantiate(go, out var reactivate, out var initialize, out var prefabModule, out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent, instantiateInWorldSpace);
            PostInstantiate(go, clone.gameObject, locatorSource, reactivate, initialize, prefabModule,
                injectiblePrefab);
            return clone;
        }

        public static T Instantiate<T>(GameObject prefab, Transform parent, bool instantiateInWorldSpace,
            LocatorSource locatorSource)
            where T : MonoBehaviour
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent, instantiateInWorldSpace);
            PostInstantiate(prefab, clone, locatorSource, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone.GetComponent<T>();
        }

        public static T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation, LocatorSource locatorSource)
            where T : MonoBehaviour
        {   
            var go = prefab.gameObject;
            PreInstantiate(go, out var reactivate, out var initialize, out var prefabModule, out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation);
            PostInstantiate(go, clone.gameObject, locatorSource, reactivate, initialize, prefabModule,
                injectiblePrefab);
            return clone;
        }

        public static T Instantiate<T>(GameObject prefab, Vector3 position, Quaternion rotation, LocatorSource locatorSource)
            where T : MonoBehaviour
        {   
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation);
            PostInstantiate(prefab, clone, locatorSource, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone.GetComponent<T>();
        }

        public static T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent,
            LocatorSource locatorSource)
            where T : MonoBehaviour
        {
            var go = prefab.gameObject;
            PreInstantiate(go, out var reactivate, out var initialize, out var prefabModule, out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation, parent);
            PostInstantiate(go, clone.gameObject, locatorSource, reactivate, initialize, prefabModule,
                injectiblePrefab);
            return clone;
        }

        public static T Instantiate<T>(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent,
            LocatorSource locatorSource)
            where T : MonoBehaviour
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation, parent);
            PostInstantiate(prefab, clone, locatorSource, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone.GetComponent<T>();
        }

        public static GameObject Instantiate(GameObject prefab, LocatorSource locatorSource)
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab);
            PostInstantiate(prefab, clone, locatorSource, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone;
        }

        public static GameObject Instantiate(GameObject prefab, Transform parent, LocatorSource locatorSource)
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent);
            PostInstantiate(prefab, clone, locatorSource, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone;
        }

        public static GameObject Instantiate(GameObject prefab, Transform parent, bool instantiateInWorldSpace,
            LocatorSource locatorSource)
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent, instantiateInWorldSpace);
            PostInstantiate(prefab, clone, locatorSource, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone;
        }

        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, LocatorSource locatorSource)
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation);
            PostInstantiate(prefab, clone, locatorSource, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone;
        }

        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent,
            LocatorSource locatorSource)
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation, parent);
            PostInstantiate(prefab, clone, locatorSource, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone;
        }

        public static void PostInject<T>(T instance, LocatorSource locatorSource) where T : MonoBehaviour
        {
            PreInstantiate(instance.gameObject, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            PostInstantiate(instance.gameObject, instance.gameObject, locatorSource, reactivate, initialize,
                    prefabModule, injectiblePrefab);            
        }

        #region overloads with default locator set to FromScene
        public static T Instantiate<T>(T prefab) where T : MonoBehaviour
        {   
            var go = prefab.gameObject;
            PreInstantiate(go, out var reactivate, out var initialize, out var prefabModule, out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab);
            PostInstantiate(go, clone.gameObject, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule,
                injectiblePrefab);
            return clone;
        }

        public static T Instantiate<T>(GameObject prefab) where T : MonoBehaviour
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab);
            PostInstantiate(prefab, clone, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone.GetComponent<T>();
        }

        public static T Instantiate<T>(T prefab, Transform parent) where T : MonoBehaviour
        {   
            var go = prefab.gameObject;
            PreInstantiate(go, out var reactivate, out var initialize, out var prefabModule, out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent);
            PostInstantiate(go, clone.gameObject, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule,
                injectiblePrefab);
            return clone;
        }

        public static T Instantiate<T>(GameObject prefab, Transform parent) where T : MonoBehaviour
        {   
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent);
            PostInstantiate(prefab, clone, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone.GetComponent<T>();
        }

        public static T Instantiate<T>(T prefab, Transform parent, bool instantiateInWorldSpace)
            where T : MonoBehaviour
        {
            var go = prefab.gameObject;
            PreInstantiate(go, out var reactivate, out var initialize, out var prefabModule, out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent, instantiateInWorldSpace);
            PostInstantiate(go, clone.gameObject, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule,
                injectiblePrefab);
            return clone;
        }

        public static T Instantiate<T>(GameObject prefab, Transform parent, bool instantiateInWorldSpace)
            where T : MonoBehaviour
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent, instantiateInWorldSpace);
            PostInstantiate(prefab, clone, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone.GetComponent<T>();
        }

        public static T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation)
            where T : MonoBehaviour
        {   
            var go = prefab.gameObject;
            PreInstantiate(go, out var reactivate, out var initialize, out var prefabModule, out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation);
            PostInstantiate(go, clone.gameObject, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule,
                injectiblePrefab);
            return clone;
        }

        public static T Instantiate<T>(GameObject prefab, Vector3 position, Quaternion rotation)
            where T : MonoBehaviour
        {   
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation);
            PostInstantiate(prefab, clone, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone.GetComponent<T>();
        }

        public static T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent)
            where T : MonoBehaviour
        {
            var go = prefab.gameObject;
            PreInstantiate(go, out var reactivate, out var initialize, out var prefabModule, out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation, parent);
            PostInstantiate(go, clone.gameObject, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule,
                injectiblePrefab);
            return clone;
        }

        public static T Instantiate<T>(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
            where T : MonoBehaviour
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation, parent);
            PostInstantiate(prefab, clone, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone.GetComponent<T>();
        }

        public static GameObject Instantiate(GameObject prefab)
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab);
            PostInstantiate(prefab, clone, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone;
        }

        public static GameObject Instantiate(GameObject prefab, Transform parent)
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent);
            PostInstantiate(prefab, clone, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone;
        }

        public static GameObject Instantiate(GameObject prefab, Transform parent, bool instantiateInWorldSpace)
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, parent, instantiateInWorldSpace);
            PostInstantiate(prefab, clone, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone;
        }

        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation);
            PostInstantiate(prefab, clone, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone;
        }

        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            PreInstantiate(prefab, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            var clone = GameObject.Instantiate(prefab, position, rotation, parent);
            PostInstantiate(prefab, clone, LocatorSource.FromCurrentScene, reactivate, initialize, prefabModule, injectiblePrefab);
            return clone;
        }

        public static void PostInject<T>(T instance) where T : MonoBehaviour
        {
            PreInstantiate(instance.gameObject, out var reactivate, out var initialize, out var prefabModule,
                out var injectiblePrefab);
            PostInstantiate(instance.gameObject, instance.gameObject, LocatorSource.FromCurrentScene, reactivate, initialize,
                    prefabModule, injectiblePrefab);            
        }
        

        #endregion

        public static InjectiblePrefab AttachToGoAndSetReferences(GameObject go, ILifecycleComponent[] lifecycleComponents)
        {
            
            // Prevent Awake from firing on AddComponent.
            var cachedActive = go.activeSelf;
            go.SetActive(false);
            var ip = go.AddComponent<InjectiblePrefab>();
            var mbs = lifecycleComponents.OfType<MonoBehaviour>().ToArray();
            
            if (mbs.Length != lifecycleComponents.Length)
            {
                var notMb = lifecycleComponents
                    .Where(lc => lc is not MonoBehaviour)
                    .Select(lc => lc.GetType().Name)
                    .Aggregate("", (accum, element) => accum + element + "; ");
                Debug.LogError($"You re trying to add InjectiblePrefab to {go.FullPath()} but not all of provided " +
                                 $"lifecycle components are monobehaviours: {notMb}");
            }

            if (!go.SceneLocatorLookup(out var locator))
            {
                Debug.LogError($"You re trying to add InjectiblePrefab to {go.FullPath()} but scene locator can not be found");
            }
            ip.lifecycleComponents = mbs;
            ip.InstantiationLocator = locator;
            go.SetActive(cachedActive);
            return ip;
        }
    }
}

