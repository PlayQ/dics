using System;
using System.Collections.Generic;
using System.Linq;

namespace DICS
{
    /// <summary>
    ///     Materializes the instantiation order of a dependency graph as data.
    ///     Takes the same graph shape as <see cref="LoopDetector" />: a node maps to the set
    ///     of nodes it depends on. Requires an acyclic graph — run
    ///     <see cref="LoopDetector.FindAllCycles{T}" /> first.
    /// </summary>
    public static class TopologicalOrder
    {
        /// <summary>
        ///     Dependencies before dependents. Nodes are grouped by longest distance to a
        ///     dependency-free node, which is the wave a producer can build them in, and ties
        ///     inside a wave are broken by <paramref name="rank" />; equal ranks keep the
        ///     graph's enumeration order. Reversing the result yields a safe teardown order:
        ///     every dependent is released before anything it was constructed from.
        /// </summary>
        public static List<T> Of<T>(IDictionary<T, ISet<T>> graph, Func<T, int> rank) where T : notnull
        {
            var depths = new Dictionary<T, int>();

            return graph.Keys
                .OrderBy(node => DepthOf(graph, depths, node))
                .ThenBy(rank)
                .ToList();
        }

        /// <summary>
        ///     Longest distance from <paramref name="node" /> down to a dependency-free node.
        ///     Memoized in <paramref name="depths" />; terminates only on an acyclic graph.
        /// </summary>
        private static int DepthOf<T>(IDictionary<T, ISet<T>> graph, IDictionary<T, int> depths, T node)
            where T : notnull
        {
            if (depths.TryGetValue(node, out var known)) return known;

            var depth = 0;
            if (graph.TryGetValue(node, out var deps))
                foreach (var dep in deps)
                    depth = Math.Max(depth, DepthOf(graph, depths, dep) + 1);

            depths[node] = depth;
            return depth;
        }
    }
}
