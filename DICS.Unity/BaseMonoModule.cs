using System;
using System.Collections.Generic;
using UnityEngine;

namespace DICS.Unity
{
    public abstract class BaseMonoModule: MonoBehaviour
    {
        protected sealed class InternalModule : Module
        {
        }
        
        protected InternalModule module;

        /**
         * Must be overriden in the derived class with necessary bindings. This would include both,
         * those that need to be instantiated, and those that are part of the module to be used later.
         */
        protected abstract void Make();

        /**
         * Must be overriden with the necessary keys which must be produced upon start of the scene module.
         */
        protected virtual ISet<Key> RequiredRoots()
        {
            return new HashSet<Key>();
        }

        protected AfterMake<T> Make<T>() where T : notnull
        {
            return module.Make<T>();
        }

        /**
         * Creates a new component factory, using provided behavior as a guide for parameters.
         */
        protected void MakeComponentFactory<T>(Sig signature, ModuleExt.ComponentFactoryBehavior behavior) where T : notnull, MonoBehaviour, ILifecycleComponent
        {
            module.MakeComponentFactory<T>(signature, behavior);
        }
        
        /**
         * Creates a new component factory, using provided behavior as a guide for parameters.
         */
        protected void MakeComponentFactory<TF, T>(IAbstractGeneratedFactoryFunctoid functoid, ModuleExt.ComponentFactoryBehavior behavior)
            where TF: IGeneratedFactory<T>
            where T : notnull, MonoBehaviour
        {
            module.MakeComponentFactory<TF, T>(functoid, behavior);
        }
    }
}
