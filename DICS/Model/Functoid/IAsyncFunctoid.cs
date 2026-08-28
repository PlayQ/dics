using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DICS
{
    /// <summary>
    /// Async-aware counterpart to <see cref="IFunctoid"/>. Returns a <see cref="Task{TResult}"/>
    /// from <see cref="Invoke"/> and may use the supplied <see cref="CancellationToken"/>.
    /// <para>
    /// Async functoids can only be executed by <see cref="AsyncProducer"/> (i.e. via
    /// <see cref="Injector.ProduceAsync(System.Threading.CancellationToken,Key[])"/>).
    /// The synchronous <see cref="Producer"/> rejects them with a
    /// <see cref="DicsProducerException"/>.
    /// </para>
    /// </summary>
    public interface IAsyncFunctoid : IAbstractFunctoid<IAsyncFunctoid>
    {
        Task<object> Invoke(ILocator locator, CancellationToken ct);

        IAsyncFunctoid AddFakeDependencies(params Key[] args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get<T>(ILocator loc, Key key) where T : notnull
        {
            return loc.Resolve<T>(key);
        }

        public static Key KeyN<T>(string?[]? names, byte index) where T : notnull
        {
            if (names != null && index < names.Length)
            {
                var name = names[index];
                if (name != null) return Key.Of<T>(name);
            }
            return Key.Of<T>();
        }

        public static IAsyncFunctoid Lift<T>(Func<CancellationToken, Task<T>> f)
            where T : notnull
        {
            return new AsyncFunctoidFromLocator<T>((loc, ct) => f(ct), Sig.Of());
        }

        public static IAsyncFunctoid Lift<T1, T>(Func<T1, CancellationToken, Task<T>> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
        {
            Debug.Assert(names == null || names.Length <= 1);
            var k1 = KeyN<T1>(names, 0);
            return new AsyncFunctoidFromLocator<T>(
                (loc, ct) => f(Get<T1>(loc, k1), ct),
                Sig.Of(k1));
        }

        public static IAsyncFunctoid Lift<T1, T2, T>(Func<T1, T2, CancellationToken, Task<T>> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
            where T2 : notnull
        {
            Debug.Assert(names == null || names.Length <= 2);
            var k1 = KeyN<T1>(names, 0);
            var k2 = KeyN<T2>(names, 1);
            return new AsyncFunctoidFromLocator<T>(
                (loc, ct) => f(Get<T1>(loc, k1), Get<T2>(loc, k2), ct),
                Sig.Of(k1, k2));
        }

        public static IAsyncFunctoid Lift<T1, T2, T3, T>(Func<T1, T2, T3, CancellationToken, Task<T>> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
        {
            Debug.Assert(names == null || names.Length <= 3);
            var k1 = KeyN<T1>(names, 0);
            var k2 = KeyN<T2>(names, 1);
            var k3 = KeyN<T3>(names, 2);
            return new AsyncFunctoidFromLocator<T>(
                (loc, ct) => f(Get<T1>(loc, k1), Get<T2>(loc, k2), Get<T3>(loc, k3), ct),
                Sig.Of(k1, k2, k3));
        }

        public static IAsyncFunctoid Lift<T1, T2, T3, T4, T>(Func<T1, T2, T3, T4, CancellationToken, Task<T>> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
        {
            Debug.Assert(names == null || names.Length <= 4);
            var k1 = KeyN<T1>(names, 0);
            var k2 = KeyN<T2>(names, 1);
            var k3 = KeyN<T3>(names, 2);
            var k4 = KeyN<T4>(names, 3);
            return new AsyncFunctoidFromLocator<T>(
                (loc, ct) => f(Get<T1>(loc, k1), Get<T2>(loc, k2), Get<T3>(loc, k3), Get<T4>(loc, k4), ct),
                Sig.Of(k1, k2, k3, k4));
        }

        public static IAsyncFunctoid Lift<T1, T2, T3, T4, T5, T>(Func<T1, T2, T3, T4, T5, CancellationToken, Task<T>> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull
        {
            Debug.Assert(names == null || names.Length <= 5);
            var k1 = KeyN<T1>(names, 0);
            var k2 = KeyN<T2>(names, 1);
            var k3 = KeyN<T3>(names, 2);
            var k4 = KeyN<T4>(names, 3);
            var k5 = KeyN<T5>(names, 4);
            return new AsyncFunctoidFromLocator<T>(
                (loc, ct) => f(Get<T1>(loc, k1), Get<T2>(loc, k2), Get<T3>(loc, k3), Get<T4>(loc, k4), Get<T5>(loc, k5), ct),
                Sig.Of(k1, k2, k3, k4, k5));
        }

        public static IAsyncFunctoid Lift<T1, T2, T3, T4, T5, T6, T>(Func<T1, T2, T3, T4, T5, T6, CancellationToken, Task<T>> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull
            where T6 : notnull
        {
            Debug.Assert(names == null || names.Length <= 6);
            var k1 = KeyN<T1>(names, 0);
            var k2 = KeyN<T2>(names, 1);
            var k3 = KeyN<T3>(names, 2);
            var k4 = KeyN<T4>(names, 3);
            var k5 = KeyN<T5>(names, 4);
            var k6 = KeyN<T6>(names, 5);
            return new AsyncFunctoidFromLocator<T>(
                (loc, ct) => f(Get<T1>(loc, k1), Get<T2>(loc, k2), Get<T3>(loc, k3), Get<T4>(loc, k4), Get<T5>(loc, k5), Get<T6>(loc, k6), ct),
                Sig.Of(k1, k2, k3, k4, k5, k6));
        }

        public static IAsyncFunctoid Lift<T1, T2, T3, T4, T5, T6, T7, T>(Func<T1, T2, T3, T4, T5, T6, T7, CancellationToken, Task<T>> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull
            where T6 : notnull
            where T7 : notnull
        {
            Debug.Assert(names == null || names.Length <= 7);
            var k1 = KeyN<T1>(names, 0);
            var k2 = KeyN<T2>(names, 1);
            var k3 = KeyN<T3>(names, 2);
            var k4 = KeyN<T4>(names, 3);
            var k5 = KeyN<T5>(names, 4);
            var k6 = KeyN<T6>(names, 5);
            var k7 = KeyN<T7>(names, 6);
            return new AsyncFunctoidFromLocator<T>(
                (loc, ct) => f(Get<T1>(loc, k1), Get<T2>(loc, k2), Get<T3>(loc, k3), Get<T4>(loc, k4), Get<T5>(loc, k5), Get<T6>(loc, k6), Get<T7>(loc, k7), ct),
                Sig.Of(k1, k2, k3, k4, k5, k6, k7));
        }
    }

    public class AsyncFunctoidFromLocator<T> : IAsyncFunctoid where T : notnull
    {
        private readonly Func<ILocator, CancellationToken, Task<T>> _fun;
        private readonly IDictionary<Key, Key>? _mapping;
        private readonly Sig _sig;

        public AsyncFunctoidFromLocator(Func<ILocator, CancellationToken, Task<T>> fun, Sig sig, IDictionary<Key, Key>? mapping = null)
        {
            _fun = fun;
            _sig = sig;
            _mapping = mapping;
        }

        public async Task<object> Invoke(ILocator locator, CancellationToken ct)
        {
            var resolved = _mapping != null ? locator.Remap(_mapping) : locator;
            var result = await _fun(resolved, ct).ConfigureAwait(false);
            return result!;
        }

        public Sig Signature() => _sig;
        public Type Underlying() => typeof(T);

        public IAsyncFunctoid RenameArgs(string?[] names)
        {
            var newsig = _sig.RenameArgs(names);
            var mapping = _sig.Args.Zip(newsig.Args, (a, b) => new KeyValuePair<Key, Key>(a, b))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return new AsyncFunctoidFromLocator<T>(_fun, newsig, mapping);
        }

        public IAsyncFunctoid AddFakeDependencies(params Key[] args)
        {
            return new AsyncFunctoidFromLocator<T>(_fun, _sig.ExtendUnchecked(args));
        }
    }
}
