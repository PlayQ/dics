using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DICS.Tools;

namespace DICS
{
    public sealed class DefaultDicsMeasurement : IDicsMeasurement
    {
        private sealed class Handler : IDisposable
        {
            private readonly Action onComplete;

            public Handler(Action onComplete)
            {
                this.onComplete = onComplete;
            }

            public void Dispose() => onComplete();
        }

        // Concurrent-safe accumulators. ProduceAsync runs functoids in parallel,
        // each calling Start(Key) which mutates these maps. Prior to this change
        // they were plain Dictionary<,> and threw InvalidOperationException
        // ("Operations that change non-concurrent collections must have exclusive
        // access") and IndexOutOfRangeException under parallel load.
        private readonly ConcurrentDictionary<Key, TimeSpan> timings = new();
        private readonly ConcurrentDictionary<Key, DateTime> timestamps = new();

        private readonly ConcurrentDictionary<string, TimeSpan> customTimings = new();
        private readonly ConcurrentDictionary<string, DateTime> customTimestamps = new();

        // `order` records first-seen instantiation order. List<T> is not safe for
        // concurrent Add; mutation is infrequent (once per first sighting of a key)
        // so a lock is simpler than a concurrent insertion-ordered collection.
        private readonly List<Key> order = new();

        private DateTime startTimeTotal;
        private TimeSpan elapsedTimeTotal;
        
        public IDisposable StartTotal()
        {
            startTimeTotal = DateTime.Now;
            return new Handler(() =>
            {
                // Accumulate across repeated Produce calls on the same Injector. The
                // first timestamp is retained; total time is the sum of all spans.
                elapsedTimeTotal += DateTime.Now - startTimeTotal;
            });
        }

        public IDisposable Start(string key)
        {
            var startTime = DateTime.Now;
            return new Handler(() =>
            {
                var elapsedTime = DateTime.Now - startTime;

                // First sighting wins for the timestamp; subsequent sightings sum
                // into the timing accumulator.
                customTimestamps.TryAdd(key, startTime);
                customTimings.AddOrUpdate(key, elapsedTime, (_, prev) => prev + elapsedTime);
            });
        }

        public string PlanToTrace(Plan plan)
        {
            return TraceGen.RenderProfile(InstantiationOrder, Timestamps, Timings);
        }
        public string PlanToString(Plan plan)
        {
            var timings = Timings
                .Select(kv => kv)
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"  - {kv.Key}: {kv.Value}")
                .Join("\n");
            
            
            var message = $"MonoModule({GetType().FullName}) Timings: \n{timings}\nTotal time: {TotalProducerTime}";
            message += $"\nMonoModule({GetType().FullName}) Trace: \n{PlanToTrace(plan)}";            
            message += $"\nMonoModule({GetType().FullName}) Plan: \n{plan}";
            return message;
        }

        public IDisposable Start(Key key)
        {
            var startTime = DateTime.Now;
            return new Handler(() =>
            {
                var elapsedTime = DateTime.Now - startTime;

                // First-sighting semantics: only the first observation of `key`
                // appends to the instantiation `order` list and seeds the
                // timestamp. Subsequent observations accumulate elapsed time.
                if (timestamps.TryAdd(key, startTime))
                {
                    lock (order) { order.Add(key); }
                }
                timings.AddOrUpdate(key, elapsedTime, (_, prev) => prev + elapsedTime);
            });
        }

        public TimeSpan TotalProducerTime => elapsedTimeTotal;

        // Snapshots: callers iterate these on the main thread but writers may
        // still be active in async scenarios; returning live ConcurrentDictionary
        // / List<T> references would leak unsafe iteration. Snapshot semantics
        // also preserve the prior Dictionary<,> / List<T> public surface.
        public List<Key> InstantiationOrder
        {
            get { lock (order) { return new List<Key>(order); } }
        }
        public Dictionary<Key, TimeSpan> Timings => new(timings);
        public Dictionary<Key, DateTime> Timestamps => new(timestamps);
    }
}