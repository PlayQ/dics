# DICS — Async Usage

DICS supports asynchronous construction and initialization. The `Plan` itself
is computed synchronously (planning is cheap and pure), but executing a plan
that contains any `Task`-returning operation must go through `AsyncProducer`
via `Injector.ProduceAsync(...)`.

## When to use async

Reach for async bindings when an object cannot be constructed without `await`:

- loading a config blob from disk / network,
- opening a database connection,
- starting a background socket or warming a cache,
- any I/O bound work that would block startup if done synchronously.

For purely CPU-bound construction, stay synchronous.

## The big picture

```
        ┌──── all-sync plan ────▶ Injector.Produce      ──▶ ILocator
Plan ──▶┤
        └─── has async ops ─────▶ Injector.ProduceAsync ──▶ Task<ILocator>
```

A plan that contains *any* async instruction will fail in the synchronous
`Producer` with a `DicsProducerException`. The async producer handles both
sync and async instructions transparently, so mixing them in one module is
fine — even encouraged.

## Producer API

```csharp
Task<ILocator> ProduceAsync(Plan plan, CancellationToken ct = default);
Task<ILocator> ProduceAsync(CancellationToken ct, params Key[] roots);
Task<ILocator> ProduceAsync(ISet<Key> roots, ISet<IAxisPoint> config,
                            CancellationToken ct = default);
```

