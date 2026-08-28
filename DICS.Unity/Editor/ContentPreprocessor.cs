#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.Build;

namespace DICS.Unity
{
    public sealed class ContentPreprocessor : IPreprocessBuild
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildTarget target, string path) {
            // TODO We need to process all prefabs and scenes before the build.
        }
    }
}

#endif