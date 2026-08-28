using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DICS
{
    public interface IInitializer : IAbstractFunctoid<IInitializer>
    {
        public void Initialize(object instance, ILocator locator);


        public static IInitializer FromComponent<T>(Sig signature) where T : ILifecycleComponent
        {
            return new SelfInitializingInitializer<T>(signature, typeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get<T>(ILocator loc, Key key) where T : notnull
        {
            // the only regular case where we will make parent lookup is assisted injections in factories
            // Producer copies parent references into local context (see how Import instructions are handled)
            return loc.Resolve<T>(key);
        }

        public static IInitializer Lift<T1, T>(Action<T, Sig, T1> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
        {
            Debug.Assert(names == null || names.Length <= 1);
            var k1 = KeyN<T1>(names, 0);
            return new InitializerFromLocator<T>((self, sig, loc) => f(
                self,
                sig,
                Get<T1>(loc, k1)
            ), Sig.Of(
                k1
            ));
        }

        public static IInitializer Lift<T1, T2, T>(Action<T, Sig, T1, T2> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
            where T2 : notnull
        {
            Debug.Assert(names == null || names.Length <= 2);
            var k1 = KeyN<T1>(names, 0);
            var k2 = KeyN<T2>(names, 1);

            return new InitializerFromLocator<T>((self, sig, loc) => f(
                self,
                sig,
                Get<T1>(loc, k1),
                Get<T2>(loc, k2)
            ), Sig.Of(
                k1,
                k2
            ));
        }

        public static IInitializer Lift<T1, T2, T3, T>(Action<T, Sig, T1, T2, T3> f, string?[]? names = null)
            where T : notnull
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
        {
            Debug.Assert(names == null || names.Length <= 3);
            var k1 = KeyN<T1>(names, 0);
            var k2 = KeyN<T2>(names, 1);
            var k3 = KeyN<T3>(names, 2);

            return new InitializerFromLocator<T>((self, sig, loc) => f(
                self,
                sig,
                Get<T1>(loc, k1),
                Get<T2>(loc, k2),
                Get<T3>(loc, k3)
            ), Sig.Of(
                k1,
                k2,
                k3
            ));
        }

        public static IInitializer Lift<T1, T2, T3, T4, T>(Action<T, Sig, T1, T2, T3, T4> f, string?[]? names = null)
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

            return new InitializerFromLocator<T>((self, sig, loc) => f(
                self,
                sig,
                Get<T1>(loc, k1),
                Get<T2>(loc, k2),
                Get<T3>(loc, k3),
                Get<T4>(loc, k4)
            ), Sig.Of(
                k1,
                k2,
                k3,
                k4
            ));
        }

        public static IInitializer Lift<T1, T2, T3, T4, T5, T>(Action<T, Sig, T1, T2, T3, T4, T5> f,
            string?[]? names = null)
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

            return new InitializerFromLocator<T>((self, sig, loc) => f(
                self,
                sig,
                Get<T1>(loc, k1),
                Get<T2>(loc, k2),
                Get<T3>(loc, k3),
                Get<T4>(loc, k4),
                Get<T5>(loc, k5)
            ), Sig.Of(
                k1,
                k2,
                k3,
                k4,
                k5
            ));
        }

        public static IInitializer Lift<T1, T2, T3, T4, T5, T6, T>(Action<T, Sig, T1, T2, T3, T4, T5, T6> f,
            string?[]? names = null)
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

            return new InitializerFromLocator<T>((self, sig, loc) => f(
                self,
                sig,
                Get<T1>(loc, k1),
                Get<T2>(loc, k2),
                Get<T3>(loc, k3),
                Get<T4>(loc, k4),
                Get<T5>(loc, k5),
                Get<T6>(loc, k6)
            ), Sig.Of(
                k1,
                k2,
                k3,
                k4,
                k5,
                k6
            ));
        }


        public static IInitializer Lift<T1, T2, T3, T4, T5, T6, T7, T>(Action<T, Sig, T1, T2, T3, T4, T5, T6, T7> f,
            string?[]? names = null)
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

            return new InitializerFromLocator<T>((self, sig, loc) => f(
                self,
                sig,
                Get<T1>(loc, k1),
                Get<T2>(loc, k2),
                Get<T3>(loc, k3),
                Get<T4>(loc, k4),
                Get<T5>(loc, k5),
                Get<T6>(loc, k6),
                Get<T7>(loc, k7)
            ), Sig.Of(
                k1,
                k2,
                k3,
                k4,
                k5,
                k6,
                k7
            ));
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
    }

    public interface ILifecycleComponent
    {
        void Initialize(ILocator loc, Sig sig);
        Sig MakeSignature();
    }

    internal record SelfInitializingInitializer<T>(Sig Sig, Type Tpe) : IInitializer
        where T : ILifecycleComponent
    {
        public void Initialize(object instance, ILocator locator)
        {
            ((ILifecycleComponent)instance).Initialize(locator, Sig);
        }

        public Type Underlying()
        {
            return Tpe;
        }

        public IInitializer RenameArgs(string?[] names)
        {
            var newsig = Sig.RenameArgs(names);
            return new SelfInitializingInitializer<T>(newsig, Tpe);
        }

        public Sig Signature()
        {
            return Sig;
        }
    }

    public class InitializerFromLocator<T> : IInitializer
    {
        private readonly Action<T, Sig, ILocator> _fun;
        private readonly IDictionary<Key, Key>? _mapping;
        private readonly Sig _sig;

        public InitializerFromLocator(Action<T, Sig, ILocator> fun, Sig sig, IDictionary<Key, Key>? mapping = null)
        {
            _fun = fun;
            _sig = sig;
            _mapping = mapping;
        }


        public Sig Signature()
        {
            return _sig;
        }

        public Type Underlying()
        {
            return typeof(T);
        }

        public void Initialize(object instance, ILocator locator)
        {
            if (_mapping != null) _fun((T)instance, _sig, locator.Remap(_mapping));
            else _fun((T)instance, _sig, locator);
        }

        public IInitializer RenameArgs(string?[] names)
        {
            var newsig = _sig.RenameArgs(names);
            var mapping = _sig.Args.Zip(newsig.Args, (a, b) => new KeyValuePair<Key, Key>(a, b))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return new InitializerFromLocator<T>(_fun, newsig, mapping);
        }
    }
}