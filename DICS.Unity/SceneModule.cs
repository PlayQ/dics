using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DICS.Unity
{
    /**
     * Scene module is to be used on scenes, which need to provide their own bindings.
     * Allows to specify a parent scene, in order to have access to its locator. 
     */
    [DefaultExecutionOrder(order: int.MinValue)]
    public abstract class SceneModule: MonoModule
    {
        [SerializeField] private string parentScene; 

        protected override ILocator GetParentLocator()
        {
            var sceneModule = FindSceneModule(parentScene); 
            return sceneModule != null ? sceneModule.Locator : null;
        }

        public static SceneModule FindSceneModule(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return null;
            }

            // We have a parent scene that we have to take into consideration.
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid())
            {
                Debug.LogError($"FindSceneModule failed for scene={sceneName}");
                return null;
            }

            return FindSceneModule(scene);
        } 
        
        public static SceneModule FindSceneModule(Scene scene)
        {
            SceneModule found = null;
            foreach (var rgo in scene.GetRootGameObjects())
            {
                var maybeSceneModule = rgo.GetComponent<SceneModule>();
                if (maybeSceneModule != null)
                {
                    if (found != null)
                    {
                        Debug.LogError($"Scene '{scene.name}' has more than one SceneModule on its root GameObjects; " +
                                       $"returning the first found ('{found.name}'). " +
                                       $"Fix the scene to keep behaviour deterministic.");
                        return found;
                    }
                    found = maybeSceneModule;
                }
            }
            return found;
        }
    }
}
