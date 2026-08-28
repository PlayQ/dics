using DICS.Attribute;

namespace DICS.TestDep
{
    [LiftInitializer]
    public partial class TestSeparateParentSuper
    {
        // [Inject] [Id("str-pub")] private string? _strPrivate;
        [Inject] [Id("str-prot")] protected string? _strProtected;
    }
}