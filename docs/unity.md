# DICS — Unity Usage

This guide assumes you have read [core.md](./core.md). All DICS core
concepts (modules, bindings, locators, factories, lifecycles, axes) work
identically inside Unity; this page covers the helpers in `DICS.Unity`
that bridge them to Unity's component lifecycle.

## Project layout

The Unity package lives in `DICS.Unity` with its own `.asmdef`. It depends
on `DICS` and on `UnityEngine`. Import both as packages, or drop the
folders into `Packages/` or `Assets/`.

## The big picture

```
GameObject (root of a scene)
├── MonoModule  (your subclass)            — Awake() builds Plan + Locator
│   ├── lifecycleObjects: MonoLifecycle    — Inspector-set holder of
│   │   ├── lifecycleComponents[]            ILifecycleComponent MonoBehaviours
│   ├── externalMonoModules: ExternalMonoModule[] — siblings that contribute bindings
│   └── otherModules                       — code-added Module instances
└── …other GameObjects under this scene root
```

`MonoModule` is your entry point. You subclass it, override `Make()` to fill
the `module` field, and optionally override `RequiredRoots()` to name the keys
that must be produced when the scene starts.

## A minimal scene module

```csharp
using UnityEngine;
using DICS;
using DICS.Unity;

public sealed class GameSceneModule : MonoModule
{
    [SerializeField] private GameConfig config;

    protected override void Make()
    {
        Make<GameConfig>().From().Instance(config);

        Make<IClock>().From().Functoid(IFunctoid.Lift(() => SystemClock.Instance));

        Make<EnemySpawner>().From().Lifecycle(
            IFunctoid.Lift(() => new EnemySpawner()),
            EnemySpawner.LiftInitializer()
        );
    }

    protected override System.Collections.Generic.ISet<Key> RequiredRoots()
        => new System.Collections.Generic.HashSet<Key> { Key.Of<EnemySpawner>() };
}
```

In the Inspector:

1. Attach `GameSceneModule` to the scene root.
2. Attach a `MonoLifecycle` somewhere in the hierarchy and wire it into
   `lifecycleObjects`. Its `lifecycleComponents[]` array should reference every
   `MonoBehaviour` that implements `ILifecycleComponent`.
3. Optionally drop `ExternalMonoModule` GameObjects into `externalMonoModules[]`
   to compose bindings from other scenes/prefabs.

When the scene loads:

1. `MonoModule.Awake()` builds the `ImmutableModule[]`, constructs an
   `Injector`, and calls `Produce`.
2. Every `ILifecycleComponent` in `lifecycleObjects.lifecycleComponents` is
   initialized — fields tagged `[Inject]` are filled in from the locator.
3. `IList<ITickable>` / `IList<ILateTickable>` autosets are cached so DICS
   can drive `Update` / `LateUpdate` for you.
4. `IList<IDisposable>` is collected; on `OnDestroy` everything is disposed
   in reverse order and the scene's `CancellationToken` is cancelled.

## Injectable `MonoBehaviour`s

```csharp
[LiftInitializer]
public partial class HealthBar : MonoBehaviour, ILifecycleComponent
{
    [Inject] protected Player Player = null!;
    [SerializeField] private UnityEngine.UI.Slider slider;

    // Initialize(...) is generated. It is called by MonoLifecycle on scene start.
    private void Update()
    {
        slider.value = Player.HealthFraction;
    }
}
```

For tickables and late-tickables:

```csharp
public class Enemy : MonoBehaviour, ITickable, ILateTickable
{
    public void Tick()      { /* runs every Update    */ }
    public void LateTick()  { /* runs every LateUpdate */ }
}
```

Bind it into the autoset:

```csharp
Make<Enemy>().From().Functoid(...).AddToSetOf<ITickable>().AddToSetOf<ILateTickable>();
```

`MonoModule` does the rest.

## Cancellation

Every `MonoModule` instance owns a `CancellationTokenSource` that is cancelled
in `OnDestroy`. The token is registered under
`Key.Of<CancellationToken>(gameObject.scene.name)`, so any binding can request
it:

```csharp
Make<NetworkClient>().From().Lifecycle(
    IFunctoid.Lift(() => new NetworkClient()),
    // `names` aligns the LOCATOR-RESOLVED parameters to named keys, in order.
    // `self` and `Sig` are NOT locator keys, so they are not counted: here the
    // only locator-resolved parameter is `CancellationToken`, and we want to
    // bind it to Key.Of<CancellationToken>("MyScene") — the scene-scoped token
    // MonoModule publishes under the scene's name.
    IInitializer.Lift((NetworkClient self, Sig _, CancellationToken ct) =>
        self.AttachLifetime(ct), names: new[] { "MyScene" })
);
```

