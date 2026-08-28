using System.Collections.Generic;
using UnityEngine;

namespace DICS.Unity
{
    public abstract class MonoLifecycle : MonoBehaviour
    {
        [SerializeField] internal MonoBehaviour[] lifecycleComponents;

        public void Initialize(ILocator locator)
        {
            var components = new List<ILifecycleComponent>(lifecycleComponents.Length);
            foreach (var mb in lifecycleComponents)
            {
                components.Add((ILifecycleComponent) mb);
            }
            LifecycleInitializer.InitializeAll(components, locator);
        }
    }
}
