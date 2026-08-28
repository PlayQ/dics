#if UNITY_EDITOR

using System.Collections.Concurrent;
using System.Collections.Generic;
using DICS.Attribute;
using UnityEngine;
using UnityEditor;

namespace DICS.Unity
{
    public static class PrefabProcessor
    {
        public static void Process(string path, ConcurrentDictionary<string, object> finishedJobs)
        {
            // Load this prefab and at the root, check that it has the
            // InjectiblePrefab. If it doesn't, we are not interested in using
            // this prefab with injections.

            var instance = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (instance.GetComponent<InjectiblePrefab>() == null)
            {
                return;
            }

            var cmp = instance.GetComponent<InjectiblePrefab>();
                
            // We need to collect all injections within the children
            var module = instance.GetComponent<PrefabModule>();
            var children = instance.GetComponentsInChildren<MonoBehaviour>(true);
            var injectibles = new List<MonoBehaviour>();
            foreach (var mb in children)
            {
                if(!mb)
                {
                    Debug.LogError($"DICS:PrefabProcessor prefab {path} has some missing monobehaviours.");
                    continue;
                }
                if (!mb.GetType().IsDefined(typeof(LiftInitializer), false)) continue;
                if (mb is not ILifecycleComponent)
                {
                    Debug.LogError($"DICS:PrefabProcessor Component {mb.GetType().FullName} has a LiftInitializer, but does not implement ILifecycleComponent at {path}");
                }
                injectibles.Add(mb);
            }
            
            // For all nested InjectiblePrefabs - we want to remove the whole component there
            foreach (var ip in instance.GetComponentsInChildren<InjectiblePrefab>(true))
            {
                if (instance.gameObject == ip.gameObject)
                {
                    // Don't destroy InjectiblePrefab component on root.
                    continue;
                }

                Debug.Log($"DICS:PrefabProcessor: found InjectiblePrefab component in hierarchy, " +
                                 $"removed for gameobject: {ip.gameObject.FullPath()}; " +
                                 $"for prefab: {path}");
                Object.DestroyImmediate(ip, true);
            }

            // In the injectible prefab
            cmp.lifecycleComponents = injectibles.ToArray();

            // If we have a sub module defined for this prefab, set its lifecycle objects management
            if (module != null)
            {
                module.lifecycleObjects = cmp;
                cmp.enabled = false;
            }
            else
            {
                cmp.enabled = true;
            }

            finishedJobs.TryAdd(path, null);
            EditorUtility.SetDirty(instance);
            PrefabUtility.SavePrefabAsset(instance);
        }
    }
}

#endif