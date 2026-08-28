using System;
using System.Collections.Generic;
using System.Linq;

namespace DICS
{
    public class Planner
    {
        private readonly ImmutableModule _module;
        private readonly Injector _owner;
        private readonly IDicsMeasurement _dicsMeasurement;
        private readonly ILocator _parent;

        public Planner(ILocator parent, ImmutableModule module, Injector owner, IDicsMeasurement dicsMeasurement)
        {
            _parent = parent;
            _module = module;
            _owner = owner;
            _dicsMeasurement = dicsMeasurement;
        }

        /// <summary>
        ///     There is an inevitable minor performance penalty associated with this call, better avoid it.
        /// </summary>
        public ISet<Key> RootCandidates()
        {
            var bindings = Preprocess();

            var instructions = Translate(bindings);
            var index = instructions.IndexBy(b => b.Key);
            var definedKeys = index.Keys.ToHashSet();

            var resolvedDeps = new Dictionary<Key, ISet<Key>>();

            foreach (var (key, value) in index)
            {
                var deps = value.SelectMany(i => DependenciesOf(definedKeys, i)).ToHashSet();
                if (!resolvedDeps.TryAdd(key, deps))
                {
                }
            }

            var referenced = resolvedDeps.SelectMany(kv => kv.Value);
            var candidates = resolvedDeps.Keys.Where(k => !referenced.Contains(k));
            return candidates.ToHashSet();
        }

        private List<Binding> Preprocess()
        {
            var bindings = new List<Binding>();
            var source = _module.Bindings;
            var extractorKeys = ExtractorKeysOf(source);

            foreach (var op in source)
                if (op is Binding.CreateAutoset ca)
                {
                    // One ordered element list per autoset. Emitting many AddSetElement
                    // records and merging them via HashSet<Instruction> dropped IList order.
                    var elementKeys = new List<Key>();
                    foreach (var binding in source)
                    {
                        if (IsAutosetElement(ca, binding, extractorKeys))
                            elementKeys.Add(binding.Key);
                    }

                    bindings.Add(new Binding.AddSetElements(
                        ca.Key, elementKeys, ca.SetZygote, ca.ImplType, ca.Points));
                }
                else
                {
                    bindings.Add(op);
                }

            return bindings;
        }

        private static HashSet<Key> ExtractorKeysOf(IEnumerable<Binding> source)
        {
            var keys = new HashSet<Key>();
            foreach (var b in source)
            {
                switch (b)
                {
                    case Binding.ToInitializer ti:
                        keys.Add(ti.ExtractorKey);
                        break;
                    case Binding.ToAsyncInitializer ati:
                        keys.Add(ati.ExtractorKey);
                        break;
                }
            }

            return keys;
        }

        /// <summary>
        /// Auto collects locally produced instances, not second names, stubs, or factories.
        /// ImplType is a real type, so kind-filtering has to be explicit.
        /// </summary>
        private static bool IsAutosetElement(Binding.CreateAutoset ca, Binding binding, ISet<Key> extractorKeys)
        {
            if (binding is Binding.AddSetElement or Binding.CreateAutoset or Binding.AddSetElements)
                return false;
            if (!ca.ElementType.IsAssignableFrom(binding.ImplType))
                return false;

            switch (binding)
            {
                case Binding.ToInstance:
                case Binding.ToInitializer:
                case Binding.ToAsyncInitializer:
                    return true;
                case Binding.ToFunctoid:
                case Binding.ToAsyncFunctoid:
                    return !extractorKeys.Contains(binding.Key);
                case Binding.Import:
                    return ca.IncludeImports;
                default:
                    return false;
            }
        }

        public Plan Plan(ISet<Key> roots, ISet<IAxisPoint> config)
        {
            using var handler = _dicsMeasurement.Start("Plan");
            
            var configDict = ValidatePoints(config, "config");

            var bindings = Preprocess();

            var instructions = Translate(bindings);

            var index = instructions.IndexBy(b => b.Key);

            var resolvedValues = new Dictionary<Key, Instruction>();
            var resolvedDeps = new Dictionary<Key, ISet<Key>>();

            DoPlan(roots, index, index.Keys.ToHashSet(), resolvedValues, resolvedDeps, configDict);

            var matrix = new DepMatrix<Instruction>(resolvedDeps, resolvedValues);

            var loops = LoopDetector.FindAllCycles(matrix.Links);
            if (loops.Any())
                throw new DicsPlanningException(
                    $"Dependency graph contains cyclic references, refusing to proceed:\n{loops.Select(a => a.Join("->")).NiceList()}");

            var instantiationOrder = TopologicalOrder.Of(matrix.Links, DeclarationRank(bindings));

            if (OrderSetElements(instantiationOrder, resolvedValues))
                matrix = new DepMatrix<Instruction>(resolvedDeps, resolvedValues);

            return new Plan(matrix, instantiationOrder, roots, _module.PrivateBindings, _owner);
        }

