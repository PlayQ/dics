using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DICS.Test.Fixtures;
using NUnit.Framework;

namespace DICS.Test
{
    public class AsyncProducerTest
    {
        private static Injector NewInjector(Module module) =>
            new Injector(ILocator.Empty, IDicsMeasurement.FromDefault(), string.Empty, module.Freeze());

        // 1. Simple async constructor (manual Lift) produces the expected value.
        [Test]
        public async Task AsyncFunctoid_ProducesValue()
        {
            var module = new InlineModule(m =>
            {
                m.Make<int>().From().Instance(7);
                m.Make<string>().From().AsyncFunctoid(
                    IAsyncFunctoid.Lift(async (int i, CancellationToken ct) =>
                    {
                        await Task.Yield();
                        return $"value={i}";
                    })
                );
            });

            var loc = await NewInjector(module).ProduceAsync(CancellationToken.None, Key.Of<string>());

            Assert.That(loc.Get<string>(), Is.EqualTo("value=7"));
        }

        // 2. Sync producer must refuse a plan containing an async functoid.
        [Test]
        public void SyncProducer_RejectsAsyncOperations()
        {
            var module = new InlineModule(m =>
            {
                m.Make<int>().From().AsyncFunctoid(
                    IAsyncFunctoid.Lift((CancellationToken ct) => Task.FromResult(1))
                );
            });

            var injector = NewInjector(module);
            Assert.Throws<DicsProducerException>(() => injector.Produce(Key.Of<int>()));
        }

        // 3. Mixed sync + async graph: async key depending on sync key, both work.
        [Test]
        public async Task Mixed_SyncAndAsync_Graph()
        {
            var module = new InlineModule(m =>
            {
                m.Make<int>().From().Instance(40);
                m.Make<int>().Named("offset").From().Instance(2);
                m.Make<AsyncDep>().From().AsyncFunctoid(
                    IAsyncFunctoid.Lift(async (int i, int o, CancellationToken ct) =>
                    {
                        await Task.Delay(1, ct);
                        return new AsyncDep((i + o).ToString());
                    }, new[] { null, "offset" })
                );
                m.Make<AsyncWidget>().From().Functoid(
                    IFunctoid.Lift((AsyncDep d, int n) => new AsyncWidget(d, n))
                );
            });

            var loc = await NewInjector(module).ProduceAsync(CancellationToken.None,
                Key.Of<AsyncWidget>());

            var w = loc.Get<AsyncWidget>();
            Assert.That(w.Dep.Value, Is.EqualTo("42"));
            Assert.That(w.Number, Is.EqualTo(40));
        }

        // 4. Async initializer + sync extractor.
        [Test]
        public async Task AsyncLifecycle_SyncExtractor_AsyncInit()
        {
            var module = new InlineModule(m =>
            {
                m.Make<string>().From().Instance("hello");
                m.Make<Holder>().From().AsyncLifecycle(
                    IFunctoid.Lift(() => new Holder()),
                    IAsyncInitializer.Lift(async (Holder self, Sig _, string s, CancellationToken ct) =>
                    {
                        await Task.Yield();
                        self.Value = s.ToUpperInvariant();
                    })
                );
            });

            var loc = await NewInjector(module).ProduceAsync(CancellationToken.None, Key.Of<Holder>());

            Assert.That(loc.Get<Holder>().Value, Is.EqualTo("HELLO"));
        }

        // 5. Fully async lifecycle (async extractor + async initializer).
        [Test]
        public async Task AsyncLifecycle_FullyAsync()
        {
            var module = new InlineModule(m =>
            {
                m.Make<Holder>().From().AsyncLifecycle(
                    IAsyncFunctoid.Lift(async (CancellationToken ct) =>
                    {
                        await Task.Yield();
                        return new Holder { Value = "created" };
                    }),
                    IAsyncInitializer.Lift(async (Holder self, Sig _, CancellationToken ct) =>
                    {
                        await Task.Yield();
                        self.Value += ",initialized";
                    })
                );
            });

            var loc = await NewInjector(module).ProduceAsync(CancellationToken.None, Key.Of<Holder>());
            Assert.That(loc.Get<Holder>().Value, Is.EqualTo("created,initialized"));
        }

