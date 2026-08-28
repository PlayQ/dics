using System;

namespace DICS.Attribute
{
    /// <summary>
    ///     Use this attribute to specify names for of the dependencies extracted by DICS.Generator from parameters and fields.
    ///     All the classes intended for automatic processing with DICS.Generator must be marked with <code>partial</code>
    ///     keyword
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
    public class Id : System.Attribute
    {
        public Id(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}