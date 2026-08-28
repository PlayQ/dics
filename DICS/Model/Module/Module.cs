using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace DICS
{
    public static class AfterFromExtensions
    {
        public static AfterTo<ISet<TElement>> Auto<TElement>(this AfterFrom<ISet<TElement>> afterFrom)
        {
            var realKey = Key.Of<ISet<TElement>>().Rename(afterFrom.Key.Name);
            var setBinding =
                new Binding.CreateAutoset(realKey, typeof(TElement), new SetZygote<TElement>(),
                    new MutableReferenceZygoteImpl<ISet<TElement>>(),
                    false, afterFrom.Points);
            afterFrom.Module.AddBinding(setBinding);
            return new AfterTo<ISet<TElement>>(afterFrom.Module, realKey, afterFrom.Points);
        }

        public static AfterTo<IList<TElement>> Auto<TElement>(this AfterFrom<IList<TElement>> afterFrom)
        {
            var realKey = Key.Of<IList<TElement>>().Rename(afterFrom.Key.Name);
            var setBinding =
                new Binding.CreateAutoset(realKey, typeof(TElement), new ListZygote<TElement>(),
                    new MutableReferenceZygoteImpl<IList<TElement>>(),
                    false, afterFrom.Points);
            afterFrom.Module.AddBinding(setBinding);
            return new AfterTo<IList<TElement>>(afterFrom.Module, realKey, afterFrom.Points);
        }

        public static AfterTo<ISet<TElement>> Empty<TElement>(this AfterFrom<ISet<TElement>> afterFrom)
        {
            var setBinding =
                new Binding.AddSetElement(afterFrom.Key, null, new SetZygote<TElement>(), typeof(TElement),
                    afterFrom.Points);
            afterFrom.Module.AddBinding(setBinding);
            return new AfterTo<ISet<TElement>>(afterFrom.Module, afterFrom.Key, afterFrom.Points);
        }

        public static WordMakeSetImpl<TElement> Add<TElement>(this AfterMakeNamed<ISet<TElement>> afterFrom)
            where TElement : notnull
        {
            return new WordMakeSetImpl<TElement>(afterFrom.Module, afterFrom.Key, new IAxisPoint[] { });
        }

        public static WordMakeSetImpl<TElement> Add<TElement>(this AfterMakeNamedIn<ISet<TElement>> afterFrom)
            where TElement : notnull
        {
            return new WordMakeSetImpl<TElement>(afterFrom.Module, afterFrom.Key, afterFrom.Points);
        }

        public static WordMakeSetImpl<TElement> Add<TElement>(this AfterFrom<ISet<TElement>> afterFrom)
            where TElement : notnull
        {
            return new WordMakeSetImpl<TElement>(afterFrom.Module, afterFrom.Key, afterFrom.Points);
        }

        public static AfterMakeFactoryFromUntyped<T> UntypedFactory<T>(this AfterFrom<IUnsafeFactory<T>> afterFrom)
            where T : notnull
        {
            return new AfterMakeFactoryFromUntyped<T>(afterFrom.Module, afterFrom.Key, afterFrom.Points);
        }

        public static AfterMakeFactoryFromTyped<T> TypedFactory<T>(this AfterFrom<T> afterFrom)
            where T : IAbstractGeneratedFactory
        {
            return new AfterMakeFactoryFromTyped<T>(afterFrom.Module, afterFrom.Key, afterFrom.Points);
        }
    }

    public abstract class Module
    {
        internal readonly List<Binding> Bindings = new();
        internal readonly Dictionary<Key, ISet<Key>> ExtraDeps = new();

        internal readonly HashSet<Key> PrivateBindings = new();

        internal void AddBinding(Binding binding)
        {
            Bindings.Add(binding);
        }

        internal void MarkBindingPrivate(Key key)
        {
            PrivateBindings.Add(key);
        }

        internal void AddDep(Key key, Key extra)
        {
            if (!ExtraDeps.ContainsKey(key)) ExtraDeps[key] = new HashSet<Key>();

            ExtraDeps[key].Add(extra);
        }

        public ImmutableModule Freeze()
        {
            return new ImmutableModule(Bindings, ExtraDeps, PrivateBindings);
        }

        public AfterMake<T> Make<T>() where T : notnull
        {
            var key = new Key(typeof(T), null, null);
            return new AfterMake<T>(this, key);
        }
    }


    public class AfterMake<T> where T : notnull
    {
        internal readonly Key Key;
        internal readonly Module Module;

        public AfterMake(Module module, Key key)
        {
            Module = module;
            Key = key;
        }

        public AfterMakeNamed<T> Named(string name)
        {
            var key = new Key(Key.Tpe, name, null);
            return new AfterMakeNamed<T>(Module, key);
        }

        public AfterFrom<T> From()
        {
            return new AfterFrom<T>(Module, Key, new IAxisPoint[] { });
        }

        public void ToDo(string message = "to be done")
        {
            var binding = new Binding.ToDo(Key, message, new IAxisPoint[] { });
            Module.AddBinding(binding);
        }

        public AfterMakeIn<T> In(params IAxisPoint[] points)
        {
            return new AfterMakeIn<T>(Module, Key, points);
        }
    }


    public class AfterMakeNamed<T> where T : notnull
    {
        internal readonly Key Key;
        internal readonly Module Module;

        public AfterMakeNamed(Module module, Key key)
        {
            Module = module;
            Key = key;
        }

        public AfterFrom<T> From()
        {
            return new AfterFrom<T>(Module, Key, new IAxisPoint[] { });
        }

        public void ToDo(string message = "to be done")
        {
            var binding = new Binding.ToDo(Key, message, new IAxisPoint[] { });
            Module.AddBinding(binding);
        }

        public AfterMakeNamedIn<T> In(params IAxisPoint[] points)
        {
            return new AfterMakeNamedIn<T>(Module, Key, points);
        }
    }

    public class AfterMakeIn<T> where T : notnull
    {
        internal readonly Key Key;
        internal readonly Module Module;
        internal readonly IAxisPoint[] Points;

        public AfterMakeIn(Module module, Key key, IAxisPoint[] points)
        {
            Module = module;
            Key = key;
            Points = points;
        }

        public AfterMakeNamed<T> Named(string name)
        {
            var key = new Key(Key.Tpe, name, null);
            return new AfterMakeNamed<T>(Module, key);
        }

        public AfterFrom<T> From()
        {
            return new AfterFrom<T>(Module, Key, Points);
        }

        public void ToDo(string message = "to be done")
        {
            var binding = new Binding.ToDo(Key, message, Points);
            Module.AddBinding(binding);
        }
    }


    public class AfterMakeNamedIn<T> where T : notnull
    {
        internal readonly Key Key;
        internal readonly Module Module;
        internal readonly IAxisPoint[] Points;

        public AfterMakeNamedIn(Module module, Key key, IAxisPoint[] points)
        {
            Module = module;
            Key = key;
            Points = points;
        }

        public AfterFrom<T> From()
        {
            return new AfterFrom<T>(Module, Key, Points);
        }

        public void ToDo(string message = "to be done")
        {
            var binding = new Binding.ToDo(Key, message, Points);
            Module.AddBinding(binding);
        }
    }


    public class AfterMakeSetNamed<T> where T : notnull
    {
        internal readonly Module Module;
        internal readonly IAxisPoint[] Points;
        internal readonly Key SetKey;

        public AfterMakeSetNamed(Module module, Key setKey, IAxisPoint[] points)
        {
            Module = module;
            SetKey = setKey;
            Points = points;
        }

        public WordMakeSetImpl<T> Add()
        {
            return new WordMakeSetImpl<T>(Module, SetKey, Points);
        }
    }

    public class WordMakeSetImpl<TElement> where TElement : notnull
    {
        // Process-wide monotonically increasing counter used to mint synthetic
        // per-element key names. Previously the names were derived from
        // RuntimeHelpers.GetHashCode(value), which is a 32-bit identity hash and
        // can collide between distinct instances; a collision produced two
        // bindings with the same Key and threw "Key already present" at module
        // Freeze / Plan time. A monotonic counter is collision-free.
        private static int _uidCounter;

        private static int NextUid() => Interlocked.Increment(ref _uidCounter);

        internal readonly Module Module;
        internal readonly IAxisPoint[] Points;
        internal readonly Key SetKey;

        public WordMakeSetImpl(Module module, Key setKey, IAxisPoint[] points)
        {
            Module = module;
            SetKey = setKey;
            Points = points;
        }

        // TODO: dedup this
        public AfterMakeSetNamed<TElement> Instance(TElement value)
        {
            var id = NextUid();
            var elementKey = new Key(typeof(TElement), $"uidv:{id}", SetKey);

            var binding = new Binding.ToInstance(elementKey, value, Points);
            Module.AddBinding(binding);

            var elementBinding =
                new Binding.AddSetElement(SetKey, elementKey, new SetZygote<TElement>(), value.GetType(), Points);
            Module.AddBinding(elementBinding);

            return new AfterMakeSetNamed<TElement>(Module, SetKey, Points);
        }

        public AfterMakeSetNamed<TElement> Functoid(Func<ILocator, TElement> value, Sig signature)
        {
            var id = NextUid();
            var elementKey = new Key(typeof(TElement), $"uidf:{id}", SetKey);

            var Functoid = new FunctoidFromLocator<TElement>(value, signature);
            Debug.Assert(typeof(TElement).IsAssignableFrom(Functoid.Underlying()));

            var binding = new Binding.ToFunctoid(elementKey, Functoid, Points);
            Module.AddBinding(binding);

            var elementBinding =
                new Binding.AddSetElement(SetKey, elementKey, new SetZygote<TElement>(), Functoid.Underlying(), Points);
            Module.AddBinding(elementBinding);

            return new AfterMakeSetNamed<TElement>(Module, SetKey, Points);
        }

        public AfterMakeSetNamed<TElement> Functoid(IFunctoid functoid)
        {
            Debug.Assert(typeof(TElement).IsAssignableFrom(functoid.Underlying()));

            var id = NextUid();
            var elementKey = new Key(typeof(TElement), $"uidp:{id}", SetKey);

            var binding = new Binding.ToFunctoid(elementKey, functoid, Points);
            Module.AddBinding(binding);

            var elementBinding =
                new Binding.AddSetElement(SetKey, elementKey, new SetZygote<TElement>(), functoid.Underlying(), Points);
            Module.AddBinding(elementBinding);

            return new AfterMakeSetNamed<TElement>(Module, SetKey, Points);
        }
    }

    public class AfterMakeFactoryFromUntyped<TCreatable> where TCreatable : notnull
    {
        internal readonly Key Key;
        internal readonly Module Module;
        internal readonly IAxisPoint[] Points;

        public AfterMakeFactoryFromUntyped(Module module, Key key, IAxisPoint[] points)
        {
            Module = module;
            Key = key;
            Points = points;
        }

        public AfterMakeFactoryFromUntypedUsing<TCreatable> Using()
        {
            return new AfterMakeFactoryFromUntypedUsing<TCreatable>(Module, Key, Points);
        }
    }

    public class AfterMakeFactoryFromUntypedUsing<TCreatable> where TCreatable : notnull
    {
        internal readonly Key Key;
        internal readonly Module Module;
        internal readonly IAxisPoint[] Points;

        public AfterMakeFactoryFromUntypedUsing(Module module, Key key, IAxisPoint[] points)
        {
            Module = module;
            Key = key;
            Points = points;
        }

        public void ToDo(string message = "to be done")
        {
            var binding = new Binding.ToDo(Key, message, Points);
            Module.AddBinding(binding);
        }

        public AfterTo<TCreatable> Functoid(Func<ILocator, TCreatable> value, Sig signature)
        {
            var Functoid = new FunctoidFromLocator<TCreatable>(value, signature);
            var binding =
                new Binding.FactoryToFunctoid(Key, new FactoryZygote<TCreatable>(Functoid, null), Functoid, Points);
            Module.AddBinding(binding);
            return new AfterTo<TCreatable>(Module, Key, Points);
        }

        public AfterTo<TCreatable> Functoid(IFunctoid functoid)
        {
            Debug.Assert(typeof(TCreatable).IsAssignableFrom(functoid.Underlying()));
            var binding =
                new Binding.FactoryToFunctoid(Key, new FactoryZygote<TCreatable>(functoid, null), functoid, Points);
            Module.AddBinding(binding);
            return new AfterTo<TCreatable>(Module, Key, Points);
        }

        public AfterTo<TCreatable> Lifecycle(IFunctoid extractor, IInitializer initializer)
        {
            Debug.Assert(typeof(TCreatable).IsAssignableFrom(extractor.Underlying()));
            Debug.Assert(typeof(TCreatable).IsAssignableFrom(initializer.Underlying()));
            var binding = new Binding.FactoryToLifecycle(Key, new FactoryZygote<TCreatable>(extractor, initializer),
                extractor,
                initializer, Points);
            Module.AddBinding(binding);
            return new AfterTo<TCreatable>(Module, Key, Points);
        }
    }

    public class AfterMakeFactoryFromTyped<TCreatable> where TCreatable : IAbstractGeneratedFactory
    {
        internal readonly Key Key;
        internal readonly Module Module;
        internal readonly IAxisPoint[] Points;

        public AfterMakeFactoryFromTyped(Module module, Key key, IAxisPoint[] points)
        {
            Module = module;
            Key = key;
            Points = points;
        }

        public AfterMakeFactoryFromTypedUsing<TCreatable> Using()
        {
            return new AfterMakeFactoryFromTypedUsing<TCreatable>(Module, Key, Points);
        }
    }

    public class AfterMakeFactoryFromTypedUsing<TCreatable> where TCreatable : IAbstractGeneratedFactory
    {
        internal readonly Key Key;
        internal readonly Module Module;
        internal readonly IAxisPoint[] Points;

        public AfterMakeFactoryFromTypedUsing(Module module, Key key, IAxisPoint[] points)
        {
            Module = module;
            Key = key;
            Points = points;
        }

        public void ToDo(string message = "to be done")
        {
            var binding = new Binding.ToDo(Key, message, Points);
            Module.AddBinding(binding);
        }

        public AfterTo<TCreatable> Functoid<TK>(TK Functoid)
            where TK : IAbstractGeneratedFactoryFunctoid
        {
            var binding =
                new Binding.FactoryToGeneratedFunctoid(Key with { Tpe = typeof(TCreatable) }, Functoid, Points);
            Module.AddBinding(binding);
            return new AfterTo<TCreatable>(Module, Key, Points);
        }

        public AfterTo<TCreatable> Lifecycle<TK>(IFunctoid extractor, TK Functoid)
            where TK : IAbstractGeneratedFactoryFunctoid
        {
            var binding =
                new Binding.FactoryToGeneratedLifecycle(Key with { Tpe = typeof(TCreatable) }, extractor, Functoid,
                    Points);
            Module.AddBinding(binding);
            return new AfterTo<TCreatable>(Module, Key, Points);
        }
    }


    public class AfterFrom<T> where T : notnull
    {
        internal readonly Key Key;
        internal readonly Module Module;
        internal readonly IAxisPoint[] Points;

        public AfterFrom(Module module, Key key, IAxisPoint[] points)
        {
            Module = module;
            Key = key;
            Points = points;
        }


        /// <summary>
        ///     Declares a binding explicitly as an import from a parent locator. If you module is a submodule and you know
        ///     that a certain dependency needs to be imported from the locator hierarchy, it might be a good
        ///     idea to explicitly declare it as an import to have a conflict in case another binding defines the same dependency.
        /// </summary>
        public AfterTo<T> Import()
        {
            var binding = new Binding.Import(Key, Points);
            Module.AddBinding(binding);
            return new AfterTo<T>(Module, Key, Points);
        }

        public AfterTo<T> Ref<T1>() where T1 : notnull
        {
            var referenced = Key.Of<T1>();
            Debug.Assert(Key.Tpe.IsAssignableFrom(referenced.Tpe));
            var binding = new Binding.ToKey(Key, referenced, Points);
            Module.AddBinding(binding);
            return new AfterTo<T>(Module, Key, Points);
        }

        public AfterTo<T> Ref<T1>(string name) where T1 : notnull
        {
            var referenced = Key.Of<T1>(name);
            Debug.Assert(Key.Tpe.IsAssignableFrom(referenced.Tpe));
            var binding = new Binding.ToKey(Key, referenced, Points);
            Module.AddBinding(binding);
            return new AfterTo<T>(Module, Key, Points);
        }

        public AfterTo<T> Instance(T value)
        {
            var binding = new Binding.ToInstance(Key, value, Points);
            Module.AddBinding(binding);
            return new AfterTo<T>(Module, Key, Points);
        }

        public AfterTo<T> Functoid(IFunctoid functoid)
        {
            Debug.Assert(Key.Tpe.IsAssignableFrom(functoid.Underlying()));
            var binding = new Binding.ToFunctoid(Key, functoid, Points);
            Module.AddBinding(binding);
            return new AfterTo<T>(Module, Key, Points);
        }

        /// <summary>
        /// Bind this key to the result of an <see cref="IAsyncFunctoid"/>. The plan containing
        /// this binding can only be executed via <see cref="Injector.ProduceAsync(Plan,System.Threading.CancellationToken)"/>.
        /// </summary>
        public AfterTo<T> AsyncFunctoid(IAsyncFunctoid functoid)
        {
            Debug.Assert(Key.Tpe.IsAssignableFrom(functoid.Underlying()));
            var binding = new Binding.ToAsyncFunctoid(Key, functoid, Points);
            Module.AddBinding(binding);
            return new AfterTo<T>(Module, Key, Points);
        }

        public AfterTo<T> Lifecycle(IFunctoid extractor, IInitializer initializer)
        {
            Debug.Assert(Key.Tpe.IsAssignableFrom(extractor.Underlying()));
            Debug.Assert(Key.Tpe.IsAssignableFrom(initializer.Underlying()));

            var extractorKey = new Key(Key.Tpe, $"extractor[{Key}]", Key);
            var extractorBinding = new Binding.ToFunctoid(extractorKey, extractor, Points);
            var initializerBinding = new Binding.ToInitializer(Key, extractorKey, initializer, Points);

            Module.AddBinding(initializerBinding);
            Module.AddBinding(extractorBinding);

            return new AfterTo<T>(Module, Key, Points);
        }

        /// <summary>
        /// Bind this key to a (sync extractor, async initializer) lifecycle. Executable only via
        /// <see cref="Injector.ProduceAsync(Plan,System.Threading.CancellationToken)"/>.
        /// </summary>
        public AfterTo<T> AsyncLifecycle(IFunctoid extractor, IAsyncInitializer initializer)
        {
            Debug.Assert(Key.Tpe.IsAssignableFrom(extractor.Underlying()));
            Debug.Assert(Key.Tpe.IsAssignableFrom(initializer.Underlying()));

            var extractorKey = new Key(Key.Tpe, $"extractor[{Key}]", Key);
            var extractorBinding = new Binding.ToFunctoid(extractorKey, extractor, Points);
            var initializerBinding = new Binding.ToAsyncInitializer(Key, extractorKey, initializer, Points);

            Module.AddBinding(initializerBinding);
            Module.AddBinding(extractorBinding);

            return new AfterTo<T>(Module, Key, Points);
        }

        /// <summary>
        /// Bind this key to a fully async (extractor + initializer) lifecycle. Executable only via
        /// <see cref="Injector.ProduceAsync(Plan,System.Threading.CancellationToken)"/>.
        /// </summary>
        public AfterTo<T> AsyncLifecycle(IAsyncFunctoid extractor, IAsyncInitializer initializer)
        {
            Debug.Assert(Key.Tpe.IsAssignableFrom(extractor.Underlying()));
            Debug.Assert(Key.Tpe.IsAssignableFrom(initializer.Underlying()));

            var extractorKey = new Key(Key.Tpe, $"extractor[{Key}]", Key);
            var extractorBinding = new Binding.ToAsyncFunctoid(extractorKey, extractor, Points);
            var initializerBinding = new Binding.ToAsyncInitializer(Key, extractorKey, initializer, Points);

            Module.AddBinding(initializerBinding);
            Module.AddBinding(extractorBinding);

            return new AfterTo<T>(Module, Key, Points);
        }
    }

    public class AfterTo<T> where T : notnull
    {
        internal readonly Key Key;
        internal readonly Module Module;
        internal readonly IAxisPoint[] Points;

        public AfterTo(Module module, Key key, IAxisPoint[] points)
        {
            Module = module;
            Key = key;
            Points = points;
        }

        private AfterTo<T> Aliased(Key key)
        {
            if (!key.Tpe.IsAssignableFrom(Key.Tpe))
                throw new ArgumentException($"{Key.Tpe} must be a subtype of {key.Tpe}");
            var binding = new Binding.ToKey(key, Key, Points);
            Module.AddBinding(binding);
            return new AfterTo<T>(Module, Key, Points);
        }

        public AfterTo<T> Aliased<T1>() where T1 : notnull
        {
            return Aliased(Key.Of<T1>());
        }

        public AfterTo<T> Aliased<T1>(string name) where T1 : notnull
        {
            return Aliased(Key.Of<T1>(name));
        }

        public AfterTo<T> Private()
        {
            Module.MarkBindingPrivate(Key);
            return this;
        }


        private AfterTo<T> AddToSetOf<TE>(Key setKey)
        {
            var elementType = typeof(TE);
            if (!elementType.IsAssignableFrom(Key.Tpe))
                throw new ArgumentException($"{Key.Tpe} must be a subtype of {elementType}");

            var elementBinding = new Binding.AddSetElement(setKey, Key, new SetZygote<TE>(), typeof(TE), Points);
            Module.AddBinding(elementBinding);

            return new AfterTo<T>(Module, Key, Points);
        }


        public AfterTo<T> AddToSetOf<T1>() where T1 : notnull
        {
            return AddToSetOf<T1>(Key.Of<ISet<T1>>());
        }

        public AfterTo<T> AddToSetOf<T1>(string name) where T1 : notnull
        {
            return AddToSetOf<T1>(Key.Of<ISet<T1>>(name));
        }


        public AfterTo<T> AddDependency(Key key)
        {
            Module.AddDep(Key, key);
            return new AfterTo<T>(Module, Key, Points);
        }

        public AfterTo<T> AddDependency<T1>() where T1 : notnull
        {
            return AddDependency(Key.Of<T1>());
        }

        public AfterTo<T> AddDependency<T1>(string name) where T1 : notnull
        {
            return AddDependency(Key.Of<T1>(name));
        }
    }
}