        // 6. Exception in async functoid propagates to the awaiting caller.
        [Test]
        public void AsyncFunctoid_ThrownException_Propagates()
        {
            var module = new InlineModule(m =>
            {
                m.Make<int>().From().AsyncFunctoid(
                    IAsyncFunctoid.Lift<int>(async ct =>
                    {
                        await Task.Yield();
                        throw new InvalidOperationException("boom");
                    })
                );
            });

            var injector = NewInjector(module);
            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await injector.ProduceAsync(CancellationToken.None, Key.Of<int>()));
            Assert.That(ex, Is.InstanceOf<InvalidOperationException>());
        }

        // 7. Cancellation: pre-cancelled token aborts the produce, and the functoid body
        //    must NOT be entered (the cancellation guard runs before any user code).
        [Test]
        public void Cancellation_PreCancelled()
        {
            var wasInvoked = false;
            var module = new InlineModule(m =>
            {
                m.Make<int>().From().AsyncFunctoid(
                    IAsyncFunctoid.Lift<int>(async ct =>
                    {
                        wasInvoked = true;
                        await Task.Delay(1000, ct);
                        return 1;
                    })
                );
            });
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.CatchAsync<OperationCanceledException>(
                async () => await NewInjector(module).ProduceAsync(cts.Token, Key.Of<int>()));
            Assert.That(wasInvoked, Is.False,
                "Functoid body must not be entered when the token is already cancelled at schedule time");
        }

        // H19: Mid-flight cancellation. Two parallel async functoids rendezvous to a
        // known "both started" state, then the CTS is cancelled. ProduceAsync must
        // throw an OperationCanceledException-derived exception AND the sibling must
        // not have observed a normal completion (its post-await flag stays false).
        [Test, CancelAfter(10_000)]
        public void Cancellation_MidFlight_AbortsInFlightFunctoids()
        {
            var bothStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var startedCount = 0;
            var aCompletedNormally = false;
            var bCompletedNormally = false;

            var cts = new CancellationTokenSource();

            async Task<int> StartThenAwaitCancellation(int value, CancellationToken ct, Action markCompleted)
            {
                if (Interlocked.Increment(ref startedCount) == 1)
                {
                    firstStarted.SetResult(0);
                    await secondStarted.Task.ConfigureAwait(false);
                }
                else
                {
                    secondStarted.SetResult(0);
                    await firstStarted.Task.ConfigureAwait(false);
                }
                bothStarted.TrySetResult(0);

                // Both functoids are now in flight. Park on cancellation token.
                var park = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (ct.Register(() => park.TrySetCanceled(ct)))
                    await park.Task.ConfigureAwait(false);

                markCompleted();
                return value;
            }

            var module = new InlineModule(m =>
            {
                m.Make<int>().Named("a").From().AsyncFunctoid(
                    IAsyncFunctoid.Lift((CancellationToken ct) =>
                        StartThenAwaitCancellation(1, ct, () => aCompletedNormally = true)));
                m.Make<int>().Named("b").From().AsyncFunctoid(
                    IAsyncFunctoid.Lift((CancellationToken ct) =>
                        StartThenAwaitCancellation(2, ct, () => bCompletedNormally = true)));
                m.Make<int>().From().Functoid(
                    IFunctoid.Lift((int a, int b) => a + b, new[] { "a", "b" })
                );
            });

            var injector = NewInjector(module);

            // Fire the cancellation after both functoids have entered.
            _ = Task.Run(async () =>
            {
                await bothStarted.Task.ConfigureAwait(false);
                cts.Cancel();
            });

            Assert.CatchAsync<OperationCanceledException>(
                async () => await injector.ProduceAsync(cts.Token, Key.Of<int>()));

            Assert.That(aCompletedNormally, Is.False,
                "Functoid 'a' must not have completed normally; cancellation should have aborted it");
            Assert.That(bCompletedNormally, Is.False,
                "Functoid 'b' must not have completed normally; cancellation should have aborted it");
        }

