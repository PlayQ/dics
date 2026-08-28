namespace DICS.Test.Fixtures
{
    public class TesSubModule : Module
    {
        public TesSubModule()
        {
            Make<string>().From().Import();

            Make<TestClass2>().From().Functoid(IFunctoid.Lift((TestClass a) => new TestClass2(a)));
        }
    }
}