using System;
using System.Collections.Generic;

namespace MazeMind
{
    // Controls the behaviour of every pursuer in the maze.
    // Each pursuer makes decisions based on its current mode
    // (Hunt, Roam, Vulnerable, Returning or Home) and role.
    // Different search algorithms can be used to calculate
    // movement paths towards the selected target.
    public sealed class PursuerPlanner
    {
        // Reference to the game board used for movement validation.
        private readonly MazeBoard board;

        // Performs pathfinding between two cells.
        private readonly RouteSearch routes;

        // Used by intelligent pursuers to predict future player movement.
        private readonly AlphaBetaLookAhead lookAhead;

        // Generates random decisions for Rover behaviour.
        private readonly Random random;

        // Stores the player's previous position so movement direction
        // can be estimated during the next update.
        private Cell previousPlayer;

        // Indicates whether a previous player position exists.
        private bool hasPrevious;

        // Search algorithm currently selected.
        public SearchStyle SearchStyle { get; set; }

        // Total number of explored nodes during the latest decision.
        // This is useful for analysing search algorithm performance.
        public int LastExplored { get; private set; }

        // Initialise the planner and supporting search components.
        public PursuerPlanner(MazeBoard board, int seed = 1337, SearchStyle style = SearchStyle.AStar)
        {
            // Ensure a valid game board has been supplied.
            this.board = board ?? throw new ArgumentNullException(nameof(board));

            // Create the pathfinding engine.
            routes = new RouteSearch(board);

            // Create the Alpha-Beta prediction engine.
            lookAhead = new AlphaBetaLookAhead(board);

            // Create a random generator with a fixed seed so
            // behaviour remains consistent during testing.
            random = new Random(seed);

            // Store the selected search algorithm.
            SearchStyle = style;
        }

        // Determines the next movement for every pursuer.
        public IReadOnlyList<Cell> ChooseAll(MatchModel model)
        {
            // Prevent null reference errors.
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            // Store the movement decision for each pursuer.
            var decisions = new Cell[model.Pursuers.Count];

            // Reset search statistics.
            LastExplored = 0;

            // Calculate the next move for every pursuer.
            for (int i = 0; i < decisions.Length; i++)
                decisions[i] = Choose(model, model.Pursuers[i]);

            // Remember the player's position so the movement
            // direction can be estimated next turn.
            previousPlayer = model.PlayerPosition;
            hasPrevious = true;

            return decisions;
        }

        // Determines the next move for a single pursuer.
        public Cell Choose(MatchModel model, PursuerState actor)
        {
            // Find every legal neighbouring position.
            var legal = board.Neighbors(actor.Position);

            // If no movement is possible or the pursuer is already
            // at home, remain in the current position.
            if (legal.Count == 0 || actor.Mode == PursuerMode.Home)
                return actor.Position;

            // Returning pursuers always travel back to home.
            if (actor.Mode == PursuerMode.Returning)
                return RouteStep(actor.Position, board.Home);

            // Vulnerable pursuers attempt to escape from the player.
            if (actor.Mode == PursuerMode.Vulnerable)
                return Flee(legal, model.PlayerPosition);

            // During roam mode the pursuer patrols instead of chasing.
            if (actor.Mode == PursuerMode.Roam)
                return Roam(actor, legal);

            // Determine the target location based on the pursuer's role.
            Cell target = HuntTarget(model, actor.Role);

            // Spear and Seer use Alpha-Beta look-ahead
            // to predict future player movement.
            if (actor.Role == PursuerRole.Spear ||
                actor.Role == PursuerRole.Seer)
            {
                Cell step = lookAhead.Choose(
                    actor.Position,
                    model.PlayerPosition,
                    target,
                    4);

                // Record search effort.
                LastExplored += lookAhead.LastExplored;

                return step;
            }

            // Rover behaves unpredictably by choosing
            // a random neighbouring position.
            if (actor.Role == PursuerRole.Rover)
                return legal[random.Next(legal.Count)];

            // Remaining pursuers simply follow
            // the shortest path to the target.
            return RouteStep(actor.Position, target);
        }

        // Calculates the chase target for each pursuer role.
        private Cell HuntTarget(MatchModel model, PursuerRole role)
        {
            // Spear and Rover directly chase the player.
            if (role == PursuerRole.Spear ||
                role == PursuerRole.Rover)
                return model.PlayerPosition;

            // Seer predicts where the player will be
            // several moves into the future.
            if (role == PursuerRole.Seer)
            {
                Cell target = model.PlayerPosition;

                // Estimate player movement direction by comparing
                // the current position with the previous position.
                Cell heading = hasPrevious
                    ? new Cell(
                        model.PlayerPosition.X - previousPlayer.X,
                        model.PlayerPosition.Y - previousPlayer.Y)
                    : new Cell(1, 0);

                // Look ahead three tiles unless a wall blocks the path.
                for (int i = 0; i < 3; i++)
                {
                    Cell next = target + heading;

                    if (!board.IsLegalStep(target, next))
                        break;

                    target = next;
                }

                return target;
            }

            // Guardian attempts to patrol between
            // the player and the home location.
            Cell guard = board.Home;
            int best = int.MaxValue;

            foreach (Cell candidate in board.Floor)
            {
                // Combine the distance to the player
                // and distance to home.
                int score =
                    candidate.Manhattan(model.PlayerPosition) +
                    candidate.Manhattan(board.Home);

                // Choose the lowest scoring location.
                if (score < best ||
                    (score == best &&
                     candidate.CompareTo(guard) < 0))
                {
                    best = score;
                    guard = candidate;
                }
            }

            return guard;
        }

        // Controls patrol behaviour while the pursuer
        // is not actively chasing the player.
        private Cell Roam(PursuerState actor, IReadOnlyList<Cell> legal)
        {
            // Rover patrols randomly.
            if (actor.Role == PursuerRole.Rover)
                return legal[random.Next(legal.Count)];

            // Each role patrols a different corner
            // of the maze.
            Cell corner;

            switch (actor.Role)
            {
                case PursuerRole.Spear:
                    corner = new Cell(board.Width - 1, board.Height - 1);
                    break;

                case PursuerRole.Seer:
                    corner = new Cell(0, board.Height - 1);
                    break;

                default:
                    corner = new Cell(board.Width - 1, 0);
                    break;
            }

            // Start with the first legal movement.
            Cell best = legal[0];
            int distance = best.Manhattan(corner);

            // Choose the move closest to the patrol corner.
            foreach (Cell cell in legal)
            {
                if (cell.Manhattan(corner) < distance)
                {
                    best = cell;
                    distance = cell.Manhattan(corner);
                }
            }

            return best;
        }

        // Used when the pursuer becomes vulnerable.
        // The goal is to maximise the distance from the player.
        private Cell Flee(IReadOnlyList<Cell> legal, Cell player)
        {
            Cell best = legal[0];

            int distance = board.OpenDistance(best, player);

            foreach (Cell cell in legal)
            {
                int candidate = board.OpenDistance(cell, player);

                // Select the movement that increases
                // the distance from the player.
                if (candidate > distance)
                {
                    best = cell;
                    distance = candidate;
                }
            }

            return best;
        }

        // Calculate the shortest path to the selected target
        // and return only the next movement step.
        private Cell RouteStep(Cell from, Cell target)
        {
            var result = routes.Find(from, target, SearchStyle);

            // Record the number of explored nodes.
            LastExplored += result.Explored;

            // Return the next movement along the path.
            return result.Route.Count > 1
                ? result.Route[1]
                : from;
        }
    }
}
