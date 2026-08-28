using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DICS
{
    public class Injector
    {
        private readonly ImmutableModule _module;
        private readonly ILocator _parent;
        private readonly IDicsMeasurement dicsMeasurement;
        private readonly Planner _planner;
        private readonly Producer _producer;
        private readonly AsyncProducer _asyncProducer;

        public Injector(ILocator parent,  IDicsMeasurement dicsMeasurement, string name, params ImmutableModule[] module)
        {
            _parent = parent;
            this.dicsMeasurement = dicsMeasurement;
            _module = ImmutableModule.Merge(module);
            _producer = new Producer(_parent, this, name, dicsMeasurement);
            _asyncProducer = new AsyncProducer(_parent, this, name, dicsMeasurement);
            _planner = new Planner(_parent, _module, this, dicsMeasurement);
        }

        /// <summary>
        ///     Returns a new <see cref="Injector"/> whose parent locator overlays the given
        ///     <paramref name="instances"/> on top of this injector's parent, and which
        ///     carries this injector's <see cref="ImmutableModule"/> forward. Bindings of
        ///     the original module remain producible from the returned child.
        /// </summary>
        public Injector Extend(string name, params Instance[] instances)
        {
            return new Injector(_parent.Inherited(instances), dicsMeasurement, name, _module);
        }

        /// <summary>
        ///     There is an inevitable minor performance penalty associated with this call, better avoid it.
        /// </summary>
        public ISet<Key> RootCandidates()
        {
            return _planner.RootCandidates();
        }

        public Plan Plan(params Key[] roots)
        {
            return _planner.Plan(roots.ToHashSet(), new HashSet<IAxisPoint>());
        }

        public Plan Plan(ISet<Key> roots, ISet<IAxisPoint> config)
        {
            return _planner.Plan(roots, config);
        }

        public ILocator Produce(Plan plan)
        {
            using var handler = dicsMeasurement.StartTotal();
            return _producer.Produce(plan);
        }

        public ILocator Produce(params Key[] roots)
        {
            using var handler = dicsMeasurement.StartTotal();
            var plan = _planner.Plan(roots.ToHashSet(), new HashSet<IAxisPoint>());
            return _producer.Produce(plan);
        }

        public ILocator Produce(ISet<Key> roots, ISet<IAxisPoint> config)
        {
            using var handler = dicsMeasurement.StartTotal();
            var plan = _planner.Plan(roots, config);
            return _producer.Produce(plan);
        }

        /// <summary>
        /// Async counterpart to <see cref="Produce(Plan)"/>. Handles both synchronous and
        /// async bindings; independent ready keys are executed concurrently.
        /// </summary>
        // Disposable thread-affinity note: a single overlapping StartTotal/Dispose pair is
        // safe; overlapping concurrent StartTotal invocations on the same IDicsMeasurement
        // are not (the underlying ambient stopwatch is process-singleton in
        // DefaultDicsMeasurement).
        public async Task<ILocator> ProduceAsync(Plan plan, CancellationToken ct = default)
        {
            using var handler = dicsMeasurement.StartTotal();
            return await _asyncProducer.Produce(plan, ct).ConfigureAwait(false);
        }

        public async Task<ILocator> ProduceAsync(CancellationToken ct, params Key[] roots)
        {
            using var handler = dicsMeasurement.StartTotal();
            var plan = _planner.Plan(roots.ToHashSet(), new HashSet<IAxisPoint>());
            return await _asyncProducer.Produce(plan, ct).ConfigureAwait(false);
        }

        public async Task<ILocator> ProduceAsync(ISet<Key> roots, ISet<IAxisPoint> config, CancellationToken ct = default)
        {
            using var handler = dicsMeasurement.StartTotal();
            var plan = _planner.Plan(roots, config);
            return await _asyncProducer.Produce(plan, ct).ConfigureAwait(false);
        }
    }
}