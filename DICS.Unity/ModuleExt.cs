using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DICS.Unity
{
    public static class ModuleExt
    {
        public const string INSTANTIATION_PARENT_TAG = nameof(INSTANTIATION_PARENT_TAG);
        public enum ComponentFactoryBehavior
        {
            /**
             * Creates a new component at an existing game object.
             */
            ExistingTarget,
            /**
             * Creates a new blank game object and attaches a component to it.
             */
            CreateObjectAndAttach,
            /**
             * Creates a new game object from a prefab, then attaches a new component.
             */
            ClonePrefabAndAttach,
            /**
             * Creates a new game object from a prefab, finds the component and initializes it.
             */
            ClonePrefabAndInit
        }

        /**
         * Creates a new component factory, using provided behavior as a guide for parameters.
         */
        public static void MakeComponentFactory<TF, T>(this Module module, IAbstractGeneratedFactoryFunctoid functoid, ComponentFactoryBehavior behavior)
            where TF: IGeneratedFactory<T>
            where T : notnull, MonoBehaviour
        {
            switch (behavior)
            {
                case ComponentFactoryBehavior.ExistingTarget:
                    module.Make<TF>().From().TypedFactory().Using().Lifecycle(
                    IFunctoid.Lift((GameObject target) => target.AddComponent<T>()),
                        functoid
                    );
                    break;
                case ComponentFactoryBehavior.CreateObjectAndAttach:
                    module.Make<TF>().From().TypedFactory().Using().Lifecycle(
                        IFunctoid.Lift((Transform parent) =>
                        {
                            var target = new GameObject($"Factory#{typeof(T).Name}", typeof(T));
                            target.transform.SetParent(parent);
                            var cmp = target.GetComponent<T>();
                            return cmp;
                        }),
                        functoid
                    );
                    break;
                case ComponentFactoryBehavior.ClonePrefabAndAttach:
                case ComponentFactoryBehavior.ClonePrefabAndInit:
                    module.Make<TF>().From().TypedFactory().Using().Lifecycle(
                        IFunctoid.Lift((GameObject prefab, Transform parent) =>
                        {
                            var clone = InjectiblePrefab.Instantiate(prefab, parent);
                            return behavior == ComponentFactoryBehavior.ClonePrefabAndInit
                                ? clone.GetComponent<T>()
                                : clone.AddComponent<T>();
                        }),
                        functoid
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(behavior), behavior, null);
            }
        }
        
        /**
         * Creates a new component factory, using provided behavior as a guide for parameters.
         */
        public static void MakeComponentFactory<T>(this Module module, Sig signature, ComponentFactoryBehavior behavior) where T : notnull, MonoBehaviour, ILifecycleComponent
        {
            switch (behavior)
            {
                case ComponentFactoryBehavior.ExistingTarget:
                    module.Make<IUnsafeFactory<T>>().From().UntypedFactory().Using().Functoid(
                    (locator) =>
                        {
                            var target = locator.Get<GameObject>();
                            var cmp = target.GetComponent<T>();
                            cmp.Initialize(locator, signature);
                            return cmp;
                        },
                        signature
                    );
                    break;
                case ComponentFactoryBehavior.CreateObjectAndAttach:
                    module.Make<IUnsafeFactory<T>>().From().UntypedFactory().Using().Functoid(
                        (locator) =>
                        {
                            var parent = locator.Get<Transform>();
                            var target = new GameObject($"Factory#{typeof(T).Name}", typeof(T));
                            target.transform.SetParent(parent);
                            var cmp = target.GetComponent<T>();
                            cmp.Initialize(locator, signature);
                            return cmp;
                        },
                        signature
                    );
                    break;
                case ComponentFactoryBehavior.ClonePrefabAndAttach:
                case ComponentFactoryBehavior.ClonePrefabAndInit:
                    module.Make<IUnsafeFactory<T>>().From().UntypedFactory().Using()
                        .Functoid(CreaterFunctoid<T>(signature, behavior));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(behavior), behavior, null);
            }
        }

        public static IFunctoid CreaterFunctoid<T>(Sig signature, ComponentFactoryBehavior behavior) where T : notnull, MonoBehaviour, ILifecycleComponent
        {
            return new FunctoidFromLocator<T>((locator) =>
            {
                var prefab = locator.Get<GameObject>();
                bool? worldPositionStays = locator.TryGet<bool>(out var res) ? res : null;
                
                locator.TryResolve(out Transform parent);
                
                GameObject clone = null;
                if(locator.TryResolve(INSTANTIATION_PARENT_TAG, out GameObject maybeInstantiationParent))
                {
                    if (worldPositionStays.HasValue)
                    {
                        clone = InjectiblePrefab.Instantiate
                            (prefab, parent, worldPositionStays.Value, 
                                LocatorSource.FromParent(maybeInstantiationParent));
                    }
                    else
                    {
                        clone = InjectiblePrefab.Instantiate
                            (prefab, parent, LocatorSource.FromParent(maybeInstantiationParent));   
                    }
                }
                else
                {
                    clone =
                        worldPositionStays.HasValue
                            ? InjectiblePrefab.Instantiate(prefab, parent, worldPositionStays.Value, 
                                LocatorSource.FromReference(locator))
                            : InjectiblePrefab.Instantiate(prefab, parent, LocatorSource.FromReference(locator));    
                }
                

                if (behavior == ComponentFactoryBehavior.ClonePrefabAndInit)
                {
                    return clone.GetComponentInChildren<T>(true);
                }

                var component = clone.AddComponent<T>();
                component.Initialize(locator, signature);
                return component;
            }, signature);
        }
        
        //look up upwards in hierarchy for Locator
        //Locator can be found either in PrefabModule or InjectiblePrefab 
        //fallback to scene locator if not locator is found in hierarchy
        public static bool HierarchyLocatorLookup(this GameObject trueParent, out ILocator locator)
        {
            HashSet<Transform> traversedTransforms = new(20);
            Transform currentParent = trueParent.transform;
            bool cycleDetected = false;
            while (currentParent)
            {
                //check for potential loops, as TrueParent may be set up arbitrary
                if (!traversedTransforms.Add(currentParent))
                {
                    var debugMessage = traversedTransforms.Aggregate("",
                        (accum, transform) => $"{accum};\n {transform.gameObject.FullPath()}");
                    Debug.Log($"There is a loop in hierarchy locator lookup around objects: {debugMessage} " +
                              $"\nFallback to scene locator");
                    cycleDetected = true;
                    break;
                }
                
                var parentPrefabModule = currentParent.GetComponent<PrefabModule>();
                
                //if locator is found in PrefabModule skip InjectiblePrefabe look up as
                //PrefabModule has priority over PrefabModule
                if (parentPrefabModule)
                {
                    if (parentPrefabModule.Locator != null)
                    {
                        locator = parentPrefabModule.Locator;
                        return true;
                    }

                    //if InstantiationParent field is set use it as parent for traversing instead of regular transform.parent
                    if (parentPrefabModule.TrueParent)
                    {
                        currentParent = parentPrefabModule.TrueParent.transform;
                    }
                    else
                    {
                        currentParent = currentParent.parent;
                    }
                    
                    //if there is no locator in PrefabModule it also can't be in InjectiblePrefabe
                    //keep search in parents
                    continue;
                }
                
                var parentInjectiblePrefab = currentParent.GetComponent<InjectiblePrefab>();
                if (parentInjectiblePrefab)
                {
                    if (parentInjectiblePrefab.InstantiationLocator != null)
                    {
                        locator = parentInjectiblePrefab.InstantiationLocator;
                        return true;
                    }

                    //if InstantiationParent field is set use it as parent for traversing instead of regular transform.parent
                    if (parentInjectiblePrefab.TrueParent)
                    {
                        currentParent = parentInjectiblePrefab.TrueParent.transform;
                    }
                    else
                    {
                        currentParent = currentParent.parent;
                    }

                    continue;
                }

                currentParent = currentParent.parent;
            }
            
            if (!cycleDetected)
            {
                Debug.Log($"Prefab {trueParent.FullPath()}: hierarchy traversed, " +
                          $"no locator found; falling back to scene locator");
            }

            return trueParent.SceneLocatorLookup(out locator);
        }

        public static bool SceneLocatorLookup(this GameObject go, out ILocator locator)
        {
            var sm = SceneModule.FindSceneModule(go.scene);
            locator = sm != null ? sm.Locator : null;
            return locator != null;
        }

        public static string FullPath(this GameObject go)
        {
            StringBuilder sb = new StringBuilder(500);
            Stack<string> stack = new Stack<string>(50);
            stack.Push(go.name);
            
            Transform parent = go.transform.parent;
            while (parent)
            {
                stack.Push("/");
                stack.Push(parent.name);
                parent = parent.parent;
            }
            stack.Push("Path: ");

            if (!string.IsNullOrEmpty(go.scene.name))
            {
                stack.Push("; ");
                stack.Push(go.scene.name);
                stack.Push("Scene: ");
            }

            while (stack.TryPop(out string result))
            {
                sb.Append(result);
            }

            return sb.ToString();
        }
        
    }
}
