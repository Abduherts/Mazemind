using System;
using System.Collections.Generic;

namespace MazeMind
{
    public sealed class PursuerPlanner
    {
        private readonly MazeBoard board;
        private readonly RouteSearch routes;
        private readonly AlphaBetaLookAhead lookAhead;
        private readonly Random random;
        private Cell previousPlayer;
        private bool hasPrevious;

        public SearchStyle SearchStyle { get; set; }
        public int LastExplored { get; private set; }

        public PursuerPlanner(MazeBoard board, int seed = 1337, SearchStyle style = SearchStyle.AStar)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            routes = new RouteSearch(board); lookAhead = new AlphaBetaLookAhead(board);
            random = new Random(seed); SearchStyle = style;
        }

        public IReadOnlyList<Cell> ChooseAll(MatchModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var decisions = new Cell[model.Pursuers.Count]; LastExplored = 0;
            for (int i = 0; i < decisions.Length; i++) decisions[i] = Choose(model, model.Pursuers[i]);
            previousPlayer = model.PlayerPosition; hasPrevious = true;
            return decisions;
        }

        public Cell Choose(MatchModel model, PursuerState actor)
        {
            var legal = board.Neighbors(actor.Position);
            if (legal.Count == 0 || actor.Mode == PursuerMode.Home) return actor.Position;
            if (actor.Mode == PursuerMode.Returning) return RouteStep(actor.Position, board.Home);
            if (actor.Mode == PursuerMode.Vulnerable) return Flee(legal, model.PlayerPosition);
            if (actor.Mode == PursuerMode.Roam) return Roam(actor, legal);

            Cell target = HuntTarget(model, actor.Role);
            if (actor.Role == PursuerRole.Spear || actor.Role == PursuerRole.Seer)
            {
                Cell step = lookAhead.Choose(actor.Position, model.PlayerPosition, target, 4);
                LastExplored += lookAhead.LastExplored; return step;
            }
            if (actor.Role == PursuerRole.Rover) return legal[random.Next(legal.Count)];
            return RouteStep(actor.Position, target);
        }

        private Cell HuntTarget(MatchModel model, PursuerRole role)
        {
            if (role == PursuerRole.Spear || role == PursuerRole.Rover) return model.PlayerPosition;
            if (role == PursuerRole.Seer)
            {
                Cell target = model.PlayerPosition;
                Cell heading = hasPrevious
                    ? new Cell(model.PlayerPosition.X - previousPlayer.X, model.PlayerPosition.Y - previousPlayer.Y)
                    : new Cell(1, 0);
                for (int i = 0; i < 3; i++)
                {
                    Cell next = target + heading;
                    if (!board.IsLegalStep(target, next)) break;
                    target = next;
                }
                return target;
            }
            Cell guard = board.Home; int best = int.MaxValue;
            foreach (Cell candidate in board.Floor)
            {
                int score = candidate.Manhattan(model.PlayerPosition) + candidate.Manhattan(board.Home);
                if (score < best || (score == best && candidate.CompareTo(guard) < 0))
                { best = score; guard = candidate; }
            }
            return guard;
        }

        private Cell Roam(PursuerState actor, IReadOnlyList<Cell> legal)
        {
            if (actor.Role == PursuerRole.Rover) return legal[random.Next(legal.Count)];
            Cell corner;
            switch (actor.Role)
            {
                case PursuerRole.Spear: corner = new Cell(board.Width - 1, board.Height - 1); break;
                case PursuerRole.Seer: corner = new Cell(0, board.Height - 1); break;
                default: corner = new Cell(board.Width - 1, 0); break;
            }
            Cell best = legal[0]; int distance = best.Manhattan(corner);
            foreach (Cell cell in legal)
                if (cell.Manhattan(corner) < distance) { best = cell; distance = cell.Manhattan(corner); }
            return best;
        }

        private Cell Flee(IReadOnlyList<Cell> legal, Cell player)
        {
            Cell best = legal[0]; int distance = board.OpenDistance(best, player);
            foreach (Cell cell in legal)
            {
                int candidate = board.OpenDistance(cell, player);
                if (candidate > distance) { best = cell; distance = candidate; }
            }
            return best;
        }

        private Cell RouteStep(Cell from, Cell target)
        {
            var result = routes.Find(from, target, SearchStyle); LastExplored += result.Explored;
            return result.Route.Count > 1 ? result.Route[1] : from;
        }
    }
}