For async initialization scoped to the scene, see [async.md](./async.md).

## Roles, in one diagram

```
                          ┌──────────────────────────────────────────┐
                          │ MonoModule  (owns Injector + Locator)    │
                          │  ─ provides bindings                     │
                          │  ─ drives ticks / disposables            │
                          └──────────────────────────────────────────┘
                                          ▲
                                          │ resolves dependencies from
                                          │
       ┌──────────────────────────────────┴──────────────────────────────────┐
       │                                                                     │
┌────────────────┐                                                  ┌────────────────┐
│ InjectiblePrefab│  authoring-time index of every                  │ InjectibleScene│
│                 │  ILifecycleComponent inside a prefab            │                │
└────────────────┘                                                  └────────────────┘
       │
       │  for prefabs that need their OWN bindings:
       ▼
┌────────────────┐
│ PrefabModule   │  (subclass of MonoModule on a prefab root)
└────────────────┘
```

`MonoModule` is the *producer* of dependencies. `InjectiblePrefab` and
`InjectibleScene` are *consumer-side* optimisations: each one carries a
pre-collected list of `ILifecycleComponent`s for its scope, so injection at
runtime is a flat for-loop instead of a recursive `GetComponentsInChildren`.

## Prefab injection

`InjectiblePrefab` is broader than the term "marker" suggests. It is a
serialized index of every `ILifecycleComponent` in the prefab's hierarchy:

- **Auto-populated at save time.** When a `.prefab` containing an
  `InjectiblePrefab` is saved, `PrefabProcessor` (driven by
  `ContentModificationProcessor.OnWillSaveAssets`) walks the entire prefab,
  finds every `MonoBehaviour` decorated with `[LiftInitializer]` (and
  implementing `ILifecycleComponent`), and writes them into
  `InjectiblePrefab.lifecycleComponents[]`. It also strips any nested
  `InjectiblePrefab` components from the hierarchy — only the root keeps one.
- **No runtime traversal.** At instantiation time, DICS iterates the
  pre-saved array. There is no recursive component scan, no reflection.
- **Fallback is loud.** If the array is missing, contains null entries, or no
  `InjectiblePrefab` exists on the prefab root, DICS falls back to a runtime
  `GetComponentsInChildren<ILifecycleComponent>(true)` scan and logs an
  *error* (not a warning) — the fallback is slower and considered a bug.

There is a `ContentPreprocessor : IPreprocessBuild` hook reserved for
re-running this processing during a Unity build. The intent is to catch cases
where a parent prefab changed but child prefabs were not re-saved, so the
list of injectable components drifted out of date. The scaffold is in place;
the build-time pass itself is a TODO in the current codebase.

### How to instantiate

Always go through `InjectiblePrefab.Instantiate(...)` (a drop-in replacement
for `GameObject.Instantiate`), passing a `LocatorSource`:

```csharp
// 1. Use the locator of the current scene's SceneModule:
var go = InjectiblePrefab.Instantiate(prefab);                 // FromCurrentScene

// 2. Use a specific locator (e.g. one you got from a parent MonoModule):
var go = InjectiblePrefab.Instantiate(prefab, LocatorSource.FromReference(loc));

// 3. Use the locator that's reachable by walking up from a "true parent" — the
//    logical owner of the new instance, which may be a different GameObject
//    than its transform parent. See "Locator resolution" below.
var go = InjectiblePrefab.Instantiate(prefab, parentTransform,
                                      LocatorSource.FromParent(logicalOwner));
```

Calling plain `GameObject.Instantiate(prefab)` is supported but logs an error
and falls back to the runtime scan.

### `PrefabModule`: prefabs that bring their own bindings

