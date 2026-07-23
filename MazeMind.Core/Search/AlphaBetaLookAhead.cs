using System;
using System.Collections.Generic;

namespace MazeMind
{
    public sealed class AlphaBetaLookAhead
    {
        private readonly MazeBoard board;
        private readonly RouteSearch routes;
        public int LastExplored { get; private set; }

        public AlphaBetaLookAhead(MazeBoard board)
        { this.board = board ?? throw new ArgumentNullException(nameof(board)); routes = new RouteSearch(board); }

        public Cell Choose(Cell pursuer, Cell player, Cell target, int depth)
        {
            if (!board.IsFloor(pursuer) || !board.IsFloor(player)) return pursuer;
            depth = Math.Max(1, Math.Min(depth, 6));
            // Fixed depth bounds frame cost; iterative deepening would be the upgrade for larger boards.
            LastExplored = 0;
            var moves = board.Neighbors(pursuer); if (moves.Count == 0) return pursuer;
            Cell best = moves[0]; int bestValue = int.MinValue;
            foreach (Cell move in moves)
            {
                int value = Min(move, player, target, depth - 1, int.MinValue + 1, int.MaxValue);
                if (value > bestValue || (value == bestValue && move.CompareTo(best) < 0))
                { bestValue = value; best = move; }
            }
            return best;
        }

        private int Max(Cell hunter, Cell player, Cell target, int depth, int alpha, int beta)
        {
            LastExplored++;
            if (depth == 0 || hunter == player) return Evaluate(hunter, player, target);
            int value = int.MinValue;
            foreach (Cell move in board.Neighbors(hunter))
            {
                value = Math.Max(value, Min(move, player, target, depth - 1, alpha, beta));
                alpha = Math.Max(alpha, value); if (alpha >= beta) break;
            }
            return value;
        }

        private int Min(Cell hunter, Cell player, Cell target, int depth, int alpha, int beta)
        {
            LastExplored++;
            if (depth == 0 || hunter == player) return Evaluate(hunter, player, target);
            int value = int.MaxValue;
            foreach (Cell move in board.Neighbors(player))
            {
                value = Math.Min(value, Max(hunter, move, target, depth - 1, alpha, beta));
                beta = Math.Min(beta, value); if (alpha >= beta) break;
            }
            return value;
        }

        private int Evaluate(Cell hunter, Cell player, Cell target)
        {
            if (hunter == player) return 10000;
            int playerCost = routes.Find(hunter, player, SearchStyle.AStar).Cost;
            int targetCost = routes.Find(hunter, target, SearchStyle.AStar).Cost;
            if (playerCost == int.MaxValue) playerCost = 1000;
            if (targetCost == int.MaxValue) targetCost = 1000;
            return -playerCost * 20 - targetCost;
        }
    }
}