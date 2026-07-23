using System;
using System.Collections.Generic;
using System.Linq;
using MazeMind;

internal static class Program
{
    private static int checks;
    private static readonly string[] Map =
    {
        "###########",
        "#P..#....1#",
        "#.#.#.##..#",
        "T.o.H....2T",
        "#.#.#.##..#",
        "#3.......4#",
        "###########"
    };

    private static int Main(string[] args)
    {
        if (args.Length != 1 || args[0] != "--check")
        {
            Console.Error.WriteLine("Usage: dotnet run --project Verification -- --check");
            return 2;
        }
        try
        {
            BoardAndPortal(); SearchAgreement(); PlayerEvasion(); ScoringAndPower();
            CollisionAndEnd(); RoundReset(); PursuerChoices(); AlphaBetaMove(); SeededRepeatability();
            Console.WriteLine($"MazeMind verification passed: {checks} checks."); return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Verification failed: " + error.Message); return 1;
        }
    }

    private static MazeBoard Board() => MazeBoard.Parse(Map);
    private static void Require(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException(message); }

    private static void BoardAndPortal()
    {
        var board = Board(); var ends = board.Portals.Keys.OrderBy(c => c.X).ToArray();
        Require(ends.Length == 2, "portal pair missing");
        Require(board.IsLegalStep(ends[0], ends[1]) && board.IsLegalStep(ends[1], ends[0]), "portal is not bidirectional");
        Require(!board.IsLegalStep(board.PlayerStart, new Cell(-9, -9)), "illegal step accepted");
    }

    private static void SearchAgreement()
    {
        var board = Board(); var search = new RouteSearch(board);
        Cell goal = board.PursuerStarts[1];
        var a = search.Find(board.PlayerStart, goal, SearchStyle.AStar);
        var d = search.Find(board.PlayerStart, goal, SearchStyle.Dijkstra);
        Require(a.Found && d.Found, "route search failed");
        Require(a.Cost == d.Cost, "A* and Dijkstra costs differ");
    }

    private static void PlayerEvasion()
    {
        var model = new MatchModel(Board()); var threat = model.Board.Neighbors(model.PlayerPosition)[0];
        model.Pursuers[0].Position = threat; model.Pursuers[0].Mode = PursuerMode.Hunt;
        for (int i = 1; i < 4; i++) model.Pursuers[i].Mode = PursuerMode.Returning;
        var planner = new PlayerPlanner(model.Board);
        Cell choice = planner.Choose(model);
        Require(planner.Intent == PlayerIntent.Evade, "near threat did not trigger Evade");
        Require(choice != threat && model.Board.IsLegalStep(model.PlayerPosition, choice), "evade decision is unsafe or illegal");
    }

    private static void ScoringAndPower()
    {
        var model = new MatchModel(Board()); var routes = new RouteSearch(model.Board);
        Cell pellet = model.Pellets.OrderBy(c => routes.Find(model.PlayerPosition, c, SearchStyle.AStar).Cost).First();
        Walk(model, routes.Find(model.PlayerPosition, pellet, SearchStyle.AStar).Route);
        Require(model.Score >= 10, "pellet did not score 10");

        Cell power = model.PowerPellets.First();
        Walk(model, routes.Find(model.PlayerPosition, power, SearchStyle.AStar).Route);
        Require(model.Score >= 60, "power pellet did not score 50");
        Require(model.PowerRemaining > 0 && model.Pursuers.All(p => p.Mode == PursuerMode.Vulnerable),
            "power pellet did not activate one vulnerable mode");

        Cell contact = model.Board.Neighbors(model.PlayerPosition)[0]; int before = model.Score;
        model.Pursuers[0].Position = contact;
        var holds = model.Pursuers.Select(p => p.Position).ToArray();
        var eaten = model.ApplyStep(contact, holds);
        Require(eaten.PursuersEaten == 1 && model.Score == before + 200 &&
            model.Pursuers[0].Mode == PursuerMode.Returning, "vulnerable pursuer did not return for 200 points");
    }

    private static void Walk(MatchModel model, IReadOnlyList<Cell> route)
    {
        for (int step = 1; step < route.Count; step++)
        {
            var holds = new Cell[4];
            for (int i = 0; i < 4; i++)
            {
                model.Pursuers[i].Position = model.Board.PursuerStarts[i];
                model.Pursuers[i].Mode = PursuerMode.Roam; holds[i] = model.Board.PursuerStarts[i];
            }
            model.ApplyStep(route[step], holds);
        }
    }

    private static void CollisionAndEnd()
    {
        var model = new MatchModel(Board());
        for (int loss = 0; loss < 3; loss++)
        {
            Cell next = model.Board.Neighbors(model.PlayerPosition)[0];
            model.Pursuers[0].Position = next; model.Pursuers[0].Mode = PursuerMode.Hunt;
            for (int i = 1; i < 4; i++) { model.Pursuers[i].Position = model.Board.Home; model.Pursuers[i].Mode = PursuerMode.Returning; }
            var result = model.ApplyStep(next, new[] { next, model.Board.Home, model.Board.Home, model.Board.Home });
            Require(result.LifeLost, "dangerous collision did not cost a life");
        }
        Require(model.Lives == 0 && model.IsOver, "third collision did not end match");
    }

    private static void RoundReset()
    {
        var model = new MatchModel(Board()); model.Pellets.Clear(); model.PowerPellets.Clear();
        model.Pellets.Add(model.PlayerPosition);
        var result = model.ApplyStep(model.PlayerPosition, model.Pursuers.Select(p => p.Position).ToArray());
        Require(result.RoundCleared && model.Round == 2, "empty board did not advance round");
        Require(model.Pellets.Count == model.Board.InitialPellets.Count, "round did not restore pellets");
    }

    private static void PursuerChoices()
    {
        var model = new MatchModel(Board()); var planner = new PursuerPlanner(model.Board, 7);
        foreach (var actor in model.Pursuers) actor.Mode = PursuerMode.Hunt;
        var choices = planner.ChooseAll(model);
        Require(choices.Count == 4, "planner did not return four choices");
        for (int i = 0; i < 4; i++)
            Require(model.Board.IsLegalStep(model.Pursuers[i].Position, choices[i]), $"illegal role choice {i}");
    }

    private static void AlphaBetaMove()
    {
        var board = Board(); var search = new AlphaBetaLookAhead(board);
        Cell start = board.PursuerStarts[0]; Cell move = search.Choose(start, board.PlayerStart, board.PlayerStart, 4);
        Require(board.IsLegalStep(start, move), "alpha-beta produced illegal move");
        Require(search.LastExplored > 0, "alpha-beta explored no states");
    }

    private static void SeededRepeatability()
    {
        var firstModel = new MatchModel(Board()); var secondModel = new MatchModel(Board());
        var first = new PursuerPlanner(firstModel.Board, 99); var second = new PursuerPlanner(secondModel.Board, 99);
        var a = new List<Cell>(); var b = new List<Cell>();
        for (int turn = 0; turn < 8; turn++)
        {
            a.Add(first.ChooseAll(firstModel)[3]); b.Add(second.ChooseAll(secondModel)[3]);
        }
        Require(a.SequenceEqual(b), "same seed did not repeat wandering choices");
    }
}