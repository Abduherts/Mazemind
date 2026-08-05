using System;
using System.Collections.Generic;
using System.Linq;
using MazeMind;

internal static class Program
{
    // Stores the total number of successful verification checks performed.
    // Each time a condition is verified, this counter is increased.
    private static int checks;

    // Test maze used for all verification routines.
    // Symbols:
    // # = Wall
    // P = Player start
    // . = Pellet
    // o = Power pellet
    // H = Pursuer home
    // 1-4 = Pursuer starting positions
    // T = Portal
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

    // Main entry point of the verification program.
    // The program runs only when the "--check" command-line argument is supplied.
    private static int Main(string[] args)
    {
        // Ensure the correct command-line argument has been provided.
        if (args.Length != 1 || args[0] != "--check")
        {
            Console.Error.WriteLine("Usage: dotnet run --project Verification -- --check");
            return 2;
        }

        try
        {
            // Execute every verification routine.
            BoardAndPortal();
            SearchAgreement();
            PlayerEvasion();
            ScoringAndPower();
            CollisionAndEnd();
            RoundReset();
            PursuerChoices();
            AlphaBetaMove();
            SeededRepeatability();

            // Display the total number of completed checks.
            Console.WriteLine($"MazeMind verification passed: {checks} checks.");

            return 0;
        }
        catch (Exception error)
        {
            // Display the reason why verification failed.
            Console.Error.WriteLine("Verification failed: " + error.Message);

            return 1;
        }
    }

    // Creates a new maze board from the predefined test map.
    // A fresh board is returned every time to prevent tests
    // from affecting one another.
    private static MazeBoard Board() => MazeBoard.Parse(Map);

    // Verifies whether a test condition is true.
    // Every call increases the total check counter.
    // If the condition fails, an exception is thrown immediately.
    private static void Require(bool condition, string message)
    {
        checks++;

        if (!condition)
            throw new InvalidOperationException(message);
    }

    // Verifies that portal behaviour and movement validation work correctly.
    private static void BoardAndPortal()
    {
        // Create a fresh maze board.
        var board = Board();

        // Retrieve both portal locations.
        var ends = board.Portals.Keys.OrderBy(c => c.X).ToArray();

        // Verify that exactly two portal endpoints exist.
        Require(ends.Length == 2, "portal pair missing");

        // Verify that travelling through a portal works in both directions.
        Require(board.IsLegalStep(ends[0], ends[1]) &&
                board.IsLegalStep(ends[1], ends[0]),
                "portal is not bidirectional");

        // Verify that movement outside the maze is rejected.
        Require(!board.IsLegalStep(board.PlayerStart, new Cell(-9, -9)),
                "illegal step accepted");
    }

    // Verifies that both A* and Dijkstra produce
    // identical shortest path costs.
    private static void SearchAgreement()
    {
        // Create a new board.
        var board = Board();

        // Create the route search object.
        var search = new RouteSearch(board);

        // Select one pursuer position as the destination.
        Cell goal = board.PursuerStarts[1];

        // Compute the route using A*.
        var a = search.Find(board.PlayerStart, goal, SearchStyle.AStar);

        // Compute the route using Dijkstra.
        var d = search.Find(board.PlayerStart, goal, SearchStyle.Dijkstra);

        // Verify that both algorithms successfully found a route.
        Require(a.Found && d.Found, "route search failed");

        // Verify that both algorithms produced the same shortest distance.
        Require(a.Cost == d.Cost, "A* and Dijkstra costs differ");
    }

