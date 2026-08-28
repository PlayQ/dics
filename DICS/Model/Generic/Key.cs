using System;
using System.Text;

namespace DICS
{
    public record Key(Type Tpe, string? Name, Key? Prefix)
    {
        public StringBuilder BuildString(StringBuilder? maybeStringBuilder = null)
        {
            maybeStringBuilder ??= new StringBuilder();
            if (Prefix != null) maybeStringBuilder.Append($"{{{Prefix}}}:");
            maybeStringBuilder.Append(Tpe);
            if (Name != null) maybeStringBuilder.Append($"#{Name}");
            return maybeStringBuilder;
        }
        public override string ToString() => BuildString().ToString();

        public static Key Of<T>() where T : notnull
        {
            return new Key(typeof(T), null, null);
        }

        public static Key Of<T>(string name) where T : notnull
        {
            return new Key(typeof(T), name, null);
        }

        public Key Rename(string? name)
        {
            return new Key(Tpe, name, Prefix);
        }
    }

    public record Instance(Key Key, object Value);

    public interface IAxisPoint
    {
        string AxisName();
        string PointName();
    }
}