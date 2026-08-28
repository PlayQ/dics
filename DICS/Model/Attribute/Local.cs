using System;

namespace DICS.Attribute
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property)]
    public class Local : System.Attribute
    {
    }
}