        // Parallel resolve under load — diamond DAG with one root depending on 7
        // sibling async functoids (max IFunctoid.Lift arity) that all share one async
        // leaf. Asserts that every sibling and the leaf were each invoked exactly once
        // even under concurrency (singleton semantics).
        [Test, CancelAfter(10_000)]
        public async Task ParallelResolve_DiamondFanOut_SingletonSemantics()
        {
            const int fanOut = 7;
            var leafInvocations = 0;
            var siblingInvocations = new int[fanOut];

            var module = new InlineModule(m =>
            {
                m.Make<int>().Named("leaf").From().AsyncFunctoid(
                    IAsyncFunctoid.Lift<int>(async ct =>
                    {
                        Interlocked.Increment(ref leafInvocations);
                        await Task.Yield();
                        return 1;
                    }));

                for (int i = 0; i < fanOut; i++)
                {
                    int captured = i;
                    m.Make<int>().Named($"s{captured}").From().AsyncFunctoid(
                        IAsyncFunctoid.Lift(async (int leaf, CancellationToken ct) =>
                        {
                            Interlocked.Increment(ref siblingInvocations[captured]);
                            await Task.Yield();
                            return leaf + captured;
                        }, new[] { "leaf" }));
                }

                var rootNames = new string?[fanOut];
                for (int i = 0; i < fanOut; i++) rootNames[i] = $"s{i}";
                m.Make<int>().From().Functoid(
                    IFunctoid.Lift(
                        (int s0, int s1, int s2, int s3, int s4, int s5, int s6)
                            => s0 + s1 + s2 + s3 + s4 + s5 + s6,
                        rootNames));
            });

            var loc = await NewInjector(module).ProduceAsync(CancellationToken.None, Key.Of<int>());

            // Root = sum_i (leaf + i) = fanOut*1 + (0+1+..+6) = 7 + 21 = 28.
            Assert.That(loc.Get<int>(), Is.EqualTo(28));
            Assert.That(Volatile.Read(ref leafInvocations), Is.EqualTo(1),
                "Shared leaf must be invoked exactly once even under concurrent dispatch");
            for (int i = 0; i < fanOut; i++)
                Assert.That(Volatile.Read(ref siblingInvocations[i]), Is.EqualTo(1),
                    $"Sibling s{i} must be invoked exactly once");
        }

        // 8. Parallelism (barrier-driven, no wall-clock): two independent async functoids
        //    are scheduled simultaneously. Each signals it has started, then awaits a
        //    shared barrier that is only released once both have signalled. If the producer
        //    ran them serially, the first would block forever on the barrier and the test
        //    would deadlock (caught by NUnit's per-test timeout).
        [Test, CancelAfter(5_000)]
        public async Task IndependentTasks_RunInParallel()
        {
            var started = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var alsoStarted = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var startedCount = 0;

            async Task<int> RendezvousAndReturn(int value, CancellationToken ct)
            {
                if (System.Threading.Interlocked.Increment(ref startedCount) == 1)
                {
                    started.SetResult(0);
                    // Wait for the sibling to also report it has started.
                    await alsoStarted.Task.ConfigureAwait(false);
                }
                else
                {
                    alsoStarted.SetResult(0);
                    await started.Task.ConfigureAwait(false);
                }
                return value;
            }

            var module = new InlineModule(m =>
            {
                m.Make<int>().Named("a").From().AsyncFunctoid(
                    IAsyncFunctoid.Lift((CancellationToken ct) => RendezvousAndReturn(1, ct)));
                m.Make<int>().Named("b").From().AsyncFunctoid(
                    IAsyncFunctoid.Lift((CancellationToken ct) => RendezvousAndReturn(2, ct)));
                m.Make<int>().From().Functoid(
                    IFunctoid.Lift((int a, int b) => a + b, new[] { "a", "b" })
                );
            });

            var loc = await NewInjector(module).ProduceAsync(CancellationToken.None, Key.Of<int>());

            Assert.That(loc.Get<int>(), Is.EqualTo(3));
            Assert.That(startedCount, Is.EqualTo(2),
                "Both async functoids must have entered before either completed (proves parallelism)");
        }

        // 9. Empty plan (no deps) still produces a valid locator.
        [Test]
        public async Task EmptyPlan_NoRoots_DoesNotHang()
        {
            var module = new InlineModule(_ => { });
            var loc = await NewInjector(module).ProduceAsync(CancellationToken.None);
            Assert.That(loc, Is.Not.Null);
        }

        // 10. Generator: [LiftAsyncConstructor] produces a working LiftAsync().
        [Test]
        public async Task Generator_AsyncConstructor()
        {
            var module = new InlineModule(m =>
            {
                m.Make<AsyncDep>().From().Instance(new AsyncDep("gen"));
                m.Make<AsyncGenerated>().From().AsyncFunctoid(AsyncGenerated.LiftAsync());
            });

            var loc = await NewInjector(module).ProduceAsync(CancellationToken.None, Key.Of<AsyncGenerated>());

            Assert.That(loc.Get<AsyncGenerated>().Payload, Is.EqualTo("built:gen"));
        }

