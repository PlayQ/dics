using System;
using System.Collections.Generic;
using UnityEngine;

namespace DICS.Unity
{
    internal static class LifecycleInitializer
    {
        /// <summary>
        /// Initializes every lifecycle component in isolation: a failure in one component
        /// (in either MakeSignature or Initialize) is logged with the culprit's identity and
        /// does not prevent the remaining components from being initialized. After the whole
        /// set has been attempted, if any component failed an AggregateException is thrown so
        /// the caller never proceeds on a half-initialized locator.
        /// </summary>
        public static void InitializeAll(IEnumerable<ILifecycleComponent> components, ILocator locator)
        {
            List<Exception> failures = null;

            foreach (var lcc in components)
            {
                try
                {
                    lcc.Initialize(locator, lcc.MakeSignature());
                }
                catch (Exception e)
                {
                    Debug.LogError($"Lifecycle initialization failed for {Describe(lcc)}: {e}");
                    (failures ??= new List<Exception>()).Add(e);
                }
            }

            if (failures != null)
            {
                throw new AggregateException(
                    $"{failures.Count} lifecycle component(s) failed to initialize. See logged errors above for each culprit.",
                    failures);
            }
        }

        private static string Describe(ILifecycleComponent lcc)
        {
            var typeName = lcc.GetType().Name;
            if (lcc is Component component)
            {
                return $"{typeName} on {component.gameObject.FullPath()}";
            }

            return typeName;
        }
    }
}