The cancellation token is forwarded into every async functoid and async
initializer. See [Failure semantics](#failure-semantics) below for how the
async producer drains in-flight work and aggregates exceptions on failure.

The `params`-`Key[]` overload is a shortcut. Like its sync counterpart, it is
equivalent to building a `Plan` first and then executing it. Use the two-step
form when you want to inspect or print the plan (or its async-instruction
content) before running it:

```csharp
var plan    = injector.Plan(Key.Of<Repository>());     // synchronous, cheap
Console.WriteLine(plan);                               // inspect / log
var locator = await injector.ProduceAsync(plan, ct);   // execute
```

The `ProduceAsync(ISet<Key>, ISet<IAxisPoint>, CancellationToken)` overload is
the same idea with explicit activation axes, mirroring the sync
`Produce(ISet<Key>, ISet<IAxisPoint>)` shape from
[core.md](./core.md#configuration-activation-axes).

## Parallel evaluation

`AsyncProducer` runs **independent** ready keys concurrently. A key becomes
ready as soon as the last of its dependencies completes; at that moment it is
scheduled on the .NET thread pool.

The producer relies on `LocatorImpl` being a `ConcurrentDictionary` underneath.
Your async functoids and initializers **must not mutate shared external state**
without their own synchronization — DICS does not lock around your code.

Example: two independent ~200 ms async constructors complete in ~200 ms total,
not 400 ms. This is exercised by `IndependentTasks_RunInParallel` in the test
suite.

## Async functoids (the constructor side)

`IAsyncFunctoid` is the async counterpart of `IFunctoid`. The DSL exposes it
through `.AsyncFunctoid(...)` and through `IAsyncFunctoid.Lift(...)`:

```csharp
public class ConfigLoader
{
    public ConfigLoader(string blob) { /* ... */ }
}

Make<ConfigLoader>().From().AsyncFunctoid(
    IAsyncFunctoid.Lift(async (IFileSystem fs, CancellationToken ct) =>
    {
        var blob = await fs.ReadAllText("config.json", ct);
        return new ConfigLoader(blob);
    })
);
```

`Lift` overloads exist for arities 0..7. The last lambda parameter is always
`CancellationToken`. Ignore it if you do not need it.

When the lambda return type cannot be inferred (rare; usually only when the
body throws unconditionally), specify it explicitly:
`IAsyncFunctoid.Lift<int>(async ct => { … throw … })`.

## Async initializers (the lifecycle side)

`IAsyncInitializer` mirrors `IInitializer`. Bind a (sync extractor, async
initializer) lifecycle with `.AsyncLifecycle(...)`:

```csharp
public class Repository
{
    public IDbConnection? Conn;
    public Task OpenAsync(IDbProvider p, CancellationToken ct) =>
        p.Open(out Conn!, ct);
}

Make<Repository>().From().AsyncLifecycle(
    IFunctoid.Lift(() => new Repository()),
    IAsyncInitializer.Lift(
        async (Repository self, Sig _, IDbProvider p, CancellationToken ct) =>
            await self.OpenAsync(p, ct))
);
```

If you also need the construction step to be async:

```csharp
Make<Repository>().From().AsyncLifecycle(
    extractor:   IAsyncFunctoid.Lift(async (CancellationToken ct) => {
                     await Task.Yield();
                     return new Repository();
                 }),
    initializer: IAsyncInitializer.Lift(async (Repository self, Sig _, CancellationToken ct) =>
                     await self.OpenAsync(default!, ct))
);
```

## Generator support

Async lifting is **opt-in by convention**, not by attribute: the same
`[LiftConstructor]` and `[LiftInitializer]` you use for the sync path detect
async-shaped members and emit additional factory methods for them.

### Async constructor — `static Task<T> CreateAsync(...)` under `[LiftConstructor]`

If a class tagged `[LiftConstructor]` defines a
`public static Task<TYPE> CreateAsync(..., CancellationToken)` method, the
generator emits `LiftAsync()` returning `IAsyncFunctoid` **in addition to**
the regular `Lift()`.

```csharp
[LiftConstructor]
public partial class Cache
{
    private Cache(byte[] bytes) { /* ... */ }

    public static async Task<Cache> CreateAsync(IFileSystem fs, CancellationToken ct)
    {
        var bytes = await fs.ReadAllBytes("cache.bin", ct);
        return new Cache(bytes);
    }
}

Make<Cache>().From().AsyncFunctoid(Cache.LiftAsync());
```

Requirements:
- The class must be `partial`.
- The static method must be named `CreateAsync`.
- Its last parameter must be `CancellationToken`, else `INJ021`.
- It must return an awaitable of the class itself — `Task<Self>`, `ValueTask<Self>`,
  or a task of a subclass — else `INJ025`. A `void`, non-generic `Task`, or unrelated
  result is rejected and the class is lifted synchronously only.
- Use `[Id("name")]` on parameters to bind to named keys.

If you want async-only construction, make the regular constructor `private`.
`Lift()` will still be generated but is harmless if you do not bind it.

### Async initializer — `Task InitializeAsync(CancellationToken)` under `[LiftInitializer]`

If a class tagged `[LiftInitializer]` defines an instance method
`public Task InitializeAsync(CancellationToken ct)`, the generator emits:

- the regular `Initialize(ILocator, Sig)` (sync field injection),
- an `IAsyncLifecycleComponent.Initialize(ILocator, Sig, CancellationToken)`
  that runs the sync field injection then calls `InitializeAsync(ct)`,
- a static `LiftAsyncInitializer()` returning `IAsyncInitializer`.

The signature is enforced: an `InitializeAsync` that does not take exactly one
`CancellationToken` is rejected with `INJ023`, and one whose return type is not
assignable to `Task` (`void`, `ValueTask`) with `INJ024`. Either way the class is
lifted synchronously only, instead of emitting an async block that cannot compile.

```csharp
[LiftInitializer]
public partial class Repository
{
    [Inject] protected IDbProvider Provider = null!;
    public IDbConnection? Conn;

    public async Task InitializeAsync(CancellationToken ct)
        => await Provider.Open(out Conn!, ct);
}

Make<Repository>().From().AsyncLifecycle(
    IFunctoid.Lift(() => new Repository()),
    Repository.LiftAsyncInitializer()
);
```

The sync `LiftInitializer()` is also emitted, so the same class can be bound
synchronously via `.Lifecycle(...)` if you do not need the async step in some
contexts.

## Cancellation patterns

The cancellation token flows from `ProduceAsync(ct, ...)` into every
async functoid/initializer. A few useful patterns:

- **Pre-cancelled token**: `Produce` fails fast with `OperationCanceledException`
  before any work runs.
- **Cooperative cancellation**: in your async body, pass `ct` to every async API
  you call. DICS will not preempt your code.
- **Scene cancellation in Unity**: the `MonoModule.cancellationToken` is bound
  under `Key.Of<CancellationToken>(scene.name)`; pass it to `ProduceAsync` if
  you want producer-level cancellation tied to scene unload.

## Errors

| Situation | Result |
| --- | --- |
| Async op in a plan executed by sync `Producer` | `DicsProducerException` |
| Async functoid throws | The thrown exception bubbles out of `await ProduceAsync(...)` (see [Failure semantics](#failure-semantics)) |
| Token cancelled before / during produce | `OperationCanceledException` (or a subclass) from `await` |
| Multiple parallel failures | Aggregated into `AggregateException` — see [Failure semantics](#failure-semantics) |

## Failure semantics

When an async operation throws or the caller's `CancellationToken` fires,
`AsyncProducer` does **not** return immediately. The producer:

1. Cancels a *linked* internal `CancellationTokenSource` on first failure, so
   sibling tasks observe cancellation cooperatively and stop scheduling
   further work.
2. **Drains all in-flight tasks** — every scheduled task runs its `finally`
   block (releasing handles, decrementing the completion counter) before
   `ProduceAsync` returns. The caller never sees a half-built locator that is
   still being mutated in the background.
3. Aggregates exceptions. The shape of what gets thrown depends on what was
   collected during the drain:
   - If the caller's external token was already cancelled, a fresh
     `OperationCanceledException(ct)` is thrown — regardless of what else
     happened.
   - Otherwise, if exactly one user exception was observed, it is re-thrown
     via `ExceptionDispatchInfo.Capture(...).Throw()` (preserving the original
     stack trace).
   - If two or more user exceptions were observed, they are wrapped in an
     `AggregateException`. Earlier behaviour (first failure wins, the rest
     swallowed) no longer applies.
   - If no user exceptions were observed but one or more
     `OperationCanceledException`s were (user code voluntarily called
     `ThrowIfCancellationRequested`), the same single-vs-aggregate rule applies
     to those.

The returned-from-`finally` locator (the one the caller catches the exception
*around*) is no longer being mutated by the time the exception surfaces, but
**it is not guaranteed to be usable**: keys whose construction failed or never
ran will be missing, and any object that ran a partial initializer is in
whatever state its own code left it in. Treat the locator as inspectable but
not productive — read `LocatorMeta` and finished entries for diagnostics, do
not call into half-initialized services.

## Choosing between sync and async lifecycles

| You want… | Use |
| --- | --- |
| New, blocking constructor only | `Functoid` |
| Async factory method returning `Task<T>` | `AsyncFunctoid` |
| Sync construction, sync field injection only | `[LiftInitializer]` + sync `Lifecycle` |
| Sync construction, async post-init work | `AsyncLifecycle(IFunctoid, IAsyncInitializer)` |
| Async construction + async post-init | `AsyncLifecycle(IAsyncFunctoid, IAsyncInitializer)` |

When in doubt, start sync; convert to async per-binding when you actually need
to `await` something during startup. Async leaks: once a single binding is
async, callers of `Injector` must `await`.

## Caveats

- DICS does not throttle parallelism. If you have hundreds of independent async
  constructors all hitting the same remote service, expect them to all hit it
  simultaneously. Throttle inside your functoid (e.g. with a `SemaphoreSlim`).
- `AsyncProducer` schedules onto the thread pool with `Task.Run`. If you need
  a specific synchronization context (UI thread, Unity main thread), capture
  it inside your functoid and `await` back onto it explicitly.
- Cycles are still rejected at plan time — async does not change that.