        // 11. Generator: [LiftAsyncInitializer] sync field injection + async init.
        [Test]
        public async Task Generator_AsyncInitializer()
        {
            var module = new InlineModule(m =>
            {
                m.Make<AsyncDep>().From().Instance(new AsyncDep("xyz"));
                m.Make<AsyncInitialized>().From().AsyncLifecycle(
                    IFunctoid.Lift(() => new AsyncInitialized()),
                    AsyncInitialized.LiftAsyncInitializer()
                );
            });

            var loc = await NewInjector(module).ProduceAsync(CancellationToken.None, Key.Of<AsyncInitialized>());

            Assert.That(loc.Get<AsyncInitialized>().Loaded, Is.EqualTo("loaded:xyz"));
        }

        // 12. The IDicsMeasurement.StartTotal() handle must be disposed
        //     after all async work finishes, not when the Task is returned synchronously.
        [Test]
        public async Task ProduceAsync_TotalMeasurementSpansAllAsyncWork()
        {
            var measurement = new RecordingMeasurement();
            var module = new InlineModule(m =>
            {
                m.Make<int>().From().AsyncFunctoid(
                    IAsyncFunctoid.Lift<int>(async ct =>
                    {
                        await Task.Delay(50, ct).ConfigureAwait(false);
                        return 1;
                    })
                );
            });

            var injector = new Injector(ILocator.Empty, measurement, string.Empty, module.Freeze());
            await injector.ProduceAsync(CancellationToken.None, Key.Of<int>());

            var elapsedTicks = measurement.TotalEndTimestamp - measurement.TotalStartTimestamp;
            var elapsedMs = elapsedTicks * 1000.0 / Stopwatch.Frequency;
            Assert.That(elapsedMs, Is.GreaterThanOrEqualTo(50.0),
                $"StartTotal handle must span all async work; observed {elapsedMs} ms");
        }

        // 13. Plan-inconsistency must fail loudly via DicsBug, not silent zero-add.
        [Test]
        public void AsyncProducer_ReleaseDependee_MissingKey_ThrowsDicsBug()
        {
            var pendingDeps = new ConcurrentDictionary<Key, int>();
            pendingDeps[Key.Of<int>()] = 1;
            var missing = Key.Of<string>();
            Assert.Throws<DicsBug>(() => AsyncProducer.ReleaseDependee(pendingDeps, missing));
        }

        // 14. Every user-thrown exception is surfaced — multiple failures
        //     come back as AggregateException with all messages.
        [Test, CancelAfter(10_000)]
        public void AsyncFunctoid_MultipleFailures_AllSurfaced()
        {
            var startedCount = 0;
            var gate = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<int> GateThenThrow(string message, CancellationToken ct)
            {
                if (Interlocked.Increment(ref startedCount) == 2)
                    gate.SetResult(0);
                await gate.Task.ConfigureAwait(false);
                throw new InvalidOperationException(message);
            }

            var module = new InlineModule(m =>
            {
                m.Make<int>().Named("a").From().AsyncFunctoid(
                    IAsyncFunctoid.Lift((CancellationToken ct) => GateThenThrow("first", ct)));
                m.Make<int>().Named("b").From().AsyncFunctoid(
                    IAsyncFunctoid.Lift((CancellationToken ct) => GateThenThrow("second", ct)));
                m.Make<int>().From().Functoid(
                    IFunctoid.Lift((int a, int b) => a + b, new[] { "a", "b" })
                );
            });

            var injector = NewInjector(module);
            var ex = Assert.ThrowsAsync<AggregateException>(
                async () => await injector.ProduceAsync(CancellationToken.None, Key.Of<int>()));

            var messages = ex!.InnerExceptions.Select(e => e.Message).ToHashSet();
            Assert.That(messages, Is.EquivalentTo(new[] { "first", "second" }));
        }

