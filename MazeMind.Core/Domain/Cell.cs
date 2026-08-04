using System;

namespace MazeMind
{
    // Represents a single location (cell) in the maze.
    // Every object in the game, including the player, pursuers,
    // pellets and walls, uses Cell coordinates to identify
    // their position on the board.
    public readonly struct Cell : IEquatable<Cell>, IComparable<Cell>
    {
        // Horizontal position within the maze.
        public readonly int X;

        // Vertical position within the maze.
        public readonly int Y;

        // Create a new cell using the supplied X and Y coordinates.
        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        // Compare two cells.
        // Cells are ordered by Y coordinate first and then X coordinate.
        // This provides a consistent ordering when multiple cells
        // have the same priority during pathfinding.
        public int CompareTo(Cell other)
        {
            int y = Y.CompareTo(other.Y);

            // If the Y values are different,
            // return the comparison result immediately.
            return y != 0 ? y : X.CompareTo(other.X);
        }

        // Determine whether two cells represent
        // exactly the same maze position.
        public bool Equals(Cell other) =>
            X == other.X && Y == other.Y;

        // Standard object equality implementation.
        // Ensures compatibility with collections
        // such as Dictionary and HashSet.
        public override bool Equals(object obj) =>
            obj is Cell other && Equals(other);

        // Generate a unique hash code for the cell.
        // The multiplication by 397 helps reduce
        // hash collisions when storing cells
        // inside hash-based collections.
        public override int GetHashCode() =>
            unchecked((X * 397) ^ Y);

        // Equality operator.
        // Returns true when both coordinates are identical.
        public static bool operator ==(Cell a, Cell b) =>
            a.Equals(b);

        // Inequality operator.
        // Returns true when either coordinate differs.
        public static bool operator !=(Cell a, Cell b) =>
            !a.Equals(b);

        // Add two coordinate values together.
        // Commonly used when moving in a direction.
        //
        // Example:
        // Current Position + Direction Vector = Next Position
        public static Cell operator +(Cell a, Cell b) =>
            new Cell(a.X + b.X, a.Y + b.Y);

        // Calculate the Manhattan Distance between
        // two cells.
        //
        // Manhattan Distance measures the number
        // of horizontal and vertical moves required
        // to travel between two positions.
        //
        // This heuristic is widely used by A* Search
        // because movement is restricted to four directions.
        public int Manhattan(Cell other) =>
            Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        // Convert the cell into a readable string.
        // Example output:
        // (5,12)
        public override string ToString() =>
            $"({X},{Y})";
    }

    // Search algorithms available for pathfinding.
    // The selected algorithm determines how routes
    // are calculated throughout the game.
    public enum SearchStyle
    {
        // A* Search combines travelled distance
        // with a heuristic estimate to efficiently
        // find an optimal route.
        AStar,

        // Dijkstra's Algorithm explores the maze
        // without using a heuristic and guarantees
        // the shortest path.
        Dijkstra
    }

    // Describes the player's current behaviour.
    public enum PlayerIntent
    {
        // Move towards pellets to increase score.
        Collect,

        // Move away from nearby pursuers to survive.
        Evade
    }

    // Defines the personality assigned
    // to each pursuer.
    public enum PursuerRole
    {
        // Aggressively chases the player.
        Spear,

        // Predicts the player's future position.
        Seer,

        // Guards important areas of the maze.
        Keeper,

        // Moves unpredictably using random choices.
        Rover
    }

    // Represents the current behaviour state
    // of a pursuer.
    public enum PursuerMode
    {
        // Waiting at the home position.
        Home,

        // Patrolling the maze instead of chasing.
        Roam,

        // Actively pursuing the player.
        Hunt,

        // Escaping from the player after
        // a power pellet has been collected.
        Vulnerable,

        // Travelling back to the home location
        // after being captured.
        Returning
    }
}
