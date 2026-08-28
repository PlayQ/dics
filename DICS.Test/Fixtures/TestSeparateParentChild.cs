using DICS.Attribute;
using DICS.TestDep;

namespace DICS.Test.Fixtures
{
    [LiftInitializer]
    public partial class TestSeparateParentChild : TestSeparateParentSuper
    {
        [Inject] [Id("test-sub")] public int _b1;
    }
}