        // 15a. Seeding-loop race: many independent fast seeds requested as roots in one
        //      ProduceAsync call. Each functoid body returns a fully-completed Task, so
        //      the pool-thread continuation finishes in microseconds. Without a sentinel
        //      on the completion counter, an early seed's `finally` decrements `remaining`
        //      to 0 between iterations of the seed foreach on the main thread, fires the
        //      TCS prematurely, and ProduceAsync returns/throws before later seeds have
        //      been scheduled. The PostProduce root-presence check then either fails with
        //      "Root missing in producer output" or the pathology guard trips with
        //      DicsBug — both are the race symptom. Repeated to amplify the race.
        [Test, Repeat(200), CancelAfter(60_000)]
        public async Task AsyncProducer_InitialSeeding_DoesNotCompletePrematurely()
        {
            const int seedCount = 4;

            var module = new InlineModule(m =>
            {
                for (int i = 0; i < seedCount; i++)
                {
                    int captured = i;
                    m.Make<int>().Named($"s{captured}").From().AsyncFunctoid(
                        IAsyncFunctoid.Lift<int>(ct => Task.FromResult(captured + 1)));
                }
            });

            var roots = new Key[seedCount];
            for (int i = 0; i < seedCount; i++) roots[i] = Key.Of<int>($"s{i}");

            // Use a no-op measurement: DefaultDicsMeasurement uses a non-concurrent
            // Dictionary internally and is not thread-safe under heavy parallel seeding —
            // an unrelated defect that would mask the race we are reproducing here.
            var injector = new Injector(ILocator.Empty, new NoopMeasurement(), string.Empty,
                module.Freeze());
            var loc = await injector.ProduceAsync(CancellationToken.None, roots);

            for (int i = 0; i < seedCount; i++)
                Assert.That(loc.Get<int>($"s{i}"), Is.EqualTo(i + 1), $"slot s{i}");
        }

        // 15. On failure, all in-flight tasks must have exited before
        //     ProduceAsync returns/throws.
        [Test, CancelAfter(10_000)]
        public void AsyncProducer_OnFailure_InFlightTasksDrainedBeforeReturn()
        {
            var f2Exited = 0;
            var f2Started = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            var module = new InlineModule(m =>
            {
                m.Make<int>().Named("a").From().AsyncFunctoid(
                    IAsyncFunctoid.Lift<int>(async ct =>
                    {
                        // Wait until F2 has actually started so the drain barrier is meaningful.
                        await f2Started.Task.ConfigureAwait(false);
                        await Task.Yield();
                        throw new InvalidOperationException("boom");
                    }));
                m.Make<int>().Named("b").From().AsyncFunctoid(
                    IAsyncFunctoid.Lift<int>(async ct =>
                    {
                        f2Started.TrySetResult(0);
                        try
                        {
                            await Task.Delay(2000, ct).ConfigureAwait(false);
                        }
                        finally
                        {
                            Interlocked.Exchange(ref f2Exited, 1);
                        }
                        return 0;
                    }));
                m.Make<int>().From().Functoid(
                    IFunctoid.Lift((int a, int b) => a + b, new[] { "a", "b" })
                );
            });

            var injector = NewInjector(module);
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await injector.ProduceAsync(CancellationToken.None, Key.Of<int>()));

            Assert.That(Volatile.Read(ref f2Exited), Is.EqualTo(1),
                "F2 must have exited (via OCE or completion) before ProduceAsync returned/threw");
        }
    }

    // Thread-safe no-op measurement for race-exposure tests. Avoids the unrelated
    // non-concurrent-Dictionary defect inside DefaultDicsMeasurement.
    internal sealed class NoopMeasurement : IDicsMeasurement
    {
        private static readonly IDisposable _instance = new NoopDisposable();
        public IDisposable StartTotal() => _instance;
        public IDisposable Start(Key key) => _instance;
        public IDisposable Start(string key) => _instance;
        public string PlanToString(Plan plan) => string.Empty;
        public string PlanToTrace(Plan plan) => string.Empty;
        private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
    }

    internal class RecordingMeasurement : IDicsMeasurement
    {
        public long TotalStartTimestamp;
        public long TotalEndTimestamp;
        private readonly DefaultDicsMeasurement _inner = new DefaultDicsMeasurement();

        public IDisposable StartTotal()
        {
            TotalStartTimestamp = Stopwatch.GetTimestamp();
            return new EndCapture(this);
        }

        public IDisposable Start(Key key) => _inner.Start(key);
        public IDisposable Start(string key) => _inner.Start(key);
        public string PlanToString(Plan plan) => _inner.PlanToString(plan);
        public string PlanToTrace(Plan plan) => _inner.PlanToTrace(plan);

        private sealed class EndCapture : IDisposable
        {
            private readonly RecordingMeasurement _owner;
            public EndCapture(RecordingMeasurement owner) => _owner = owner;
            public void Dispose() => _owner.TotalEndTimestamp = Stopwatch.GetTimestamp();
        }
    }

    internal class Holder
    {
        public string? Value;
    }
}
