using System.Collections.Generic;

namespace DICS
{
    public class EmptyLocator : AbstractLocator
    {
        private static readonly EmptyLocator EmptyInstance = new();

        public override bool HasLocally(Key key)
        {
            return false;
        }

        public override ILocator Remap(IDictionary<Key, Key> mapping)
        {
            return this;
        }

        public override bool TryGet<T>(Key key, out T result)
        {
            // https://github.com/dotnet/roslyn/issues/76914
#pragma warning disable CS8601 // Possible null reference assignment.
            result = default;
#pragma warning restore CS8601 // Possible null reference assignment.
            return false;
        }

        protected override Key Mapped(Key key)
        {
            return key;
        }

        public override bool IsPrivate(Key key)
        {
            return false;
        }


        public override ILocator GetParent()
        {
            return EmptyInstance;
        }

        public override Instance[] DumpLocal()
        {
            return new Instance[] { };
        }

        public static ILocator Get()
        {
            return EmptyInstance;
        }

        public override string ToString()
        {
            return "<EmptyLocator>";
        }

        public EmptyLocator() : base(nameof(EmptyLocator))
        {
        }
    }
}