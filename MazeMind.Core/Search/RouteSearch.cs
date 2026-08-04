using System;
using System.Collections.Generic;

namespace MazeMind
{
    // Stores the result returned after completing a path search.
    public readonly struct RouteResult
    {
        // Stores the complete route from the start cell to the goal.
        public readonly IReadOnlyList<Cell> Route;

        // Stores the number of nodes explored during the search.
        public readonly int Explored;

        // Indicates whether a valid route was found.
        public readonly bool Found;

        // Returns the total movement cost of the route.
        // If no route exists, the maximum integer value is returned.
        public int Cost => Found ? Route.Count - 1 : int.MaxValue;

        // Create a new route search result.
        public RouteResult(IReadOnlyList<Cell> route, int explored, bool found)
        { Route = route; Explored = explored; Found = found; }
    }

    // Performs pathfinding using either the
    // A* or Dijkstra search algorithm.
    public sealed class RouteSearch
    {
        // Reference to the maze used during pathfinding.
        private readonly MazeBoard board;

        // Create a new pathfinding object.
        public RouteSearch(MazeBoard board)
        {
            // Store the maze reference and ensure it is valid.
            this.board = board ?? throw new ArgumentNullException(nameof(board));
        }

        // Find the shortest route between two cells.
        public RouteResult Find(Cell start, Cell goal, SearchStyle style)
        {
            // Ensure both cells are valid floor locations.
            if (!board.IsFloor(start) || !board.IsFloor(goal))
                return new RouteResult(Array.Empty<Cell>(), 0, false);

            // Store nodes waiting to be explored.
            var open = new List<Cell> { start };

            // Store the current best cost to each cell.
            var cost = new Dictionary<Cell, int> { [start] = 0 };

            // Store the previous cell used to reach each location.
            var previous = new Dictionary<Cell, Cell>();

            // Store cells that have already been explored.
            var closed = new HashSet<Cell>();

            // Count how many nodes have been explored.
            int explored = 0;

            // Continue searching while unexplored nodes remain.
            while (open.Count > 0)
            {
                // A list frontier is adequate for this maze size; a priority queue would reduce selection cost for larger boards.

                // Assume the first node has the best priority.
                int bestIndex = 0;

                // Search for the node with the lowest evaluation score.
                for (int i = 1; i < open.Count; i++)
                    if (Compare(open[i], open[bestIndex], cost, goal, style) < 0) bestIndex = i;

                // Remove the selected node from the open list.
                Cell current = open[bestIndex];
                open.RemoveAt(bestIndex);

                // Skip nodes that have already been processed.
                if (!closed.Add(current)) continue;

                // Record another explored node.
                explored++;

                // Stop searching once the goal has been reached.
                if (current == goal)
                    return new RouteResult(Build(previous, start, goal), explored, true);

                // Explore every neighbouring cell.
                foreach (Cell next in board.Neighbors(current))
                {
                    // Calculate the movement cost to the neighbour.
                    int tentative = cost[current] + 1;

                    // Ignore routes that are not improvements.
                    if (cost.TryGetValue(next, out int known) && tentative >= known) continue;

                    // Store the improved path cost.
                    cost[next] = tentative;

                    // Record how the neighbour was reached.
                    previous[next] = current;

                    // Add the neighbour if it has not already
                    // been scheduled for exploration.
                    if (!closed.Contains(next) && !open.Contains(next)) open.Add(next);
                }
            }

            // No valid route exists between the two cells.
            return new RouteResult(Array.Empty<Cell>(), explored, false);
        }

        // Compare two candidate cells and determine
        // which should be explored first.
        private int Compare(Cell a, Cell b, Dictionary<Cell, int> cost, Cell goal, SearchStyle style)
        {
            // Calculate the evaluation score for the first cell.
            int fa = cost[a] + (style == SearchStyle.AStar ? board.OpenDistance(a, goal) : 0);

            // Calculate the evaluation score for the second cell.
            int fb = cost[b] + (style == SearchStyle.AStar ? board.OpenDistance(b, goal) : 0);

            // Compare the evaluation scores.
            int result = fa.CompareTo(fb);

            // Return immediately if one score is smaller.
            if (result != 0) return result;

            // If equal, compare the path costs.
            result = cost[a].CompareTo(cost[b]);

            // If still equal, compare cell coordinates
            // to produce a consistent ordering.
            return result != 0 ? result : a.CompareTo(b);
        }

        // Reconstruct the complete route by following
        // the recorded parent cells.
        private static IReadOnlyList<Cell> Build(Dictionary<Cell, Cell> previous, Cell start, Cell goal)
        {
            // Begin with the destination cell.
            var route = new List<Cell> { goal };

            // Track the current position while rebuilding the path.
            Cell current = goal;

            // Continue until the starting cell is reached.
            while (current != start)
            {
                current = previous[current];
                route.Add(current);
            }

            // Reverse the route so it starts
            // at the starting position.
            route.Reverse();

            return route;
        }
    }
}
