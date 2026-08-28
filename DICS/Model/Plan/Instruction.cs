using System.Collections.Generic;

namespace DICS
{
    public abstract record Instruction(Key Key, IAxisPoint[] Points)
    {
        public record Import(Key Key, IAxisPoint[] Points) : Instruction(Key, Points);

        public record CreateSet(Key Key, IReadOnlyList<Key> Elements, ISetZygote SetZygote, IAxisPoint[] Points)
            : Instruction(Key, Points);

        public record UseKey(Key Key, Key Source, IAxisPoint[] Points) : Instruction(Key, Points);

        public record UseInstance(Key Key, object Instance, IAxisPoint[] Points) : Instruction(Key, Points);

        public record CallFunctoid(Key Key, IFunctoid Functoid, IAxisPoint[] Points) : Instruction(Key, Points);

        public record CallAsyncFunctoid(Key Key, IAsyncFunctoid Functoid, IAxisPoint[] Points) : Instruction(Key, Points);

        public record CallAsyncInitializer(Key Key, Key ExtractorKey, IAsyncInitializer Initializer, IAxisPoint[] Points)
            : Instruction(Key, Points);

        public record CreateFunctoidFactory(Key Key, IFactoryZygote Zygote, IFunctoid Functoid, IAxisPoint[] Points)
            : Instruction(Key, Points);

        public record CallInitializer(Key Key, Key ExtractorKey, IInitializer Initializer, IAxisPoint[] Points)
            : Instruction(Key, Points);

        public record CreateLifecycleFactory(
            Key Key,
            IFactoryZygote Zygote,
            IFunctoid Functoid,
            IInitializer Initializer,
            IAxisPoint[] Points) : Instruction(Key, Points);

        public record ToDo(Key Key, string Message, IAxisPoint[] Points) : Instruction(Key, Points);

        public record FactoryToGeneratedFunctoid(
            Key Key,
            IAbstractGeneratedFactoryFunctoid Functoid,
            IAxisPoint[] Points)
            : Instruction(Key, Points);

        public record FactoryToGeneratedLifecycle(
            Key Key,
            IFunctoid Extractor,
            IAbstractGeneratedFactoryFunctoid Functoid,
            IAxisPoint[] Points
        ) : Instruction(Key, Points);
    }
}