If a prefab needs its *own* bindings (not just consume the parent's), put a
`PrefabModule` on its root in addition to `InjectiblePrefab`. `PrefabModule`
is a `MonoModule` subclass: it produces a child locator inherited from
whichever locator the `LocatorSource` resolves to. `InjectiblePrefab` defers
to `PrefabModule` when both are present.

## Scene injection

`InjectibleScene` is the scene-level counterpart of `InjectiblePrefab`. It
indexes every `ILifecycleComponent` in the *whole scene* at save time, via
`SceneProcessor`. If a scene contains a `SceneModule`, the processor wires
`module.lifecycleObjects = injectibleScene` so the module skips its own
hierarchy scan on `Awake`. Nested `InjectiblePrefab` components in a
scene with a `SceneModule` are removed by the processor — the module owns
injection for the scene; the prefab-level marker is only needed for prefabs
that get *instantiated* later.

The processor also enforces invariants: at most one `SceneModule` per scene,
and it must live on a root `GameObject`.

## Locator resolution: how a prefab finds its locator

When you call `InjectiblePrefab.Instantiate(...)`, the producer side of the
graph is decided by `LocatorSource`:

| `LocatorSource`             | Resolution                                                                                                  |
| --------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `FromCurrentScene` (default)| `SceneModule.FindSceneModule(scene).Locator` — find the `SceneModule` on any root GameObject of the scene.   |
| `FromReference(locator)`    | Use the supplied `ILocator` directly. Cheapest; preferred when you already have it.                          |
| `FromParent(gameObject)`    | Walk the hierarchy upward from `gameObject` looking for `PrefabModule` or `InjectiblePrefab`; fall back to the scene's `SceneModule`. |

`HierarchyLocatorLookup` (the implementation of `FromParent`) does *not*
blindly follow `transform.parent`. Each `PrefabModule` / `InjectiblePrefab`
can carry a `TrueParent` — a "logical parent" override used when the visual
hierarchy differs from the ownership hierarchy:

```
Root
├── A
└── B           ← B.TrueParent = A    (transform parent is Root)
    ├── Child1
    └── Child2
        └── Child3   ← TrueParent = Child1
```

For `Child3`, the lookup walks `Child1 → B → A`, not the transform chain.
This is the path you want when a popup `A` opens popup `B`, but `B` is
reparented into a global "popup stack" root for layering — `B` still needs
access to `A`'s locator.

A loop in the `TrueParent` chain is detected, logged, and fallback to the
scene locator kicks in.

## Scene inheritance: `SceneModule`

`SceneModule` is a `MonoModule` subclass that lets one scene inherit from
another scene's locator. Serialize the parent scene's name into
`parentScene`:

```csharp
public sealed class GameSceneModule : SceneModule
{
    // In the Inspector, set "parentScene" to "BootstrapScene".

    protected override void Make()
    {
        Make<Player>().From().Functoid(IFunctoid.Lift(() => new Player()));
        // ... bindings that may reference anything BootstrapScene produced.
    }
}
```

At `Awake()`, `SceneModule.GetParentLocator()` looks up the named scene via
`SceneManager.GetSceneByName(parentScene)`, finds its `SceneModule`, and
returns that scene's `Locator`. The new injector then inherits from it —
plans in the child scene can reference any non-private binding from the
parent scene's locator. Bindings the parent marked `.Private()` are hidden
from the child.

This is how DICS supports the "bootstrap / world / hud" pattern in Unity:
a `BootstrapScene` is loaded additively and stays alive; gameplay scenes
declare it as their parent and reach into shared services (analytics, audio,
network) without having to re-bind them.

Notes:

- `parentScene` is a *name*, not a build-index. The scene must be loaded
  (additively or otherwise) for the lookup to succeed.
- If the parent scene unloads, the child locator's `ILocator` references
  remain valid (DICS holds object references, not weak handles), but any
  `MonoBehaviour` from the parent scene becomes a destroyed Unity object —
  expect `MissingReferenceException` if you reach into one after unload.

## Disposables

Any binding that produces an `IDisposable` gets automatically added to the
autoset `IList<IDisposable>` (when one is bound). `MonoModule` disposes the
list in reverse on `OnDestroy`. To opt in, bind your service so that its
implementation type implements `IDisposable` and DICS will pick it up via the
autoset.

## Editor measurements

In Editor builds, `MonoModule.Awake()` logs the plan + per-key timings via
`Debug.Log`. This is invaluable for spotting accidentally-expensive
constructors. The data comes from `LocatorMeta.DicsMeasurement`; you can
implement `IDicsMeasurement` and override `CreateMeasurements()` to ship the
data elsewhere.

## Tips

- The Roslyn generator runs the *same* in Unity and outside it. Your
  `[LiftInitializer]` / `[LiftConstructor]` classes work in both worlds.
- Keep `MonoBehaviour` subclasses dumb: prefer to bind a plain C# service and
  give the `MonoBehaviour` a single `[Inject]` field pointing at it.
- For scope-per-scene, layer injectors: have a `BootstrapModule` that builds a
  root locator once, then each scene's `MonoModule` overrides `GetParentLocator`
  to inherit from it.