        /// <summary>
        /// Position of a key in the module, used to break ties between keys the dependency
        /// graph leaves unordered. Keeps module-binding order observable in produced sets;
        /// keys with no binding of their own (imports) sort after the declared ones.
        /// </summary>
        private static Func<Key, int> DeclarationRank(IList<Binding> bindings)
        {
            var ranks = new Dictionary<Key, int>();
            for (var i = 0; i < bindings.Count; i++)
            {
                if (!ranks.ContainsKey(bindings[i].Key)) ranks[bindings[i].Key] = i;
            }

            return key => ranks.TryGetValue(key, out var rank) ? rank : bindings.Count;
        }

        /// <summary>
        /// Relists the elements of every <see cref="Instruction.CreateSet"/> in
        /// <paramref name="instantiationOrder"/>, so a set enumerates its elements in the
        /// order they were instantiated and walking it backwards releases every dependent
        /// before its dependencies (<c>MonoModule.OnDestroy</c>). Returns whether any
        /// element list actually changed.
        /// </summary>
        private static bool OrderSetElements(
            IReadOnlyList<Key> instantiationOrder, IDictionary<Key, Instruction> resolvedValues)
        {
            var setKeys = resolvedValues
                .Where(kv => kv.Value is Instruction.CreateSet cs && cs.Elements.Count > 1)
                .Select(kv => kv.Key)
                .ToList();

            if (!setKeys.Any()) return false;

            var positions = new Dictionary<Key, int>();
            for (var i = 0; i < instantiationOrder.Count; i++) positions[instantiationOrder[i]] = i;

            var changed = false;
            foreach (var setKey in setKeys)
            {
                var createSet = (Instruction.CreateSet)resolvedValues[setKey];
                var ordered = createSet.Elements.OrderBy(element =>
                    positions.TryGetValue(element, out var position)
                        ? position
                        : throw new DicsBug($"Set element {element} of {setKey} is absent from the plan")).ToList();

                if (ordered.SequenceEqual(createSet.Elements)) continue;

                resolvedValues[setKey] = createSet with { Elements = ordered };
                changed = true;
            }

            return changed;
        }

        private static IDictionary<string, IAxisPoint> ValidatePoints(ISet<IAxisPoint> config, string clue)
        {
            var cfgIdx = config.IndexBy(c => c.AxisName());
            foreach (var keyValuePair in cfgIdx)
                if (keyValuePair.Value.Count() > 1)
                    throw new DicsPlanningException(
                        $"Axis {keyValuePair.Key} has conflicting definitions ({clue}):\n{keyValuePair.Value.NiceList()}");
            return cfgIdx.Select(kv => KeyValuePair.Create(kv.Key, kv.Value.First()))
                .ToDictionary(x => x.Key, x => x.Value);
        }

        private IList<Instruction> Translate(IList<Binding> moduleBindings)
        {
            return moduleBindings.SelectMany(TranslateBinding).ToList();
        }

