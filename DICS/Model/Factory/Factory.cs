namespace DICS
{
    public interface IAnyFactory
    {
    }

    public interface IUnsafeFactory<T> : IAnyFactory
    {
        T Make(ILocator args);
    }

    public class UnsafeFactoryFunctoidImpl<T> : IUnsafeFactory<T>
    {
        protected readonly IFunctoid Functoid;
        protected readonly IInitializer? Initializer;
        protected readonly ILocator Locator;

        public UnsafeFactoryFunctoidImpl(IFunctoid functoid, ILocator locator, IInitializer? initializer)
        {
            Functoid = functoid;
            Locator = locator;
            Initializer = initializer;
        }

        public T Make(ILocator args)
        {
            var subloc = Locator.InheritedWithLocal(args);
            var ret = (T)Functoid.Invoke(subloc);
            if (Initializer != null) Initializer.Initialize(ret!, subloc);
            return ret;
        }
    }

    public interface IAbstractGeneratedFactory : IAnyFactory
    {
    }

    public interface IGeneratedFactory<T> : IAbstractGeneratedFactory
    {
    }

    public interface IAbstractGeneratedFactoryFunctoid
    {
        Sig MakeSignature();
        IAbstractGeneratedFactory Make(ILocator args, IFunctoid? extractor);
    }

    public interface IGeneratedFactoryFunctoid<T> : IAbstractGeneratedFactoryFunctoid
    {
    }
}