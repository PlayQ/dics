#if UNITY_EDITOR

using System.Collections.Concurrent;
using System.Collections.Generic;
using DICS.Attribute;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DICS.Unity
{
    public static class SceneProcessor
    {
        public static void Process(string path, ConcurrentDictionary<string, object> finishedJobs)
        {
            var scene = EditorSceneManager.GetSceneByPath(path);
            var unloadAfter = false;
            if (!scene.IsValid())
            {
                unloadAfter = true;
                EditorSceneManager.OpenScene(path);
                scene = EditorSceneManager.GetSceneByPath(path);
            }
            
            // 1. Find all of the components in the scene which have an initializer
            var injectibles = new List<MonoBehaviour>();
            SceneModule module = null;
            InjectibleScene injectibleScene = null;
            var rootObjects = scene.GetRootGameObjects();
            foreach (var rgo in rootObjects)
            {
                foreach (var mb in rgo.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || !mb.GetType().IsDefined(typeof(LiftInitializer), false)) continue;
                    if (mb is not ILifecycleComponent)
                    {
                        Debug.LogError($"DICS:SceneProcessor Component {mb.GetType().FullName} has a LiftInitializer, but does not implement ILifecycleComponent at {scene.name}#{mb.transform.name}");
                    }
                    
                    injectibles.Add(mb);
                }

                if (injectibleScene == null)
                {
                    var i = rgo.GetComponentsInChildren<InjectibleScene>(true);
                    if (i.Length > 1)
                    {
                        Debug.LogError("You have more than one InjectibleScene in the scene");
                    } else if (i.Length == 1)
                    {
                        injectibleScene = i[0];
                    }
                }

                if (module == null)
                {
                    var i = rgo.GetComponents<SceneModule>();
                    if (i.Length > 1)
                    {
                        Debug.LogError("You have more than one SceneModule in the scene");
                    } else if (i.Length == 1)
                    {
                        module = i[0];
                    }
                }
            }

            if (injectibleScene == null)
            {
                // Let's create an injectible scene
                var instance = new GameObject("SceneInjections", typeof(InjectibleScene));
                injectibleScene = instance.GetComponent<InjectibleScene>();
                if (instance.scene != scene)
                {
                    SceneManager.MoveGameObjectToScene(instance, scene);                    
                }
            }
            
            // 2. Now we need to populate our scene with initializers.
            injectibleScene.lifecycleComponents = injectibles.ToArray();
            
            // 3. If a scene has a module, we need to link our initializer to avoid doing a search of those initializers.
            if (module != null)
            {
                module.transform.SetAsFirstSibling();
                module.lifecycleObjects = injectibleScene;
                
                // Remove InjectiblePrefabs components if any. Those might be occasionally put into scene with prefabs,
                // that are used to be instantiated in runtime. MonoModule is handling all injections.
                foreach (var rgo in rootObjects)
                {
                    foreach (var ip in rgo.GetComponentsInChildren<InjectiblePrefab>(true))
                    {
                        Debug.Log($"DICS:SceneProcessor remove InjectiblePrefab component for " +
                                  $"gameobject: {ip.gameObject.FullPath()} for scene: {path}");
                        Object.DestroyImmediate(ip, true);
                    }
                }
            }
            else
            {
                // Validate the setup, there should be no nested scene modules
                foreach (var rgo in rootObjects)
                {
                    var i = rgo.GetComponentsInChildren<SceneModule>(true);
                    if (i.Length > 0)
                    {
                        Debug.LogError($"You have a SceneModule in the scene ({i[0].name}), but not in the root.");
                    }                    
                }
            }
            
            // 4. Make sure there are no more than one scene module.
            var modules = 0;
            foreach (var rgo in rootObjects)
            {
                var i = rgo.GetComponentsInChildren<SceneModule>(true);
                if (i.Length > 0)
                {
                    modules += i.Length;
                }                    
            }
            if (modules > 1)
            {
                Debug.LogError("You have more than one SceneModule in the scene.");
            }

            // 5. We can now save all this
            finishedJobs.TryAdd(path, null);
            EditorSceneManager.SaveScene(scene);

            if (unloadAfter)
            {
                SceneManager.UnloadSceneAsync(scene);                
            }
        }
    }
}

#endif