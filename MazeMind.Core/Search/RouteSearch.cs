using System;
using System.Collections.Generic;

namespace MazeMind
{
    public readonly struct RouteResult
    {
        public readonly IReadOnlyList<Cell> Route;
        public readonly int Explored;
        public readonly bool Found;
        public int Cost => Found ? Route.Count - 1 : int.MaxValue;
        public RouteResult(IReadOnlyList<Cell> route, int explored, bool found)
        { Route = route; Explored = explored; Found = found; }
    }

    public sealed class RouteSearch
    {
        private readonly MazeBoard board;
        public RouteSearch(MazeBoard board) { this.board = board ?? throw new ArgumentNullException(nameof(board)); }

        public RouteResult Find(Cell start, Cell goal, SearchStyle style)
        {
            if (!board.IsFloor(start) || !board.IsFloor(goal)) return new RouteResult(Array.Empty<Cell>(), 0, false);
            var open = new List<Cell> { start };
            var cost = new Dictionary<Cell, int> { [start] = 0 };
            var previous = new Dictionary<Cell, Cell>();
            var closed = new HashSet<Cell>(); int explored = 0;
            while (open.Count > 0)
            {
                // A list frontier is adequate for this maze size; a priority queue would reduce selection cost for larger boards.
                int bestIndex = 0;
                for (int i = 1; i < open.Count; i++)
                    if (Compare(open[i], open[bestIndex], cost, goal, style) < 0) bestIndex = i;
                Cell current = open[bestIndex]; open.RemoveAt(bestIndex);
                if (!closed.Add(current)) continue;
                explored++;
                if (current == goal) return new RouteResult(Build(previous, start, goal), explored, true);
                foreach (Cell next in board.Neighbors(current))
                {
                    int tentative = cost[current] + 1;
                    if (cost.TryGetValue(next, out int known) && tentative >= known) continue;
                    cost[next] = tentative; previous[next] = current;
                    if (!closed.Contains(next) && !open.Contains(next)) open.Add(next);
                }
            }
            return new RouteResult(Array.Empty<Cell>(), explored, false);
        }

        private int Compare(Cell a, Cell b, Dictionary<Cell, int> cost, Cell goal, SearchStyle style)
        {
            int fa = cost[a] + (style == SearchStyle.AStar ? board.OpenDistance(a, goal) : 0);
            int fb = cost[b] + (style == SearchStyle.AStar ? board.OpenDistance(b, goal) : 0);
            int result = fa.CompareTo(fb);
            if (result != 0) return result;
            result = cost[a].CompareTo(cost[b]);
            return result != 0 ? result : a.CompareTo(b);
        }

        private static IReadOnlyList<Cell> Build(Dictionary<Cell, Cell> previous, Cell start, Cell goal)
        {
            var route = new List<Cell> { goal }; Cell current = goal;
            while (current != start) { current = previous[current]; route.Add(current); }
            route.Reverse(); return route;
        }
    }
}