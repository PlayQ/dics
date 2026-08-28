using System;

namespace DICS.Attribute
{
    /// <summary>
    /// Marks a partial class so that DICS.Generator emits a runtime-introspectable
    /// <c>Factory</c> nested type with strongly-typed <c>Create(...)</c> methods that
    /// accept <see cref="Local"/>-tagged parameters at call-time.
    /// <para>
    /// The <see cref="FactoryKind"/> is normally auto-detected from the class shape:
    /// classes that have at least one <see cref="Inject"/>-marked field/property are
    /// treated as <see cref="FactoryKind.Initializer"/>; classes that have only
    /// constructor parameters are treated as <see cref="FactoryKind.Constructor"/>.
    /// Pass an explicit <see cref="FactoryKind"/> to override this detection.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class LiftFactory : System.Attribute
    {
        public LiftFactory()
        {
            Kind = null;
        }

        public LiftFactory(FactoryKind name)
        {
            Kind = name;
        }

        public FactoryKind? Kind { get; }
    }

    public enum FactoryKind
    {
        Constructor,
        Initializer
    }
}
