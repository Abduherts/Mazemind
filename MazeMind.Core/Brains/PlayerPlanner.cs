using System;
using System.Collections.Generic;

namespace MazeMind
{
    public sealed class PlayerPlanner
    {
        private readonly RouteSearch routes;
        public SearchStyle SearchStyle { get; set; }
        public PlayerIntent Intent { get; private set; }
        public int LastExplored { get; private set; }

        public PlayerPlanner(MazeBoard board, SearchStyle style = SearchStyle.AStar)
        { routes = new RouteSearch(board); SearchStyle = style; }

        public Cell Choose(MatchModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            Cell current = model.PlayerPosition;
            int nearestThreat = int.MaxValue;
            foreach (var hunter in model.Pursuers)
            {
                if (hunter.Mode == PursuerMode.Vulnerable || hunter.Mode == PursuerMode.Returning) continue;
                var route = routes.Find(current, hunter.Position, SearchStyle);
                nearestThreat = Math.Min(nearestThreat, route.Cost);
            }
            Intent = nearestThreat <= 4 ? PlayerIntent.Evade : PlayerIntent.Collect;
            return Intent == PlayerIntent.Evade ? Evade(model) : Collect(model);
        }

        private Cell Collect(MatchModel model)
        {
            Cell bestTarget = model.PlayerPosition; int bestCost = int.MaxValue; LastExplored = 0;
            foreach (Cell pellet in AllPellets(model))
            {
                var result = routes.Find(model.PlayerPosition, pellet, SearchStyle);
                LastExplored += result.Explored;
                if (result.Cost < bestCost || (result.Cost == bestCost && pellet.CompareTo(bestTarget) < 0))
                { bestCost = result.Cost; bestTarget = pellet; }
            }
            if (bestCost == int.MaxValue) return model.PlayerPosition;
            var route = routes.Find(model.PlayerPosition, bestTarget, SearchStyle);
            LastExplored += route.Explored;
            return route.Route.Count > 1 ? route.Route[1] : model.PlayerPosition;
        }

        private Cell Evade(MatchModel model)
        {
            var choices = new List<Cell>(model.Board.Neighbors(model.PlayerPosition)) { model.PlayerPosition };
            Cell best = choices[0]; int bestSafety = int.MinValue; LastExplored = 0;
            foreach (Cell choice in choices)
            {
                int safety = int.MaxValue;
                foreach (var hunter in model.Pursuers)
                {
                    if (hunter.Mode == PursuerMode.Vulnerable || hunter.Mode == PursuerMode.Returning) continue;
                    var result = routes.Find(choice, hunter.Position, SearchStyle);
                    LastExplored += result.Explored; safety = Math.Min(safety, result.Cost);
                }
                if (safety > bestSafety || (safety == bestSafety && choice.CompareTo(best) < 0))
                { bestSafety = safety; best = choice; }
            }
            return best;
        }

        private static IEnumerable<Cell> AllPellets(MatchModel model)
        {
            foreach (Cell cell in model.Pellets) yield return cell;
            foreach (Cell cell in model.PowerPellets) yield return cell;
        }
    }
}