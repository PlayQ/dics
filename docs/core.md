# DICS — Core Usage (non-Unity)

This guide covers DICS as a plain .NET library. For Unity-specific helpers see
[unity.md](./unity.md). For async support see [async.md](./async.md).

## Installation

DICS targets `netstandard2.1`. Add the projects (or a NuGet package, once
published) to your solution:

- `DICS` — the runtime.
- `DICS.Generator` — the Roslyn source generator. Reference it with
  `OutputItemType="Analyzer"`:

```xml
<ItemGroup>
  <ProjectReference Include="..\DICS\DICS.csproj" />
  <ProjectReference Include="..\DICS.Generator\DICS.Generator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

## The 5-minute mental model

```
Module ── Freeze ──▶ ImmutableModule ─┐
                                      ├──▶ Planner ──▶ Plan ──▶ Producer ──▶ Locator
            roots, config ────────────┘
```

You write **modules** (collections of bindings), pass them and a set of *root keys*
into an **Injector**, get back a **Plan** (a DAG of operations), and execute it to
get a **Locator** (the produced instances).

## Defining a Module

```csharp
public class GreetingsModule : Module
{
    public GreetingsModule(string who)
    {
        Make<string>().Named("who").From().Instance(who);

        // Constructor lifted manually.
        Make<Greeter>().From().Functoid(
            IFunctoid.Lift((string w) => new Greeter(w), names: new[] { "who" })
        );
    }
}

public record Greeter(string Who) { public string Hi() => $"hello, {Who}"; }
```

`names` aligns positional arguments to named keys: the first lambda parameter
binds to `Key.Of<string>("who")` instead of the default `Key.Of<string>()`.

## Producing instances

```csharp
var module    = new GreetingsModule("world").Freeze();
var injector  = new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(),
                             name: "demo", module);
var locator   = injector.Produce(Key.Of<Greeter>());
Console.WriteLine(locator.Get<Greeter>().Hi()); // hello, world
```

`Produce(params Key[] roots)` is a shortcut for `Plan(roots)` followed by
`Produce(plan)`. Use the explicit two-step form to inspect or print the plan
before executing it.

## Binding flavours

```csharp
Make<T>().From().Instance(value)         // bind to a pre-built instance
Make<T>().From().Ref<U>()                // alias another key (T must be a supertype of U)
Make<T>().From().Functoid(functoid)      // call a constructor / factory
Make<T>().From().Lifecycle(extr, init)   // construct + initialize (see below)
Make<T>().From().Import()                // explicitly imported from a parent locator
```

Common modifiers:

```csharp
.Named("alpha")            // disambiguate by name
.In(axisPoint)             // gate on an Activation axis (see "Configuration")
.Aliased<U>()              // also publish under Key.Of<U>()
.AddToSetOf<U>()           // also add to Key.Of<ISet<U>>()
.AddDependency<U>()        // declare an extra dependency for ordering
.Private()                 // hide from child locators
```

## Sets and autosets

```csharp
// Explicit set:
Make<ISet<string>>().Named("colors")
    .Add().Instance("red")
    .Add().Instance("green");

// Autoset: every binding whose ImplType is assignable to T is collected.
Make<ISet<IPlugin>>().From().Auto();

// IList<T> autoset works the same way (use when ordering or duplicates matter):
Make<IList<IPlugin>>().From().Auto();

// Empty set marker (so consumers don't fail when nothing was added):
Make<ISet<string>>().Named("flags").From().Empty();
```

### Set element order

Elements are listed in `Plan.InstantiationOrder` — the plan's one materialized
topological order, dependencies before dependents, counting `AddDependency<U>()`
edges. `Produce` walks exactly that sequence; `ProduceAsync` schedules the same graph
concurrently, so it may interleave independent keys differently, but the set is still
listed in the plan's order. Walking an `IList<T>` backwards therefore releases every
dependent strictly before its dependencies — which is what `MonoModule.OnDestroy`
does with `IList<IDisposable>`, and the reason forward iteration ticks dependencies
first.

The dependency graph only defines a partial order, so elements it leaves unordered
fall back to module declaration order. A module can therefore still choose the
instantiation and teardown order of unrelated components by declaration position.
`ISet<T>` is unordered by contract regardless — use `IList<T>` when order matters.

## Lifecycles (extractor + initializer)

A lifecycle splits "build the empty shell" from "fill it in". This is useful
when initialization needs the locator (e.g. to look up other dependencies) or
when the shell-building and initialization come from different sources.

```csharp
Make<MyService>().From().Lifecycle(
    extractor:   IFunctoid.Lift(() => new MyService()),
    initializer: IInitializer.Lift(
        (MyService self, Sig _, IRepository repo) => self.Wire(repo))
);
```

For async initialization see [async.md](./docs/async.md).

## Factories and assisted injection

Sometimes you need to produce many instances at runtime with caller-supplied
arguments. DICS supports two factory flavours:

- **Untyped** (`IUnsafeFactory<T>`): the caller passes an `ILocator` of local
  parameters.
- **Typed**: the generator emits a strongly-typed `Factory` nested type with a
  `Make(...)` method whose signature matches `[Local]` parameters.

`[LiftFactory]` auto-detects whether to wrap a constructor or an initializer:
classes with `[Inject]` fields are treated as initializer-shaped, everything
else as constructor-shaped. Pass `FactoryKind.Constructor` or
`FactoryKind.Initializer` explicitly to override.

```csharp
// [LiftFactory] inferred from the [Local] parameter; FactoryKind also auto-detected
// (no [Inject] members => Constructor).
public partial record Widget(
    IDep Dep,
    [Local] int Size
);

