
using System.Collections.Generic;

namespace DICS
{
    public class Producer
    {
        private readonly Injector _owner;
        private readonly IDicsMeasurement _dicsMeasurement;
        private readonly ILocator _parent;
        private readonly string _ownerName;

        public Producer(ILocator parent, Injector owner, string ownerName, IDicsMeasurement dicsMeasurement)
        {
            _parent = parent;
            _owner = owner;
            _ownerName = ownerName;
            _dicsMeasurement = dicsMeasurement;
        }

        public ILocator Produce(Plan plan)
        {
            DepMatrix<Instruction> revdep;
            LocatorImpl loc;

            using (_dicsMeasurement.Start("PreProduce"))
            {
                if (plan.plannedBy != _owner)
                    throw new DicsBug(
                        $"Plan was produced by a different Injector instance than the one running it; cross-injector plan reuse is not supported.");

                loc = new LocatorImpl(_parent, plan.Private, _ownerName);

                revdep = plan.Matrix.Transpose();
            }


            DoProduce(plan.InstantiationOrder, revdep, loc, _dicsMeasurement);

            using (_dicsMeasurement.Start("PostProduce"))
            {
                foreach (var root in plan.Roots)
                    if (!loc.HasLocally(root))
                        throw new DicsProducerException($"Root missing in producer output: {root}");

                var meta = new LocatorMeta(plan, _dicsMeasurement);
                loc.Put(Key.Of<LocatorMeta>(), meta);
                if (loc.HasLocally(Key.Of<MagicMutableDicsReference<LocatorMeta>>()))
                    loc.Get<MagicMutableDicsReference<LocatorMeta>>().Set(meta);
                if (loc.HasLocally(Key.Of<MagicMutableDicsReference<ILocator>>()))
                    loc.Get<MagicMutableDicsReference<ILocator>>().Set(loc);
            }

            
            // var imports = plan.Matrix.Data.Values.OfType<Instruction.Import>().Select(op => op.Key).ToHashSet();
            // var local = loc.DumpLocal();
            // foreach (var op in plan.Matrix.Data.Values.OfType<Instruction.CreateAutoset>())
            // {
            //     var setRef = loc.Get<IInternalMutableReference>(op.Key);
            //     var zygote = op.SetZygote.Create();
            //     foreach (var b in local)
            //     {
            //         var isSubtype = op.ElementType.IsInstanceOfType(b.instance);
            //         var inclusionAllowed = !imports.Contains(op.Key) || op.IncludeImports;
            //         // Console.WriteLine($"Autoset {op.Key}: considering {b.key}, isSubtype={isSubtype} inclusionAllowed={inclusionAllowed}");
            //         if (isSubtype && inclusionAllowed)
            //             zygote.Add(b.instance);
            //     }
            //
            //
            //     setRef.SetUnsafe(zygote.Retrieve());
            // }

            return loc;
        }


        /// <summary>
        /// Walks <see cref="Plan.InstantiationOrder"/>, which already lists dependencies
        /// before dependents, so every dependency is in the locator by the time a key that
        /// needs it is executed. Previously this rediscovered the order at runtime by
        /// re-deriving build waves from the matrix, which left the order inside a wave up to
        /// HashSet enumeration and made it unobservable to anything but the producer itself.
        /// </summary>
        private void DoProduce(IReadOnlyList<Key> instantiationOrder, DepMatrix<Instruction> dependees,
            LocatorImpl locatorImpl, IDicsMeasurement dicsMeasurement)
        {
            foreach (var key in instantiationOrder)
            {
                if (!dependees.Data.TryGetValue(key, out var defn))
                    throw new DicsBug($"Inconsistent plan, no operation for {key}");

                using (dicsMeasurement.Start(key))
                {
                    Execute(locatorImpl, defn, key, dependees);
                }
            }
        }

        private void Execute(LocatorImpl locatorImpl, Instruction defn, Key key, DepMatrix<Instruction> dependees)
        {
            if (!InstructionDispatcher.TryExecuteSync(locatorImpl, defn, key, dependees, _parent))
                throw new DicsProducerException(
                    $"{key}: plan contains an async operation but is being executed by the synchronous Producer; " +
                    "use Injector.ProduceAsync instead.");
        }
    }
}