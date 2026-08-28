using DICS.Attribute;

// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable FieldCanBeMadeReadOnly.Local

namespace DICS.Test.Fixtures
{
    // No class-level Lift attribute. Presence of [Inject] alone should make the
    // generator emit LiftInitializer() and IInitializer-friendly members.
    public partial class InferredInitializerOnly
    {
        [Inject] protected ITestSuper Dep = null!;
        [Inject] [Id("test-set")] protected System.Collections.Generic.ISet<string> Strings = null!;

        public ITestSuper GetDep() => Dep;
        public int StringCount() => Strings.Count;
    }

    // No class-level Lift attribute. Presence of [Local] on a primary-constructor
    // parameter should make the generator emit a typed Factory nested type.
    public partial record InferredFactoryRecord(
        ITestSuper Dep,
        [Local] int Number);

    // Both markers on the same class. Inference produces BOTH LiftInitializer() and
    // a typed Factory (the user can pick either binding path).
    public partial class InferredInitializerAndFactory
    {
        [Inject] protected ITestSuper Dep = null!;
        [Inject] [Local] protected int Number;

        public ITestSuper GetDep() => Dep;
        public int GetNumber() => Number;
    }
}
