using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace DICS
{
    /// <summary>
    /// Async-aware <see cref="Plan"/> interpreter. Handles every <see cref="Instruction"/>
    /// the synchronous <see cref="Producer"/> handles, plus
    /// <see cref="Instruction.CallAsyncFunctoid"/> and
    /// <see cref="Instruction.CallAsyncInitializer"/>.
    /// <para>
    /// Independent ready keys run concurrently: when a key completes, every key that was
    /// only waiting for it is launched on the .NET thread pool. Cancellation propagates;
    /// the first exception thrown by any operation is surfaced through the awaiting task.
    /// </para>
    /// <para>
    /// Note: <see cref="LocatorImpl"/> is backed by <see cref="ConcurrentDictionary{TKey,TValue}"/>,
    /// so concurrent <c>Put</c> from independent ready keys is safe. Functoids and initializers
    /// must not mutate shared external state.
    /// </para>
    /// </summary>
    public class AsyncProducer
    {
        private readonly IDicsMeasurement _dicsMeasurement;
        private readonly Injector _owner;
        private readonly string _ownerName;
        private readonly ILocator _parent;

        public AsyncProducer(ILocator parent, Injector owner, string ownerName, IDicsMeasurement dicsMeasurement)
        {
            _parent = parent;
            _owner = owner;
            _ownerName = ownerName;
            _dicsMeasurement = dicsMeasurement;
        }

        public async Task<ILocator> Produce(Plan plan, CancellationToken ct)
        {
            if (plan.plannedBy != _owner)
                throw new DicsBug(
                    $"Plan was produced by a different Injector instance than the one running it; cross-injector plan reuse is not supported.");

            LocatorImpl loc;
            DepMatrix<Instruction> revdep;
            ConcurrentDictionary<Key, int> pendingDeps;
            int totalKeys;

            using (_dicsMeasurement.Start("PreProduce"))
            {
                loc = new LocatorImpl(_parent, plan.Private, _ownerName);
                revdep = plan.Matrix.Transpose();

                pendingDeps = new ConcurrentDictionary<Key, int>();
                foreach (var kv in plan.Matrix.Links)
                    pendingDeps[kv.Key] = kv.Value.Count;

                totalKeys = pendingDeps.Count;
            }

            if (totalKeys > 0)
            {
                // Completion barrier (§3 Q5): single counter + tcs. Counter starts at 0
                // and is incremented BEFORE each Task.Run is fired; the matching decrement
                // happens in a finally block, so it fires for normal completion, throw,
                // and observed cancellation alike. When the counter reaches 0, the tcs is
                // completed. Per-failure paths NEVER touch the tcs directly — they only
                // record into the exception bags and signal linkedCts.
                var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                var remaining = 0;
                var userThrown = new ConcurrentBag<Exception>();
                var cancellationDerived = new ConcurrentBag<OperationCanceledException>();
                var hasUserCause = 0;
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                void Schedule(Key k)
                {
                    Interlocked.Increment(ref remaining);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Cancellation guard sits AFTER the increment — the matching
                            // decrement in finally keeps the counter invariant whatever
                            // happens next.
                            if (linkedCts.IsCancellationRequested) return;
                            linkedCts.Token.ThrowIfCancellationRequested();

                            using (_dicsMeasurement.Start(k))
                            {
                                await ExecuteAsync(loc, plan.Matrix.Data[k], k, revdep, linkedCts.Token).ConfigureAwait(false);
                            }

                            if (revdep.Links.TryGetValue(k, out var dependees))
                            {
                                foreach (var d in dependees)
                                {
                                    var newCount = ReleaseDependee(pendingDeps, d);
                                    if (newCount == 0)
                                        Schedule(d);
                                }
                            }
                        }
                        catch (OperationCanceledException oce)
                        {
                            cancellationDerived.Add(oce);
                        }
                        catch (Exception ex)
                        {
                            userThrown.Add(ex);
                            Interlocked.Exchange(ref hasUserCause, 1);
                            // Signal sibling tasks to stop; their finally blocks still
                            // run and decrement the counter normally.
                            try { linkedCts.Cancel(); }
                            catch (ObjectDisposedException) { /* race with disposal at end of Produce */ }
                        }
                        finally
                        {
                            if (Interlocked.Decrement(ref remaining) == 0)
                                tcs.TrySetResult(0);
                        }
                    }, CancellationToken.None);
                }

                // Sentinel ensures the initial seeding loop completes before the completion
                // barrier can fire. Without it, a fast-completing seed can decrement remaining
                // to zero between iterations of the foreach, causing tcs.TrySetResult to fire
                // prematurely while later seeds are still being scheduled.
                Interlocked.Increment(ref remaining);
                var anySeedScheduled = false;
                try
                {
                    foreach (var kv in pendingDeps)
                        if (kv.Value == 0)
                        {
                            anySeedScheduled = true;
                            Schedule(kv.Key);
                        }

                    // Plan-pathology guard: totalKeys > 0 with no zero-count seeds means
                    // a cycle (or a plan with no schedulable entry point). We cannot read
                    // `remaining` for this check because in-flight seeds may have already
                    // decremented it, so use a separate flag set during the seeding loop.
                    if (!anySeedScheduled && totalKeys > 0)
                        throw new DicsBug("Plan has keys but no ready seeds — cycle?");
                }
                finally
                {
                    // Release sentinel. If we are the last contributor, fire the TCS.
                    // This runs even if the pathology guard threw — we want the await to
                    // unblock so the exception can propagate normally.
                    if (Interlocked.Decrement(ref remaining) == 0)
                        tcs.TrySetResult(0);
                }

                await tcs.Task.ConfigureAwait(false);

                // Surface exceptions per §3 Q1 + Q4.
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException(ct);

                var userArr = userThrown.ToArray();
                if (userArr.Length == 1)
                    ExceptionDispatchInfo.Capture(userArr[0]).Throw();
                if (userArr.Length >= 2)
                    throw new AggregateException(userArr);

                // hasUserCause==0 path: surface cancellation-derived exceptions (user code
                // called ThrowIfCancellationRequested voluntarily, no other cause).
                var ocArr = cancellationDerived.ToArray();
                if (ocArr.Length == 1)
                    ExceptionDispatchInfo.Capture(ocArr[0]).Throw();
                if (ocArr.Length >= 2)
                    throw new AggregateException(ocArr);
            }

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

            return loc;
        }

        // Decrement the pending-dependency counter for a key that has just become a
        // dependee of a freshly-produced one. If the key is absent from the forward map,
        // the plan is internally inconsistent — fail loud rather than silently
        // inventing a zero counter (which would phantom-dispatch a key whose Data slot
        // does not exist, then push `remaining` below zero).
        internal static int ReleaseDependee(ConcurrentDictionary<Key, int> pendingDeps, Key d)
        {
            return pendingDeps.AddOrUpdate(
                d,
                _ => throw new DicsBug(
                    $"pendingDeps missing entry for dependee {d}; plan matrix inconsistent " +
                    $"(reverse-dependency edge points at a key not in the forward map)"),
                (_, v) => v - 1);
        }

        private async Task ExecuteAsync(
            LocatorImpl loc,
            Instruction defn,
            Key key,
            DepMatrix<Instruction> dependees,
            CancellationToken ct)
        {
            switch (defn)
            {
                case Instruction.CallAsyncFunctoid callAsync:
                {
                    var result = await callAsync.Functoid.Invoke(loc, ct).ConfigureAwait(false);
                    loc.Put(callAsync.Key, result);
                    return;
                }
                case Instruction.CallAsyncInitializer callAsyncInit:
                {
                    var instance = loc.Get<object>(callAsyncInit.ExtractorKey);
                    await callAsyncInit.Initializer.Initialize(instance, loc, ct).ConfigureAwait(false);
                    loc.Put(callAsyncInit.Key, instance);
                    return;
                }
                default:
                    if (!InstructionDispatcher.TryExecuteSync(loc, defn, key, dependees, _parent))
                        throw new DicsBug($"Unhandled async instruction: {defn}");
                    return;
            }
        }
    }
}
