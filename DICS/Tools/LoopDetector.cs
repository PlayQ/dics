using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DICS
{
    public static class LoopDetector
    {
        public static List<List<T>> FindAllCycles<T>(
            IDictionary<T, ISet<T>> graph)
        {
            var allCycles = new List<List<T>>();
            var visitedCycleSignatures = new HashSet<string>();

            foreach (var startNode in graph.Keys)
            {
                var path = new Stack<T>();
                var visitedInPath = new HashSet<T>();
                DFS(graph, startNode, startNode, path, visitedInPath, allCycles, visitedCycleSignatures);
            }

            return allCycles;
        }

        private static void DFS<T>(
            IDictionary<T, ISet<T>> graph,
            T startNode,
            T currentNode,
            Stack<T> path,
            HashSet<T> visitedInPath,
            List<List<T>> allCycles,
            HashSet<string> visitedCycleSignatures)
        {
            path.Push(currentNode);
            visitedInPath.Add(currentNode);

            if (graph.TryGetValue(currentNode, out var neighbors))
                foreach (var neighbor in neighbors)
                {
                    Debug.Assert(neighbor != null);
                    if (neighbor.Equals(startNode))
                    {
                        // We have found a cycle that leads back to the startNode
                        var cycle = path.Reverse().ToList(); // Because path is a stack (LIFO), reverse it
                        var signature = GetCycleSignature(cycle);

                        // Only add the cycle if we haven't encountered an identical ordering
                        if (!visitedCycleSignatures.Contains(signature))
                        {
                            visitedCycleSignatures.Add(signature);
                            allCycles.Add(cycle);
                        }
                    }
                    else if (!visitedInPath.Contains(neighbor))
                    {
                        // Continue DFS from neighbor if it's not already in the current path
                        DFS(graph, startNode, neighbor, path, visitedInPath, allCycles, visitedCycleSignatures);
                    }
                }

            // Backtrack
            path.Pop();
            visitedInPath.Remove(currentNode);
        }

        public static string GetCycleSignature<T>(List<T> cycle)
        {
            return string.Join("->", cycle);
        }
    }
}