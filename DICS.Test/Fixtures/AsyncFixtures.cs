using System.Threading;
using System.Threading.Tasks;
using DICS.Attribute;

// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable FieldCanBeMadeReadOnly.Local

namespace DICS.Test.Fixtures
{
    public class AsyncDep
    {
        public string Value { get; }
        public AsyncDep(string value) { Value = value; }
    }

    public class AsyncWidget
    {
        public AsyncDep Dep { get; }
        public int Number { get; }
        public AsyncWidget(AsyncDep dep, int number) { Dep = dep; Number = number; }
    }

    // Constructed via async factory. [LiftConstructor] detects the static CreateAsync
    // method and emits LiftAsync() alongside Lift().
    [LiftConstructor]
    public partial class AsyncGenerated
    {
        public string Payload { get; }
        public AsyncDep Dep { get; }

        private AsyncGenerated(string payload, AsyncDep dep)
        {
            Payload = payload;
            Dep = dep;
        }

        public static async Task<AsyncGenerated> CreateAsync(AsyncDep dep, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            return new AsyncGenerated($"built:{dep.Value}", dep);
        }
    }

    // Async-initialized component. [LiftInitializer] detects InitializeAsync and
    // emits LiftAsyncInitializer() alongside LiftInitializer().
    [LiftInitializer]
    public partial class AsyncInitialized
    {
        [Inject] protected AsyncDep Dep = null!;
        public string? Loaded;

        public async Task InitializeAsync(CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            Loaded = $"loaded:{Dep.Value}";
        }
    }
}
