#if UNITY_EDITOR

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace DICS.Unity
{
    public class ContentModificationProcessor : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            // We want to look only at prefabs and scenes, so ignore everything else
            // Assets/States/Main/TestPrefab.prefab or .scene
            var jobs = new List<string>();
            foreach (var path in paths)
            {
                if (!path.ToLower().EndsWith(".prefab") && !path.ToLower().EndsWith(".unity")) continue;
                if (FinishedJobs.ContainsKey(path))
                {
                    FinishedJobs.Remove(path, out _);
                    continue;
                }
                jobs.Add(path);
            }

            if (jobs.Count > 0)
            {
                AddJob(jobs);
            }

            return paths;
        }
        
        private static readonly ConcurrentQueue<(string Path, DateTime LastModified)> Jobs = new ();
        private static readonly ConcurrentDictionary<string, object> FinishedJobs = new ();
        private static bool _processing;

        private static void AddJob(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                var lastModified = System.IO.File.GetLastWriteTime(path);
                Jobs.Enqueue((path, lastModified));
            }
            if (_processing) return;
            Process();
        }

        private static async void Process()
        {
            _processing = true;
            try
            {
                while (Jobs.TryDequeue(out var job))
                {
                    // Wait for the asset to be modified, because we receive callback before
                    // an asset is to be written, not after.
                    var (path, lastModified) = job;
                    var attempts = 0;
                    var attemptWait = 100;
                    var attemptsLimit = 20;
                    var failed = false;
                    while (System.IO.File.GetLastWriteTime(path) == lastModified)
                    {
                        attempts += 1;
                        if (attempts > attemptsLimit)
                        {
                            Debug.LogError($"Last modified time didn't change after {attempts * attemptWait}");
                            failed = true;
                        }
                        else
                        {
                            await Task.Delay(attemptWait);                        
                        }
                    }
                    if (failed) continue;
                    if (path.ToLower().EndsWith(".prefab"))
                    {
                        PrefabProcessor.Process(path, FinishedJobs);
                    }
                    else
                    {
                        SceneProcessor.Process(path, FinishedJobs);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            _processing = false;
        }
    }
}

#endif