// Module:
Make<IDep>().From().Instance(new DepImpl());        // satisfy the non-[Local] dependency
Make<Widget.Factory>().From().TypedFactory().Using()
    .Functoid(Widget.LiftFactoryFunctoid());

// Usage:
var widget = locator.Get<Widget.Factory>().Make(size: 42);
```

## Generator-driven bindings

### Annotation-omission heuristic

You usually do **not** need to write a class-level `[LiftInitializer]` or
`[LiftFactory]` attribute. The generator infers them from member-level markers
on any `partial` class that has no class-level Lift attribute of its own:

| Member-level marker present | Inferred class-level attribute |
| --- | --- |
| `[Inject]` on any field/property | `[LiftInitializer]` |
| `[Local]` on any field/parameter | `[LiftFactory]` (kind auto-detected) |

Both can fire on the same class — a class with `[Inject]` fields and `[Local]`
parameters gets both `LiftInitializer()` and a typed `Factory`.

The heuristic is **conservative**: any explicit class-level Lift attribute
takes precedence and suppresses inference for that class. Use the explicit
attribute when you need to disambiguate (e.g. force a `FactoryKind`), or just
to make intent obvious to readers.

`[LiftConstructor]` is the one class-level attribute that cannot be inferred:
there is no per-parameter marker that uniquely identifies a constructor-lift
target, so you still write it explicitly when you want a `Lift()` for the
constructor itself.

### `[LiftConstructor]`

Tag your class `[LiftConstructor]` (and make it `partial`) to get a generated
`Lift()` returning `IFunctoid`:

```csharp
[LiftConstructor]
public partial record GreeterAuto(
    [Id("who")] string Who
);

Make<GreeterAuto>().From().Functoid(GreeterAuto.Lift());
```

### `[LiftInitializer]` (often inferred)

For initialization, mark `[Inject]` on the fields/properties that should
receive dependencies. The class-level `[LiftInitializer]` is **inferred**
from the presence of `[Inject]` members:

```csharp
// No class-level attribute needed — [Inject] members imply [LiftInitializer].
public partial class Service
{
    [Inject] protected IRepository Repo = null!;
    [Inject] [Id("region")] protected string Region = null!;

    public void DoWork() { /* ... */ }
}

Make<Service>().From().Lifecycle(
    IFunctoid.Lift(() => new Service()),
    Service.LiftInitializer()
);
```

You can still write `[LiftInitializer]` explicitly if you prefer.

## Configuration (Activation axes)

A binding can be gated on an *axis point*. At plan time you choose one point per
axis; bindings on other points of the same axis are skipped:

```csharp
public record Env(string Point) : IAxisPoint
{
    public string AxisName() => "Env";
    public string PointName() => Point;
    public static readonly Env Prod = new("Prod");
    public static readonly Env Test = new("Test");
}

Make<IClock>().In(Env.Prod).From().Functoid(IFunctoid.Lift(() => SystemClock.Instance));
Make<IClock>().In(Env.Test).From().Instance(new FixedClock(DateTime.UnixEpoch));

var plan = injector.Plan(new HashSet<Key>{ Key.Of<IClock>() },
                         new HashSet<IAxisPoint>{ Env.Test });
```

## Locator inheritance

Injectors can be *layered*. A child injector is built on top of a parent locator;
its plans can reference any key the parent produced, but the parent never sees the
child's keys.

```csharp
var rootLoc = rootInjector.Produce(...);
var sub     = new Injector(rootLoc, m, name: "request-scope", subModule.Freeze());
var subLoc  = sub.Produce(Key.Of<RequestHandler>());
```

Use `.Private()` to hide a binding from any child.

### Magic imports

Three keys are recognised specially by the producer when bound via
`Make<...>().From().Import()`. They do not resolve from the parent locator
in the usual way; the producer fills them in directly:

- `MagicMutableDicsReference<LocatorMeta>` — a mutable cell that the producer
  back-fills with this locator's `LocatorMeta` once production is complete.
  Useful when an object needs to introspect the plan/measurements of the very
  locator it lives in.
- `MagicMutableDicsReference<ILocator>` — a mutable cell back-filled with the
  owning locator itself after production completes. Use sparingly — pulling
  `ILocator` into a constructed object reintroduces service-locator-style
  coupling.
- `ILocator` named `"parent"` — resolves to the parent locator of the current
  injector (i.e. the locator the new injector was layered on top of).

```csharp
// Hand the produced object a handle to its own locator's metadata.
Make<MagicMutableDicsReference<LocatorMeta>>().From().Import();

// Hand the produced object a handle to its own locator.
Make<MagicMutableDicsReference<ILocator>>().From().Import();

// Resolve the parent locator (the one this injector was layered on top of).
Make<ILocator>().Named("parent").From().Import();
```

`MagicMutableDicsReference<T>` is a one-shot cell: read it after production
has returned, not during the constructor of the same object — the back-fill
happens in the producer's post-pass.

## Debugging

- `Console.WriteLine(plan)` prints the DAG and the per-key instructions.
- `LocatorMeta` (always produced) carries the plan and any
  `IDicsMeasurement` timings.
- `MagicMutableDicsReference<LocatorMeta>` / `MagicMutableDicsReference<ILocator>`
  can be injected into a constructed object — DICS back-fills them once
  production is complete, giving the object access to the world it lives in.

## Errors you may hit

| Exception | Cause |
| --- | --- |
| `DicsPlanningException` | The plan cannot be built (missing key, cycle, conflicting bindings, axis ambiguity). |
| `DicsProducerException` | A `Plan` could not be executed (e.g. a `ToDo` binding, or an async op given to the sync producer). |
| `DicsRuntimeException`  | A locator lookup failed at runtime. |
| `DicsBug`               | An internal invariant was violated. File an issue. |
