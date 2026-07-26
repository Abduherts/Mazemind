using System;

namespace MazeMind
{
    public readonly struct Cell : IEquatable<Cell>, IComparable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y) { X = x; Y = y; }
        public int CompareTo(Cell other)
        {
            int y = Y.CompareTo(other.Y);
            return y != 0 ? y : X.CompareTo(other.X);
        }
        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);
        public int Manhattan(Cell other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        public override string ToString() => $"({X},{Y})";
    }

    public enum SearchStyle { AStar, Dijkstra }
    public enum PlayerIntent { Collect, Evade }
    public enum PursuerRole { Spear, Seer, Keeper, Rover }
    public enum PursuerMode { Home, Roam, Hunt, Vulnerable, Returning }
}