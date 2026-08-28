using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DICS
{
    public class DepMatrix<T>
    {
        public DepMatrix(IDictionary<Key, ISet<Key>> links, IDictionary<Key, T> data)
        {
            Links = new Dictionary<Key, ISet<Key>>();
            Data = new Dictionary<Key, T>();

            foreach (var kvp in links)
            {
                var rowKey = kvp.Key;
                var rowValues = kvp.Value;

                if (!Links.ContainsKey(rowKey)) Links[rowKey] = new HashSet<Key>();

                foreach (var columnKey in rowValues)
                {
                    if (!Links.ContainsKey(columnKey)) Links[columnKey] = new HashSet<Key>();

                    Links[rowKey].Add(columnKey);
                }
            }

            foreach (var kvp in data)
            {
                var dataKey = kvp.Key;

                if (!Links.ContainsKey(dataKey)) Links[dataKey] = new HashSet<Key>();
                Data[dataKey] = kvp.Value;
            }
        }

        public IDictionary<Key, ISet<Key>> Links { get; }
        public IDictionary<Key, T> Data { get; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("Matrix:\n");
            sb.Append(Links.Select(kv => $"{kv.Key} => {kv.Value.JoinCS()}").NiceList().Shift(2));
            sb.Append("\nTransposed:\n");
            sb.Append(Transpose().Links.Select(kv => $"{kv.Key} => {kv.Value.JoinCS()}").NiceList().Shift(2));
            sb.Append("\nValues:\n");
            sb.Append(Data.Select(kv => $"{kv.Key} := {kv.Value}").NiceList().Shift(2));
            sb.Append("\n");

            return sb.ToString();
        }

        public DepMatrix<T> Transpose()
        {
            IDictionary<Key, ISet<Key>> transposed = new Dictionary<Key, ISet<Key>>();

            foreach (var kvp in Links)
            {
                var rowKey = kvp.Key;
                var rowValues = kvp.Value;
                if (!transposed.ContainsKey(rowKey)) transposed[rowKey] = new HashSet<Key>();

                foreach (var columnKey in rowValues)
                {
                    if (!transposed.ContainsKey(columnKey)) transposed[columnKey] = new HashSet<Key>();

                    transposed[columnKey].Add(rowKey);
                }
            }


            return new DepMatrix<T>(transposed, Data);
        }


        public void Deconstruct(out IDictionary<Key, ISet<Key>> Links, out IDictionary<Key, T> Data)
        {
            Links = this.Links;
            Data = this.Data;
        }
    }
}