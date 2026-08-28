using System;

namespace DICS.Attribute
{
    /// <summary>
    ///     This attribute, when placed on a class, tells DICS.Generator to create a runtime-introspectable constructor
    ///     representation.
    ///     When multiple constructors are defined, the one with most arguments will be used.
    ///     Processes <see cref="Id" /> annotations
    ///     All the classes intended for automatic processing with DICS.Generator must be marked with <code>partial</code>
    ///     keyword
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class LiftConstructor : System.Attribute
    {
    }
}