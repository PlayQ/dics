using System;

namespace DICS.Attribute
{
    /// <summary>
    ///     This attribute, when placed on fields, tells the Initializer Generator to use this field in the generated
    ///     initializer.
    ///     Can be combined with <see cref="Id" />
    ///     All the classes intended for automatic processing with DICS.Generator must be marked with <code>partial</code>
    ///     keyword
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class Inject : System.Attribute
    {
    }
}