    // Verifies that the player switches to evasion behaviour
    // whenever a dangerous pursuer is nearby.
    private static void PlayerEvasion()
    {
        // Create a fresh game model.
        var model = new MatchModel(Board());

        // Select a neighbouring cell to position the pursuer.
        var threat = model.Board.Neighbors(model.PlayerPosition)[0];

        // Place one pursuer beside the player.
        model.Pursuers[0].Position = threat;
        model.Pursuers[0].Mode = PursuerMode.Hunt;

        // Make all remaining pursuers inactive.
        for (int i = 1; i < 4; i++)
            model.Pursuers[i].Mode = PursuerMode.Returning;

        // Create the player AI.
        var planner = new PlayerPlanner(model.Board);

        // Ask the AI to choose its next move.
        Cell choice = planner.Choose(model);

        // Verify that the player entered evasion mode.
        Require(planner.Intent == PlayerIntent.Evade,
                "near threat did not trigger Evade");

        // Verify that the chosen move avoids the dangerous pursuer
        // while remaining a legal move.
        Require(choice != threat &&
                model.Board.IsLegalStep(model.PlayerPosition, choice),
                "evade decision is unsafe or illegal");
    }

    // Verifies scoring, power pellets and vulnerable pursuer behaviour.
    private static void ScoringAndPower()
    {
        // Create a new game model.
        var model = new MatchModel(Board());

        // Create the route search helper.
        var routes = new RouteSearch(model.Board);

        // Find the nearest pellet.
        Cell pellet = model.Pellets
            .OrderBy(c => routes.Find(model.PlayerPosition, c, SearchStyle.AStar).Cost)
            .First();

        // Move the player to the pellet.
        Walk(model, routes.Find(model.PlayerPosition, pellet, SearchStyle.AStar).Route);

        // Verify pellet scoring.
        Require(model.Score >= 10,
                "pellet did not score 10");

        // Find a power pellet.
        Cell power = model.PowerPellets.First();

        // Move the player to the power pellet.
        Walk(model, routes.Find(model.PlayerPosition, power, SearchStyle.AStar).Route);

        // Verify power pellet scoring.
        Require(model.Score >= 60,
                "power pellet did not score 50");

        // Verify that all pursuers become vulnerable.
        Require(model.PowerRemaining > 0 &&
                model.Pursuers.All(p => p.Mode == PursuerMode.Vulnerable),
                "power pellet did not activate one vulnerable mode");

        // Place one vulnerable pursuer beside the player.
        Cell contact = model.Board.Neighbors(model.PlayerPosition)[0];

        // Store the current score.
        int before = model.Score;

        model.Pursuers[0].Position = contact;

        // Keep the remaining pursuers stationary.
        var holds = model.Pursuers.Select(p => p.Position).ToArray();

        // Move into the vulnerable pursuer.
        var eaten = model.ApplyStep(contact, holds);

        // Verify that the player receives 200 points and
        // that the pursuer enters Returning mode.
        Require(eaten.PursuersEaten == 1 &&
                model.Score == before + 200 &&
                model.Pursuers[0].Mode == PursuerMode.Returning,
                "vulnerable pursuer did not return for 200 points");
    }

