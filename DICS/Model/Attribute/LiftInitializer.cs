using System;

namespace DICS.Attribute
{
    /// <summary>
    ///     This attribute, when placed on a class, tells DICS.Generator to generate an initializer method and create a
    ///     runtime-introspectable representation of it.
    ///     Processes <see cref="Id" /> and <see cref="Inject" /> annotations.
    ///     All the classes intended for automatic processing with DICS.Generator must be marked with <code>partial</code>
    ///     keyword
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class LiftInitializer : System.Attribute
    {
    }
}