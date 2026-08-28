using System;
using UnityEngine;

namespace DICS.Unity
{
     // true parent represents a code related hierarchy rather than transform related hierarchy
        // e.g. popup A instantiates popup B, B's true parent is A, but B may be instantiated in any place in hierarchy
        // like some popup stack root gameobject and not directly on popup A.
        // in case popup A has locator with bindings that are required by popup B, we use true parent instead of transform.parent
        // to look up higher in hierarchy for PrefabModule or InjectiblePrefab to get ILocator from them.
        // true parent is optional, it indicates we have to traverse hierarchy upwards to obtain locator.
        // Hierarchy example:
        // Root
        // |____ A
        // |____ B (true parent A, transform parent Root)
        //     |____ Child 1 (true parent null, transform parent B)
        //     |____ Child 2 (true parent null, transform parent B)
        //          |____ Child 3 (true parent Child 1, transform parent Child 2)
        // traverse route for Child 3 looks like:  Child 1 -> B -> A

        public abstract record LocatorSource
        {
            private LocatorSource(){}
        
            public sealed record CurrentScene : LocatorSource
            {
                public static CurrentScene Instance = new ();
                private CurrentScene(){}
            }

            public sealed record Reference(ILocator Locator) : LocatorSource;
            public sealed record TrueParent(GameObject Parent) : LocatorSource;
        
            public static LocatorSource FromCurrentScene => CurrentScene.Instance;
            public static LocatorSource FromReference(ILocator locator) => new Reference(locator);
            public static LocatorSource FromParent(GameObject parent) => new TrueParent(parent);
        }
}