    // Moves the player along every cell in the supplied route.
    // During movement, every pursuer remains stationary.
    private static void Walk(MatchModel model, IReadOnlyList<Cell> route)
    {
        // Skip the first cell because it is the player's current position.
        for (int step = 1; step < route.Count; step++)
        {
            // Store stationary positions for all pursuers.
            var holds = new Cell[4];

            // Copy each pursuer's current position.
            for (int i = 0; i < 4; i++)
            {
                  // Reset every pursuer back to its original starting position.
            // This keeps the ghosts stationary while the player follows the
            // calculated route during the verification test.
            model.Pursuers[i].Position = model.Board.PursuerStarts[i];

            // Place each pursuer into Roam mode so they do not actively chase
            // the player while this movement test is running.
            model.Pursuers[i].Mode = PursuerMode.Roam;

            // Store each pursuer's current location so they remain in place
            // when ApplyStep() is executed.
            holds[i] = model.Board.PursuerStarts[i];
        }

        // Move the player to the next position in the calculated route while
        // all pursuers remain fixed at their starting positions.
        model.ApplyStep(route[step], holds);
    }
}

// Tests that collisions correctly remove player lives and that the game ends
// after all available lives have been lost.
private static void CollisionAndEnd()
{
    var model = new MatchModel(Board());

    // Simulate three dangerous collisions.
    for (int loss = 0; loss < 3; loss++)
    {
        // Select a legal neighbouring tile for the collision.
        Cell next = model.Board.Neighbors(model.PlayerPosition)[0];

        // Place one pursuer directly in the player's path and make it dangerous.
        model.Pursuers[0].Position = next;
        model.Pursuers[0].Mode = PursuerMode.Hunt;

        // Keep the remaining pursuers safely inside the home area.
        for (int i = 1; i < 4; i++)
        {
            model.Pursuers[i].Position = model.Board.Home;
            model.Pursuers[i].Mode = PursuerMode.Returning;
        }

        // Move the player into the dangerous pursuer.
        var result = model.ApplyStep(
            next,
            new[] { next, model.Board.Home, model.Board.Home, model.Board.Home });

        // Verify that one life was removed.
        Require(result.LifeLost, "dangerous collision did not cost a life");
    }

    // Confirm that losing three lives finishes the match.
    Require(model.Lives == 0 && model.IsOver,
        "third collision did not end match");
}

// Tests that clearing every collectible starts the next round and restores
// the maze contents.
private static void RoundReset()
{
    var model = new MatchModel(Board());

    // Remove every pellet from the maze.
    model.Pellets.Clear();
    model.PowerPellets.Clear();

    // Place one pellet beneath the player so collecting it clears the board.
    model.Pellets.Add(model.PlayerPosition);

    // Collect the final pellet.
    var result = model.ApplyStep(
        model.PlayerPosition,
        model.Pursuers.Select(p => p.Position).ToArray());

    // Verify that the next round begins.
    Require(result.RoundCleared && model.Round == 2,
        "empty board did not advance round");

    // Ensure all pellets were restored for the new round.
    Require(model.Pellets.Count == model.Board.InitialPellets.Count,
        "round did not restore pellets");
}

// Tests that the pursuer planner always generates one legal move for every
// pursuer.
private static void PursuerChoices()
{
    var model = new MatchModel(Board());
    var planner = new PursuerPlanner(model.Board, 7);

    // Force every pursuer into Hunt mode.
    foreach (var actor in model.Pursuers)
        actor.Mode = PursuerMode.Hunt;

    // Calculate moves for all four pursuers.
    var choices = planner.ChooseAll(model);

    // Verify that four decisions were returned.
    Require(choices.Count == 4,
        "planner did not return four choices");

    // Check every selected move is legal.
    for (int i = 0; i < 4; i++)
        Require(
            model.Board.IsLegalStep(model.Pursuers[i].Position, choices[i]),
            $"illegal role choice {i}");
}

// Tests that the alpha-beta search always produces a valid move and explores
// at least one search node.
private static void AlphaBetaMove()
{
    var board = Board();
    var search = new AlphaBetaLookAhead(board);

    // Starting location for the first pursuer.
    Cell start = board.PursuerStarts[0];

    // Calculate the best move using alpha-beta search.
    Cell move = search.Choose(
        start,
        board.PlayerStart,
        board.PlayerStart,
        4);

    // Confirm the chosen move is valid.
    Require(board.IsLegalStep(start, move),
        "alpha-beta produced illegal move");

    // Ensure the search actually evaluated positions.
    Require(search.LastExplored > 0,
        "alpha-beta explored no states");
}

// Tests that using the same random seed always produces identical random
// decisions for wandering pursuers.
private static void SeededRepeatability()
{
    var firstModel = new MatchModel(Board());
    var secondModel = new MatchModel(Board());

    // Create two planners using the same seed.
    var first = new PursuerPlanner(firstModel.Board, 99);
    var second = new PursuerPlanner(secondModel.Board, 99);

    var a = new List<Cell>();
    var b = new List<Cell>();

    // Record eight decisions from the rover pursuer in each planner.
    for (int turn = 0; turn < 8; turn++)
    {
        a.Add(first.ChooseAll(firstModel)[3]);
        b.Add(second.ChooseAll(secondModel)[3]);
    }

    // Both planners should generate the exact same movement sequence.
    Require(a.SequenceEqual(b),
        "same seed did not repeat wandering choices");
}
