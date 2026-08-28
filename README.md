# DICS

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](./LICENSE)
[![CI](https://github.com/PlayQ/opendics/actions/workflows/ci.yml/badge.svg)](https://github.com/PlayQ/opendics/actions/workflows/ci.yml)

DICS (pronounced *dee-see-es*) is a **staged dependency injection** library for C# —
a simplified port of the core ideas in [DIstage](https://github.com/7mind/izumi).
It runs on plain .NET (`netstandard2.1`) and inside Unity.

## Installation

**NuGet** (plain .NET, `netstandard2.1`):

```
dotnet add package DICS
```

The package carries `DICS.Generator` as a Roslyn analyzer, so `[LiftConstructor]`,
`[LiftInitializer]`, and `[LiftFactory]` work as soon as the package is referenced.

**Unity** — add the package by git URL, via *Window → Package Manager → Add package
from git URL*, or directly in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.playq.dics": "https://github.com/PlayQ/opendics.git",
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }
}
```

Unity 2021.3 or newer. `com.unity.nuget.newtonsoft-json` is a real dependency, not a
convenience: under Unity `TraceGen` serializes profiles with Newtonsoft, and only
falls back to `System.Text.Json` off-Unity.

Unity does not pick up the source generator from this repository — `DICS.Generator`
is restricted to a platform Unity does not compile for in the editor, and no
pre-built analyzer assembly is committed. Under Unity, either lift constructors and
initializers by hand, or build `DICS.Generator` and drop the DLL into the project as
an asset labelled `RoslynAnalyzer`.

## Why staged DI?

DICS is a three-pass tool:

1. **Compile-time reflection** — a [Roslyn source generator](https://learn.microsoft.com/en-gb/dotnet/csharp/roslyn-sdk/#source-generators)
   wraps constructors and initializers into runtime-inspectable *functoids*. Optional;
   you can write functoids by hand.
2. **Planning** — at runtime, DICS computes a [project network](https://en.wikipedia.org/wiki/Project_network)
   of operations from your modules and roots. If the graph cannot be wired (missing
   keys, cycles, ambiguous bindings), planning fails *before* anything is instantiated.
3. **Production** — DICS executes the plan and returns a `Locator` containing every
   produced instance.

This separation makes DI debuggable: the `Plan` is a value you can print, diff, or
serialize, and instantiation never reflects against `Type` beyond `typeof` and
equality comparison.

DICS is one instance of the *Percept-Plan-Execute-Repeat* metaprogramming pattern
formulated by [pshirshov](https://github.com/pshirshov) in 2014.

Background reading: [the DIstage manual](https://izumi.7mind.io/distage/basics.html).

## Documentation

- [**Core usage**](./docs/core.md) — concepts, modules, factories, lifecycles, the
  whole non-Unity surface.
- [**Unity usage**](./docs/unity.md) — `MonoModule`, scenes, prefabs, tickables,
  cancellation tokens.
- [**Async usage**](./docs/async.md) — `IAsyncFunctoid`, `IAsyncInitializer`,
  `ProduceAsync`, generator support, cancellation, parallelism.

## 30-second example

```csharp
public class MyModule : Module
{
    public MyModule()
    {
        Make<int>().From().Instance(42);
        Make<string>().From().Functoid(
            IFunctoid.Lift((int n) => $"answer={n}")
        );
    }
}

var injector = new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), "demo",
    new MyModule().Freeze());

ILocator loc = injector.Produce(Key.Of<string>());
Console.WriteLine(loc.Get<string>()); // answer=42
```

The async equivalent (see [docs/async.md](./docs/async.md)):

```csharp
ILocator loc = await injector.ProduceAsync(ct, Key.Of<string>());
```

## Key concepts

| Concept | What it is |
| --- | --- |
| [`Key`](./DICS/Model/Generic/Key.cs) | A `(Type, name?)` identifier for an instance. |
| [`Binding`](./DICS/Model/Module/Binding.cs) | "How to make this key": instance, ref, functoid, lifecycle. |
| [`Module`](./DICS/Model/Module/Module.cs) | A collection of bindings, plus DSL to build them. Modules concatenate. |
| [`Plan`](./DICS/Model/Plan/Plan.cs) | A DAG of operations derived from your modules and roots. Validates wiring. |
| [`Locator`](./DICS/Model/Locator/AbstractLocator.cs) | The "world" produced by executing a Plan. |
| [`Injector`](./DICS/Injector/Injector.cs) | Turns modules into plans and plans into locators. |
| [`Provider`](./DICS/Model/Functoid/Functoid.cs) | Runtime-introspectable representation of a function. |
| [`Initializer`](./DICS/Model/Functoid/Initializer.cs) | Like a provider, but mutates an existing instance. |

## Differences from other DI frameworks

- **No circular references.** Cycles are unsound, expensive to support, and encourage
  bad design. DICS rejects them at plan time and reports the cycle chains it found.
- **Singleton-only semantics.** Non-singleton needs are met via *factories*,
  *assisted injection*, and *locator inheritance* — all explicit, none implicit.
- **No reflection beyond `typeof`/`Type.Equals`.** Constructors and initializers are
  represented as functoids, either hand-written or generated.

## Limitations

- DICS internals use mutable references for performance. **Treat all DICS data
  structures as strictly immutable from the outside** — mutation will silently
  corrupt the graph.
- Due to C# constraints, you either lift constructors by hand or use
  `DICS.Generator` source generators.

## Good practices

- Put `// ReSharper disable PartialTypeWithSinglePart` in files with `partial`
  classes (or disable the inspection globally) so ReSharper does not strip
  `partial` from generator-driven types.

## License

DICS is licensed under the [MIT License](./LICENSE).