        private IEnumerable<Instruction> TranslateBinding(Binding binding)
        {
            switch (binding)
            {
                case Binding.ToKey toKey:
                    return new List<Instruction> { new Instruction.UseKey(toKey.Key, toKey.Source, toKey.Points) };
                case Binding.ToInstance toInstance:
                    return new List<Instruction>
                        { new Instruction.UseInstance(toInstance.Key, toInstance.Instance, toInstance.Points) };
                case Binding.ToFunctoid toFunctoid:
                    return new List<Instruction>
                        { new Instruction.CallFunctoid(toFunctoid.Key, toFunctoid.Functoid, toFunctoid.Points) };
                case Binding.ToAsyncFunctoid toAsyncFunctoid:
                    return new List<Instruction>
                    {
                        new Instruction.CallAsyncFunctoid(toAsyncFunctoid.Key, toAsyncFunctoid.Functoid,
                            toAsyncFunctoid.Points)
                    };
                case Binding.ToAsyncInitializer toAsyncInitializer:
                    return new List<Instruction>
                    {
                        new Instruction.CallAsyncInitializer(toAsyncInitializer.Key,
                            toAsyncInitializer.ExtractorKey, toAsyncInitializer.Initializer,
                            toAsyncInitializer.Points)
                    };
                case Binding.FactoryToFunctoid facToFunctoid:
                    return new List<Instruction>
                    {
                        new Instruction.CreateFunctoidFactory(facToFunctoid.Key, facToFunctoid.Zygote,
                            facToFunctoid.Functoid, facToFunctoid.Points)
                    };
                case Binding.FactoryToLifecycle facToLifecycle:
                    return new List<Instruction>
                    {
                        new Instruction.CreateLifecycleFactory(facToLifecycle.Key, facToLifecycle.Zygote,
                            facToLifecycle.Functoid, facToLifecycle.Initializer, facToLifecycle.Points)
                    };

                case Binding.ToInitializer toInitializer:
                    return new List<Instruction>
                    {
                        new Instruction.CallInitializer(toInitializer.Key, toInitializer.ExtractorKey,
                            toInitializer.Initializer, toInitializer.Points)
                    };
                case Binding.CreateAutoset createAutoset:
                    throw new DicsBug($"createautoset is not supposed to be translated: {createAutoset.Key}");
                case Binding.AddSetElement addSetElement:
                    IReadOnlyList<Key> one =
                        addSetElement.ElementKey != null
                            ? new[] { addSetElement.ElementKey }
                            : Array.Empty<Key>();

                    return new List<Instruction>
                    {
                        new Instruction.CreateSet(
                            addSetElement.Key,
                            one,
                            addSetElement.SetZygote, addSetElement.Points)
                    };
                case Binding.AddSetElements addSetElements:
                    return new List<Instruction>
                    {
                        new Instruction.CreateSet(
                            addSetElements.Key,
                            addSetElements.ElementKeys,
                            addSetElements.SetZygote, addSetElements.Points)
                    };
                case Binding.ToDo toDo:
                    return new List<Instruction>
                    {
                        new Instruction.ToDo(
                            toDo.Key,
                            toDo.Message,
                            toDo.Points
                        )
                    };
                case Binding.Import import:
                    return new List<Instruction>
                    {
                        new Instruction.Import(
                            import.Key,
                            import.Points
                        )
                    };
                case Binding.FactoryToGeneratedFunctoid factoryToGeneratedFunctoid:
                    return new List<Instruction>
                    {
                        new Instruction.FactoryToGeneratedFunctoid(
                            factoryToGeneratedFunctoid.Key,
                            factoryToGeneratedFunctoid.Functoid,
                            factoryToGeneratedFunctoid.Points
                        )
                    };
                case Binding.FactoryToGeneratedLifecycle factoryToGeneratedLifecycle:
                    return new List<Instruction>
                    {
                        new Instruction.FactoryToGeneratedLifecycle(
                            factoryToGeneratedLifecycle.Key,
                            factoryToGeneratedLifecycle.extractor,
                            factoryToGeneratedLifecycle.Functoid,
                            factoryToGeneratedLifecycle.Points
                        )
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(binding));
            }
        }

        private void DoPlan(ISet<Key> keys,
            IDictionary<Key, ISet<Instruction>> index,
            ISet<Key> definedKeys,
            Dictionary<Key, Instruction> resolvedValues,
            Dictionary<Key, ISet<Key>> resolvedDeps, IDictionary<string, IAxisPoint> config)
        {
            // Console.WriteLine($"Step: {keys.Join(",")}");
            var next = keys
                .Select(key =>
                {
                    if (index.TryGetValue(key, out var defns)) return (key, Resolve(key, defns, config));

                    return (key, new Instruction.Import(key, new IAxisPoint[] { }));
                })
                .Select(kv => (kv.Item1, kv.Item2, DependenciesOf(definedKeys, kv.Item2)));


            var nextDeps = new HashSet<Key>();
            foreach (var (k, v, d) in next)
            {
                if (!resolvedValues.TryAdd(k, v)) throw new DicsBug($"Bug: {k} is already resolved (operation)");
                if (!resolvedDeps.TryAdd(k, d)) throw new DicsBug($"Bug: {k} is already resolved (dependency)");
                nextDeps.UnionWith(d);
            }

            if (nextDeps.Any())
            {
                var alreadyResolved = resolvedValues.Keys.Intersect(nextDeps).ToList();
                if (alreadyResolved.Any())
                    nextDeps.ExceptWith(alreadyResolved);

                // Console.WriteLine(
                //     $"next = {nextDeps.JoinC()};; resolved={resolvedValues.JoinC()};; AR={alreadyResolved.JoinC()}");

                DoPlan(nextDeps, index, definedKeys, resolvedValues, resolvedDeps, config);
            }
        }

        private ISet<Key> DependenciesOf(ISet<Key> definedKeys, Instruction argItem2)
        {
            var primary = PrimaryDependenciesOf(definedKeys, argItem2);
            if (_module.ExtraDeps.TryGetValue(argItem2.Key, out var extra)) return primary.Union(extra).ToHashSet();
            return primary;
        }

