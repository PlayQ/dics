namespace DICS.Test
{
    /// <summary>
    /// Shared helper that lets a test build an ad-hoc <see cref="Module"/> via a lambda
    /// without declaring a one-off subclass per test file.
    /// </summary>
    internal sealed class InlineModule : Module
    {
        public InlineModule(System.Action<Module> configure) => configure(this);
    }
}
