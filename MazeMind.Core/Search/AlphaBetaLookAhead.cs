using System;
using System.Collections.Generic;

namespace MazeMind
{
    // Implements the Alpha-Beta pruning algorithm to improve
    // pursuer decision making by searching several moves ahead.
    public sealed class AlphaBetaLookAhead
    {
        // Reference to the maze used for movement generation.
        private readonly MazeBoard board;

        // Route search object used to estimate path costs.
        private readonly RouteSearch routes;

        // Records how many search nodes were explored
        // during the most recent decision.
        public int LastExplored { get; private set; }

        // Create a new Alpha-Beta search object.
        public AlphaBetaLookAhead(MazeBoard board)
        {
            // Store the maze reference and ensure it is valid.
            this.board = board ?? throw new ArgumentNullException(nameof(board));

            // Initialise the pathfinding helper.
            routes = new RouteSearch(board);
        }

        // Select the best movement for the pursuer by
        // performing an Alpha-Beta search.
        public Cell Choose(Cell pursuer, Cell player, Cell target, int depth)
        {
            // Ensure both the pursuer and player are located
            // on valid floor tiles.
            if (!board.IsFloor(pursuer) || !board.IsFloor(player)) return pursuer;

            // Restrict the search depth to a safe range.
            depth = Math.Max(1, Math.Min(depth, 6));

            // Fixed depth bounds frame cost; iterative deepening would be the upgrade for larger boards.

            // Reset the explored node counter.
            LastExplored = 0;

            // Get every legal movement from the current position.
            var moves = board.Neighbors(pursuer);

            // Stay in the current position if no moves exist.
            if (moves.Count == 0) return pursuer;

            // Initialise the best movement and evaluation score.
            Cell best = moves[0];
            int bestValue = int.MinValue;

            // Evaluate every possible movement.
            foreach (Cell move in moves)
            {
                // Assume the player responds optimally.
                int value = Min(move, player, target, depth - 1, int.MinValue + 1, int.MaxValue);

                // Keep the move with the highest evaluation.
                if (value > bestValue || (value == bestValue && move.CompareTo(best) < 0))
                {
                    bestValue = value;
                    best = move;
                }
            }

            // Return the selected movement.
            return best;
        }

        // Maximising stage of the Alpha-Beta search.
        // Represents the pursuer attempting to improve its position.
        private int Max(Cell hunter, Cell player, Cell target, int depth, int alpha, int beta)
        {
            // Count this explored search node.
            LastExplored++;

            // Stop searching if the depth limit has been reached
            // or the player has already been caught.
            if (depth == 0 || hunter == player) return Evaluate(hunter, player, target);

            // Initialise the highest evaluation value.
            int value = int.MinValue;

            // Explore every legal pursuer movement.
            foreach (Cell move in board.Neighbors(hunter))
            {
                // Evaluate the player's best response.
                value = Math.Max(value, Min(move, player, target, depth - 1, alpha, beta));

                // Update the alpha bound.
                alpha = Math.Max(alpha, value);

                // Stop searching when further exploration
                // cannot improve the current result.
                if (alpha >= beta) break;
            }

            // Return the best evaluation.
            return value;
        }

        // Minimising stage of the Alpha-Beta search.
        // Represents the player attempting to avoid capture.
        private int Min(Cell hunter, Cell player, Cell target, int depth, int alpha, int beta)
        {
            // Count this explored search node.
            LastExplored++;

            // Stop searching if the depth limit has been reached
            // or the player has been captured.
            if (depth == 0 || hunter == player) return Evaluate(hunter, player, target);

            // Initialise the lowest evaluation value.
            int value = int.MaxValue;

            // Explore every legal player movement.
            foreach (Cell move in board.Neighbors(player))
            {
                // Evaluate the pursuer's next decision.
                value = Math.Min(value, Max(hunter, move, target, depth - 1, alpha, beta));

                // Update the beta bound.
                beta = Math.Min(beta, value);

                // Stop searching when further exploration
                // cannot improve the current result.
                if (alpha >= beta) break;
            }

            // Return the lowest evaluation.
            return value;
        }

        // Calculate a score representing how favourable
        // the current game state is for the pursuer.
        private int Evaluate(Cell hunter, Cell player, Cell target)
        {
            // Assign a very high score if the player
            // has already been captured.
            if (hunter == player) return 10000;

            // Calculate the shortest path to the player.
            int playerCost = routes.Find(hunter, player, SearchStyle.AStar).Cost;

            // Calculate the shortest path to the target position.
            int targetCost = routes.Find(hunter, target, SearchStyle.AStar).Cost;

            // Replace unreachable distances with
            // a large penalty value.
            if (playerCost == int.MaxValue) playerCost = 1000;
            if (targetCost == int.MaxValue) targetCost = 1000;

            // Higher scores favour positions that are
            // closer to the player and target.
            return -playerCost * 20 - targetCost;
        }
    }
}