        private ISet<Key> PrimaryDependenciesOf(ISet<Key> definedKeys, Instruction argItem2)
        {
            switch (argItem2)
            {
                case Instruction.CallFunctoid callFunctoid:
                    return callFunctoid.Functoid.Signature().Args.ToHashSet();
                case Instruction.CallAsyncFunctoid callAsyncFunctoid:
                    return callAsyncFunctoid.Functoid.Signature().Args.ToHashSet();
                case Instruction.CallAsyncInitializer callAsyncInitializer:
                    return callAsyncInitializer.Initializer.Signature().Args.ToHashSet()
                        .Union(new HashSet<Key> { callAsyncInitializer.ExtractorKey })
                        .ToHashSet();
                case Instruction.UseInstance:
                    return new HashSet<Key>();
                case Instruction.Import:
                    return new HashSet<Key>();
                case Instruction.UseKey useKey:
                    return new HashSet<Key> { useKey.Source };
                case Instruction.CreateSet createSet:
                    return createSet.Elements.ToHashSet();
                case Instruction.CallInitializer callInitializer:
                    return callInitializer.Initializer.Signature().Args.ToHashSet()
                        .Union(new HashSet<Key> { callInitializer.ExtractorKey })
                        .ToHashSet();
                case Instruction.CreateFunctoidFactory createFunctoidFactory:
                    var allArgs1 = createFunctoidFactory.Functoid.Signature().Args.ToHashSet();
                    var toBeProvided1 = allArgs1.Where(key => ToBeProvided(definedKeys, key)).ToHashSet();
                    return allArgs1.Except(toBeProvided1).ToHashSet();

                case Instruction.CreateLifecycleFactory createLifecycleFactory:
                    var allArgs2 = createLifecycleFactory.Functoid.Signature().Args
                        .Union(createLifecycleFactory.Initializer.Signature().Args).ToHashSet();
                    var toBeProvided2 = allArgs2.Where(key => ToBeProvided(definedKeys, key)).ToHashSet();

                    return allArgs2.Except(toBeProvided2).ToHashSet();
                case Instruction.ToDo:
                    return new HashSet<Key>();
                case Instruction.FactoryToGeneratedFunctoid factoryToGeneratedFunctoid:
                    return factoryToGeneratedFunctoid.Functoid.MakeSignature().Args.ToHashSet();
                case Instruction.FactoryToGeneratedLifecycle factoryToGeneratedLifecycle:
                    var allArgs3 = factoryToGeneratedLifecycle.Extractor.Signature().Args
                        .Union(factoryToGeneratedLifecycle.Functoid.MakeSignature().Args).ToHashSet();
                    return allArgs3.ToHashSet();
                default:
                    throw new ArgumentOutOfRangeException(nameof(argItem2));
            }
        }

        private bool ToBeProvided(ISet<Key> definedKeys, Key key)
        {
            return !(definedKeys.Contains(key) || _parent.Has(key));
        }

        /// <summary>
        /// Concatenates element lists, first occurrence wins, preserving order. Replaces a
        /// ToHashSet() merge that discarded it.
        /// </summary>
        private static IReadOnlyList<Key> MergeElementKeys(IEnumerable<Instruction.CreateSet> setCreators)
        {
            var merged = new List<Key>();
            var seen = new HashSet<Key>();
            foreach (var creator in setCreators)
            {
                foreach (var element in creator.Elements)
                {
                    if (element != null && seen.Add(element))
                        merged.Add(element);
                }
            }

            return merged;
        }

        private Instruction Resolve(Key key, ISet<Instruction> value, IDictionary<string, IAxisPoint> config)
        {
            var setCreators = value.OfType<Instruction.CreateSet>().ToList();

            if (setCreators.Count > 0)
            {
                if (value.Count == setCreators.Count)
                    return new Instruction.CreateSet(key,
                        MergeElementKeys(setCreators),
                        setCreators.First().SetZygote, new IAxisPoint[] { });

                throw new DicsBug($"Inconsistent set definition for {key}");
            }

            if (value.Count == 1) return value.First();

            var filtered = value.Where(b => IsAllowed(key, b.Points, config)).ToList();

            if (filtered.Count == 1) return filtered.First();

            throw new DicsPlanningException(
                $"Multiple definitions allowed for {key} in current configuration:\n{filtered.NiceList()}");
        }

        private bool IsAllowed(Key key, IAxisPoint[] argPoints, IDictionary<string, IAxisPoint> config)
        {
            var asSet = argPoints.ToHashSet();
            ValidatePoints(asSet, key.ToString());

            foreach (var axisPoint in argPoints)
                if (config.TryGetValue(axisPoint.AxisName(), out var defined))
                    if (defined.PointName() != axisPoint.PointName())
                        return false;

            return true;
        }
    }
}