namespace DICS
{
    public interface IFactoryZygote
    {
        public object Create(ILocator locator);
    }

    internal class FactoryZygote<T> : IFactoryZygote
    {
        private readonly IFunctoid _functoid;
        private readonly IInitializer? _initializer;

        public FactoryZygote(IFunctoid functoid, IInitializer? initializer)
        {
            _functoid = functoid;
            _initializer = initializer;
        }

        public object Create(ILocator locator)
        {
            var result = new UnsafeFactoryFunctoidImpl<T>(_functoid, locator, _initializer);
            return result;
        }
    }
}