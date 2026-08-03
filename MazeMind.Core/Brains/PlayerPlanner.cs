using System;
using System.Collections.Generic;

namespace MazeMind
{
    /// PlayerPlanner is responsible for making intelligent decisions
    /// for the AI-controlled player.
    ///
    /// During every game update, the planner analyses the current game
    /// state and determines whether the player should:
    ///
    /// 1. Collect the nearest pellet to increase the score.
    /// 2. Evade nearby pursuers to avoid being captured.
    ///
    /// The planner uses the selected search algorithm
    /// (BFS, DFS, UCS, Greedy or A*) through the RouteSearch class
    /// to calculate movement paths.
    /// </summary>
    public sealed class PlayerPlanner
    {
        // Pathfinding engine used to calculate routes in the maze.
        private readonly RouteSearch routes;

        // Stores the currently selected search algorithm.
        public SearchStyle SearchStyle { get; set; }

        // Indicates whether the AI is currently collecting pellets
        // or escaping from pursuers.
        public PlayerIntent Intent { get; private set; }

        // Records how many nodes were explored during
        // the most recent search.
        // Useful for algorithm performance comparison.
        public int LastExplored { get; private set; }
        /// Creates a new player planner.
        ///
        /// A RouteSearch object is created so that pathfinding
        /// calculations can be performed throughout the game.
        public PlayerPlanner(MazeBoard board, SearchStyle style = SearchStyle.AStar)
        {
            routes = new RouteSearch(board);

            // Store the chosen search algorithm.
            SearchStyle = style;
        }
        /// Determines the player's next movement.
        ///
        /// The planner first checks the distance to every dangerous pursuer.
        ///
        /// If a pursuer is within four cells,
        /// the player enters Evade mode.
        ///
        /// Otherwise the player continues collecting pellets.
        public Cell Choose(MatchModel model)
        {
            // Ensure a valid game model has been provided.
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            // Store the player's current location.
            Cell current = model.PlayerPosition;

            // Keeps track of the closest dangerous pursuer.
            int nearestThreat = int.MaxValue;

            // Examine every pursuer currently in the maze.
            foreach (var hunter in model.Pursuers)
            {
                // Ignore pursuers that cannot currently harm the player.
                if (hunter.Mode == PursuerMode.Vulnerable ||
                    hunter.Mode == PursuerMode.Returning)
                    continue;

                // Calculate the shortest route from the player
                // to the current pursuer.
                var route = routes.Find(current, hunter.Position, SearchStyle);

                // Keep the smallest distance found.
                nearestThreat = Math.Min(nearestThreat, route.Cost);
            }

            // Decide which behaviour should be used.
            //
            // If danger is close, survival becomes the priority.
            // Otherwise continue collecting pellets.
            Intent = nearestThreat <= 4
                ? PlayerIntent.Evade
                : PlayerIntent.Collect;

            // Execute the selected behaviour.
            return Intent == PlayerIntent.Evade
                ? Evade(model)
                : Collect(model);
        }
        /// Searches for the nearest pellet or power pellet.
        ///
        /// Every remaining pellet is evaluated using the selected
        /// search algorithm.
        ///
        /// The pellet requiring the lowest travel cost is selected.
        private Cell Collect(MatchModel model)
        {
            // Current best target.
            Cell bestTarget = model.PlayerPosition;

            // Lowest movement cost discovered.
            int bestCost = int.MaxValue;

            // Reset explored node counter.
            LastExplored = 0;

            // Evaluate every remaining collectible.
            foreach (Cell pellet in AllPellets(model))
            {
                // Calculate the shortest path to this pellet.
                var result = routes.Find(
                    model.PlayerPosition,
                    pellet,
                    SearchStyle);

                // Record search effort.
                LastExplored += result.Explored;

                // Replace the current best target if
                // this pellet is closer.
                //
                // CompareTo() provides consistent behaviour
                // when multiple pellets have equal costs.
                if (result.Cost < bestCost ||
                    (result.Cost == bestCost &&
                     pellet.CompareTo(bestTarget) < 0))
                {
                    bestCost = result.Cost;
                    bestTarget = pellet;
                }
            }

            // No pellets remain.
            if (bestCost == int.MaxValue)
                return model.PlayerPosition;

            // Calculate the route to the chosen pellet.
            var route = routes.Find(
                model.PlayerPosition,
                bestTarget,
                SearchStyle);

            LastExplored += route.Explored;

            // Return only the next movement.
            // A new decision will be made during the next game update.
            return route.Route.Count > 1
                ? route.Route[1]
                : model.PlayerPosition;
        }
        /// Selects the safest movement when a dangerous pursuer
        /// is nearby.
        ///
        /// Every neighbouring position is evaluated.
        ///
        /// The safest position is the one with the greatest
        /// distance from the nearest dangerous pursuer.
        private Cell Evade(MatchModel model)
        {
            // Generate all possible moves.
            // Staying in the current position is also considered.
            var choices = new List<Cell>(
                model.Board.Neighbors(model.PlayerPosition))
            {
                model.PlayerPosition
            };

            // Best movement found so far.
            Cell best = choices[0];

            // Highest safety value discovered.
            int bestSafety = int.MinValue;

            // Reset search statistics.
            LastExplored = 0;

            // Evaluate every possible movement.
            foreach (Cell choice in choices)
            {
                // Safety is determined by the distance
                // to the nearest dangerous pursuer.
                int safety = int.MaxValue;

                foreach (var hunter in model.Pursuers)
                {
                    // Ignore harmless pursuers.
                    if (hunter.Mode == PursuerMode.Vulnerable ||
                        hunter.Mode == PursuerMode.Returning)
                        continue;

                    // Calculate distance from this candidate position
                    // to the pursuer.
                    var result = routes.Find(
                        choice,
                        hunter.Position,
                        SearchStyle);

                    // Record algorithm performance.
                    LastExplored += result.Explored;

                    // Keep the nearest pursuer.
                    safety = Math.Min(safety, result.Cost);
                }

                // Choose the safest available movement.
                // If two moves are equally safe,
                // CompareTo() ensures deterministic behaviour.
                if (safety > bestSafety ||
                    (safety == bestSafety &&
                     choice.CompareTo(best) < 0))
                {
                    bestSafety = safety;
                    best = choice;
                }
            }

            // Return the safest movement.
            return best;
        }
        /// Returns every remaining collectible in the maze.
        ///
        /// This includes both normal pellets and power pellets.
        private static IEnumerable<Cell> AllPellets(MatchModel model)
        {
            // Return every normal pellet.
            foreach (Cell cell in model.Pellets)
                yield return cell;

            // Return every power pellet.
            foreach (Cell cell in model.PowerPellets)
                yield return cell;
        }
    }
}
