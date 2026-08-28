namespace DICS
{
    public interface IMutableLocator
    {
        public void Put<T>(Key key, T value) where T : notnull;
    }
}