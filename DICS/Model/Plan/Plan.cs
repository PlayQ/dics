using System;
using System.Collections.Generic;
using System.Text;

namespace DICS
{
    /// <summary>
    ///     <paramref name="InstantiationOrder" /> is the sequence the producer instantiates
    ///     <paramref name="Matrix" />'s keys in: dependencies before dependents. It is the one
    ///     materialized topological order of the plan — set elements are listed in it, and
    ///     reversing it gives the teardown order.
    /// </summary>
    public record Plan(
        DepMatrix<Instruction> Matrix,
        IReadOnlyList<Key> InstantiationOrder,
        ISet<Key> Roots,
        ISet<Key> Private,
        Injector plannedBy)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("--- Plan ---\n");
            sb.Append($"Roots:\n{Roots.NiceList()}\n");
            sb.Append(Matrix);
            sb.Append($"Instantiation order:\n{InstantiationOrder.NiceList()}\n");
            sb.Append("------------");
            return sb.ToString();
        }
    }
}