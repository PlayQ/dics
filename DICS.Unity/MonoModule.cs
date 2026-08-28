using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DICS.Unity
{
    [DefaultExecutionOrder(order: int.MinValue)]
    public abstract class MonoModule: BaseMonoModule
    {
        [SerializeField] internal MonoLifecycle lifecycleObjects;
        [SerializeField] internal ExternalMonoModule[] externalMonoModules;

        private List<Module> otherModules;
        private CancellationTokenSource cancellationTokenSource;
        private IList<ITickable> tickables;
        private IList<ILateTickable> lateTickables;
        
        /**
         * Gets cancelled whenever a MonoModule is destroyed.
         */
        protected CancellationToken cancellationToken;
        
        private sealed class PrivateModule : Module
        {
            private PrivateModule()
            {
                Make<IList<IDisposable>>().From().Auto().Private();
                Make<IList<ITickable>>().From().Auto().Private();
                Make<IList<ILateTickable>>().From().Auto().Private();
            }
            
            public static readonly ImmutableModule Instance = new PrivateModule().Freeze();
        }

        #region IMonoModule
        public ImmutableModule[] Modules { get; private set; }
        public ILocator Locator { get; private set; }
        #endregion

        private Injector injector;

        /**
         * This method can be overriden to provide a parent module.
         */
        protected virtual ILocator GetParentLocator()
        {
            return null;
        }

        //set to true in children classes in case we want let it missing on purpose
        //for example for backward compatibility with remote assets which are missing lifecycleObjects
        protected virtual bool SuppressMissingLifecycleObjectsError() => false;

        protected virtual IDicsMeasurement CreateMeasurements() => new DefaultDicsMeasurement();

        protected virtual ISet<IAxisPoint> GetConfiguration()
        {
            return new HashSet<IAxisPoint>() { };
        }

        protected virtual void Awake()
        {
            try
            {
                AwakeCore();
            }
            catch (Exception ex)
            {
                Debug.LogError($"MonoModule.Awake failed for {gameObject.name} in scene {gameObject.scene.name}: " +
                               $"{ex.GetType().Name}: {ex.Message}. Component is partially initialized.");
                throw;
            }
        }

        private void AwakeCore()
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            var modules = new List<Module>();
            otherModules = new List<Module>();
            // Create a module that will be filled in by the scene
            module = new InternalModule();
            Make();
            Make<CancellationToken>().Named(gameObject.scene.name).From().Instance(cancellationToken);

            if (externalMonoModules != null)
            {
                modules.AddRange(externalMonoModules.Where(e => e != null).Select(e => e.Module));                
            }
            modules.AddRange(otherModules);

            var immutable = modules.Select(em => em.Freeze()).ToList();
            immutable.Add(module.Freeze());
            immutable.Add(PrivateModule.Instance);
            Modules = immutable.ToArray();
            
            // Find the parent module if any
            var parentLocator = GetParentLocator();
            if (parentLocator != null)
            {
                // We have a parent scene, we need to use its modules and locator
                // immutable.AddRange(parentModule.KnowledgeModules);
                injector = new Injector(parentLocator, CreateMeasurements(), 
                    GetType().Name, Modules);
            }
            else
            {
                // We don't have a parent scene, let's create a basic setup.
                injector = new Injector(ILocator.Empty, CreateMeasurements(), 
                    GetType().Name, Modules);
            }
            
            var requiredRoots = new HashSet<Key>();
            // Add external required roots
            if (externalMonoModules != null)
            {
                foreach (var externalModule in externalMonoModules)
                {
                    requiredRoots.UnionWith(externalModule.RequiredRoots());
                }
            }
            foreach (var otherModule in otherModules)
            {
                if (otherModule is IModuleWithRoots mwr)
                {
                    requiredRoots.UnionWith(mwr.RequiredRoots());
                }
            }
            // Add our own roots
            requiredRoots.UnionWith(RequiredRoots());
            // Add magic mutable to collect disposables
            requiredRoots.Add(Key.Of<IList<IDisposable>>());
            requiredRoots.Add(Key.Of<IList<ITickable>>());
            requiredRoots.Add(Key.Of<IList<ILateTickable>>());
            requiredRoots.Add(Key.Of<CancellationToken>(gameObject.scene.name));
            
            Locator = injector.Produce(requiredRoots, GetConfiguration());

#if UNITY_EDITOR
            var meta = Locator.Get<LocatorMeta>();
            var measurements = meta.DicsMeasurement.PlanToString(meta.Plan);
            Debug.Log(measurements);
            
#endif
            // Now that we have everything prepared, we can initialize this scene
            // with necessary injections.


            IEnumerable<ILifecycleComponent> lifecycleComponents;
            
            if (lifecycleObjects == null)
            {
                string message = $"Prefab {gameObject.name} in scene {gameObject.scene} with MonoModule component " +
                                 $"has no lifecycleObjects. " +
                                 $"Fallback to find lifecycleObjects in runtime";
                if (!SuppressMissingLifecycleObjectsError())
                {
                    Debug.LogError(message);
                }
                else
                {
                    Debug.LogWarning(message);
                }
                
                lifecycleComponents = GetComponentsInChildren<ILifecycleComponent>(true).ToList();
            }
            else if (lifecycleObjects.lifecycleComponents == null
                     || lifecycleObjects.lifecycleComponents.Any(c => c == null))
            {
                Debug.LogError($"Prefab {gameObject.name} in scene {gameObject.scene} with MonoModule component " +
                               $"some of lifecycleObjects references are missing or the array is unassigned. " +
                               $"Fallback to find lifecycleObjects in runtime");
                lifecycleComponents = GetComponentsInChildren<ILifecycleComponent>(true).ToList();
            }
            else
            {
                lifecycleComponents = lifecycleObjects.lifecycleComponents.OfType<ILifecycleComponent>();
            }
            
            LifecycleInitializer.InitializeAll(lifecycleComponents, Locator);

            tickables = Locator.Get<IList<ITickable>>();
            lateTickables = Locator.Get<IList<ILateTickable>>();
        }

        protected virtual void OnDestroy()
        {
            cancellationTokenSource?.Cancel();
            tickables = null;
            lateTickables = null;
            if (Locator != null)
            {
                var disposables = Locator.Get<IList<IDisposable>>().ToList();
                disposables.Reverse();
                foreach (var disposable in disposables)
                {
                    disposable.Dispose();
                }
            }
        }

        private void Update()
        {
            if (tickables != null)
            {
                foreach (var tickable in tickables)
                {
                    tickable.Tick();
                }
            }
        }

        private void LateUpdate()
        {
            if (lateTickables != null)
            {
                foreach (var lateTickable in lateTickables)
                {
                    lateTickable.LateTick();
                }
            }
        }

        public void AddModule(Module m)
        {
            otherModules.Add(m);
        }
    }
}
