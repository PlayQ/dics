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
    /// Async-aware counterpart to <see cref="IInitializer"/>. The <see cref="Initialize"/>
    /// method returns a <see cref="Task"/> and may use the supplied
    /// <see cref="CancellationToken"/>. Executable only by <see cref="AsyncProducer"/>.
    /// </summary>
    public interface IAsyncInitializer : IAbstractFunctoid<IAsyncInitializer>
    {
        Task Initialize(object instance, ILocator locator, CancellationToken ct);

        public static IAsyncInitializer FromComponent<T>(Sig signature) where T : IAsyncLifecycleComponent
        {
            return new SelfInitializingAsyncInitializer<T>(signature, typeof(T));
        }

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

        public static IAsyncInitializer Lift<T>(Func<T, Sig, CancellationToken, Task> f)
            where T : notnull
        {
            return new AsyncInitializerFromLocator<T>((self, sig, loc, ct) => f(self, sig, ct), Sig.Of());
        }

        public static IAsyncInitializer Lift<T1, T>(Func<T, Sig, T1, CancellationToken, Task> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
        {
            Debug.Assert(names == null || names.Length <= 1);
            var k1 = KeyN<T1>(names, 0);
            return new AsyncInitializerFromLocator<T>(
                (self, sig, loc, ct) => f(self, sig, Get<T1>(loc, k1), ct),
                Sig.Of(k1));
        }

        public static IAsyncInitializer Lift<T1, T2, T>(Func<T, Sig, T1, T2, CancellationToken, Task> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
            where T2 : notnull
        {
            Debug.Assert(names == null || names.Length <= 2);
            var k1 = KeyN<T1>(names, 0);
            var k2 = KeyN<T2>(names, 1);
            return new AsyncInitializerFromLocator<T>(
                (self, sig, loc, ct) => f(self, sig, Get<T1>(loc, k1), Get<T2>(loc, k2), ct),
                Sig.Of(k1, k2));
        }

        public static IAsyncInitializer Lift<T1, T2, T3, T>(Func<T, Sig, T1, T2, T3, CancellationToken, Task> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
        {
            Debug.Assert(names == null || names.Length <= 3);
            var k1 = KeyN<T1>(names, 0);
            var k2 = KeyN<T2>(names, 1);
            var k3 = KeyN<T3>(names, 2);
            return new AsyncInitializerFromLocator<T>(
                (self, sig, loc, ct) => f(self, sig, Get<T1>(loc, k1), Get<T2>(loc, k2), Get<T3>(loc, k3), ct),
                Sig.Of(k1, k2, k3));
        }

        public static IAsyncInitializer Lift<T1, T2, T3, T4, T>(Func<T, Sig, T1, T2, T3, T4, CancellationToken, Task> f, string?[]? names = null)
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
            return new AsyncInitializerFromLocator<T>(
                (self, sig, loc, ct) => f(self, sig, Get<T1>(loc, k1), Get<T2>(loc, k2), Get<T3>(loc, k3), Get<T4>(loc, k4), ct),
                Sig.Of(k1, k2, k3, k4));
        }
    }

    /// <summary>
    /// Async counterpart to <see cref="ILifecycleComponent"/>. Implemented by classes whose
    /// initialization is asynchronous. The generator emits implementations of this interface
    /// for classes tagged with <c>[LiftAsyncInitializer]</c>.
    /// </summary>
    public interface IAsyncLifecycleComponent
    {
        Task Initialize(ILocator loc, Sig sig, CancellationToken ct);
        Sig MakeSignature();
    }

    internal record SelfInitializingAsyncInitializer<T>(Sig Sig, Type Tpe) : IAsyncInitializer
        where T : IAsyncLifecycleComponent
    {
        public Task Initialize(object instance, ILocator locator, CancellationToken ct)
        {
            return ((IAsyncLifecycleComponent)instance).Initialize(locator, Sig, ct);
        }

        public Type Underlying() => Tpe;

        public IAsyncInitializer RenameArgs(string?[] names)
        {
            var newsig = Sig.RenameArgs(names);
            return new SelfInitializingAsyncInitializer<T>(newsig, Tpe);
        }

        public Sig Signature() => Sig;
    }

    public class AsyncInitializerFromLocator<T> : IAsyncInitializer where T : notnull
    {
        private readonly Func<T, Sig, ILocator, CancellationToken, Task> _fun;
        private readonly IDictionary<Key, Key>? _mapping;
        private readonly Sig _sig;

        public AsyncInitializerFromLocator(Func<T, Sig, ILocator, CancellationToken, Task> fun, Sig sig, IDictionary<Key, Key>? mapping = null)
        {
            _fun = fun;
            _sig = sig;
            _mapping = mapping;
        }

        public Sig Signature() => _sig;
        public Type Underlying() => typeof(T);

        public Task Initialize(object instance, ILocator locator, CancellationToken ct)
        {
            var resolved = _mapping != null ? locator.Remap(_mapping) : locator;
            return _fun((T)instance, _sig, resolved, ct);
        }

        public IAsyncInitializer RenameArgs(string?[] names)
        {
            var newsig = _sig.RenameArgs(names);
            var mapping = _sig.Args.Zip(newsig.Args, (a, b) => new KeyValuePair<Key, Key>(a, b))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return new AsyncInitializerFromLocator<T>(_fun, newsig, mapping);
        }
    }
}
