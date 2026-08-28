using System;
using System.Collections.Generic;

namespace DICS
{
    public abstract record Binding(Key Key, Type ImplType, IAxisPoint[] Points)
    {
        public record ToKey(Key Key, Key Source, IAxisPoint[] Points) : Binding(Key, Key.Tpe, Points);

        public record ToInstance(Key Key, object Instance, IAxisPoint[] Points)
            : Binding(Key, Instance.GetType(), Points);

        public record ToFunctoid(Key Key, IFunctoid Functoid, IAxisPoint[] Points)
            : Binding(Key, Functoid.Underlying(), Points);

        public record ToAsyncFunctoid(Key Key, IAsyncFunctoid Functoid, IAxisPoint[] Points)
            : Binding(Key, Functoid.Underlying(), Points);

        public record ToInitializer(Key Key, Key ExtractorKey, IInitializer Initializer, IAxisPoint[] Points)
            : Binding(Key, Initializer.Underlying(), Points);

        public record ToAsyncInitializer(Key Key, Key ExtractorKey, IAsyncInitializer Initializer, IAxisPoint[] Points)
            : Binding(Key, Initializer.Underlying(), Points);

        public record FactoryToFunctoid(Key Key, IFactoryZygote Zygote, IFunctoid Functoid, IAxisPoint[] Points)
            : Binding(Key, Functoid.Underlying(), Points);

        public record FactoryToLifecycle(
            Key Key,
            IFactoryZygote Zygote,
            IFunctoid Functoid,
            IInitializer Initializer,
            IAxisPoint[] Points
        )
            : Binding(Key, Functoid.Underlying(), Points);

        public record AddSetElement(Key Key, Key? ElementKey, ISetZygote SetZygote, Type ImplType, IAxisPoint[] Points)
            : Binding(Key, ImplType, Points);

        /// <summary>
        ///     Autoset expansion: one instruction carrying the element keys in order. Emitting
        ///     one <see cref="AddSetElement" /> per element instead loses that order, because
        ///     the planner indexes instructions into a <c>HashSet</c> before merging them.
        /// </summary>
        public record AddSetElements(
            Key Key,
            IReadOnlyList<Key> ElementKeys,
            ISetZygote SetZygote,
            Type ImplType,
            IAxisPoint[] Points)
            : Binding(Key, ImplType, Points);

        public record CreateAutoset(
        Key Key,
        Type ElementType,
        ISetZygote SetZygote,
        IMutableReferenceZygote RefZygote,
        bool IncludeImports,
        IAxisPoint[] Points
    )
        : Binding(Key, Key.Tpe, Points);

        public record ToDo(Key Key, string Message, IAxisPoint[] Points) : Binding(Key, Key.Tpe, Points);

        public record Import(Key Key, IAxisPoint[] Points) : Binding(Key, Key.Tpe, Points);

        // TODO: verify — IAbstractGeneratedFactoryFunctoid has no Underlying(); using
        // the alias's declared key type as the safe default for ImplType.
        public record FactoryToGeneratedFunctoid(
            Key key,
            IAbstractGeneratedFactoryFunctoid Functoid,
            IAxisPoint[] Points)
            : Binding(key, key.Tpe, Points);

        // TODO: verify — IAbstractGeneratedFactoryFunctoid has no Underlying(); using
        // the alias's declared key type as the safe default for ImplType.
        public record FactoryToGeneratedLifecycle(
            Key key,
            IFunctoid extractor,
            IAbstractGeneratedFactoryFunctoid Functoid,
            IAxisPoint[] Points
        ) : Binding(key, key.Tpe, Points);
    }
}