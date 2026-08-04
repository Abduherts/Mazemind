using System;
using System.Collections.Generic;
using System.Linq;

namespace MazeMind
{
    // Represents the maze layout used during the game.
    // This class stores the maze dimensions, walkable paths,
    // pellets, power pellets, portals and starting positions.
    public sealed class MazeBoard
    {
        // Defines the four possible movement directions:
        // Up, Left, Right and Down.
        private static readonly Cell[] Steps =
        {
            new Cell(0, 1), new Cell(-1, 0), new Cell(1, 0), new Cell(0, -1)
        };

        // Stores every walkable cell in the maze.
        private readonly HashSet<Cell> floor;

        // Stores portal connections used for teleportation.
        private readonly Dictionary<Cell, Cell> portals;

        // Width of the maze.
        public int Width { get; }

        // Height of the maze.
        public int Height { get; }

        // Player starting position.
        public Cell PlayerStart { get; }

        // Home position used by returning pursuers.
        public Cell Home { get; }

        // Starting positions for all four pursuers.
        public IReadOnlyList<Cell> PursuerStarts { get; }

        // Initial locations of all normal pellets.
        public IReadOnlyCollection<Cell> InitialPellets { get; }

        // Initial locations of all power pellets.
        public IReadOnlyCollection<Cell> InitialPowerPellets { get; }

        // Provides read-only access to all walkable cells.
        public IReadOnlyCollection<Cell> Floor => floor;

        // Provides read-only access to all portal connections.
        public IReadOnlyDictionary<Cell, Cell> Portals => portals;

        // Initialise the maze using validated data produced
        // by the Parse() method.
        private MazeBoard(int width, int height, HashSet<Cell> floor,
            HashSet<Cell> pellets, HashSet<Cell> powers, Cell player,
            Cell home, List<Cell> starts, Dictionary<Cell, Cell> portals)
        {
            // Store maze dimensions.
            Width = width;
            Height = height;

            // Store all walkable cells.
            this.floor = floor;

            // Store pellet locations.
            InitialPellets = pellets;

            // Store power pellet locations.
            InitialPowerPellets = powers;

            // Store player starting position.
            PlayerStart = player;

            // Store pursuer home position.
            Home = home;

            // Store pursuer starting positions.
            PursuerStarts = starts;

            // Store portal connections.
            this.portals = portals;
        }

        // Convert a text-based maze into a MazeBoard object.
        public static MazeBoard Parse(params string[] rows)
        {
            // Ensure the maze contains enough rows.
            if (rows == null || rows.Length < 3) throw new ArgumentException("Maze needs at least three rows.");

            // Determine maze width using the first row.
            int width = rows[0]?.Length ?? 0;

            // Ensure every row has the same length.
            if (width < 3 || rows.Any(r => r == null || r.Length != width))
                throw new ArgumentException("Maze rows must be rectangular and at least three cells wide.");

            // Create collections used while building the maze.
            var floor = new HashSet<Cell>();
            var pellets = new HashSet<Cell>();
            var powers = new HashSet<Cell>();
            var starts = new SortedDictionary<int, Cell>();
            var tunnel = new List<Cell>();

            // Store the player and home positions once found.
            Cell? player = null;
            Cell? home = null;

            // Scan every row and column of the maze.
            for (int row = 0; row < rows.Length; row++)
            for (int x = 0; x < width; x++)
            {
                // Read the current maze symbol.
                char token = rows[row][x];

                // Convert the row and column into maze coordinates.
                var cell = new Cell(x, rows.Length - 1 - row);

                // Ignore walls.
                if (token == '#') continue;

                // Ensure only supported symbols are used.
                if (" .oPHT1234".IndexOf(token) < 0)
                    throw new ArgumentException($"Unsupported maze token '{token}' at row {row}, column {x}.");

                // Record this location as a walkable tile.
                floor.Add(cell);

                // Process the maze symbol.
                switch (token)
                {
                    // Normal pellet.
                    case '.':
                        pellets.Add(cell);
                        break;

                    // Power pellet.
                    case 'o':
                        powers.Add(cell);
                        break;

                    // Player starting position.
                    case 'P':
                        if (player.HasValue) throw new ArgumentException("Maze has multiple player starts.");
                        player = cell;
                        break;

                    // Pursuer home location.
                    case 'H':
                        if (home.HasValue) throw new ArgumentException("Maze has multiple homes.");
                        home = cell;
                        break;

                    // Portal location.
                    case 'T':
                        tunnel.Add(cell);
                        break;

                    // Pursuer starting positions (1–4).
                    default:
                        if (token >= '1' && token <= '4')
                            starts[token - '0'] = cell;
                        break;
                }
            }

            // Ensure every required object exists.
            if (!player.HasValue || !home.HasValue || starts.Count != 4 ||
                !Enumerable.Range(1, 4).All(starts.ContainsKey))
                throw new ArgumentException("Maze requires P, H, and starts 1 through 4.");

            // A portal system must contain exactly two portal tiles.
            if (tunnel.Count != 0 && tunnel.Count != 2)
                throw new ArgumentException("Tunnel token T must occur exactly twice.");

            // Create portal connections.
            var links = new Dictionary<Cell, Cell>();

            // Connect the two portals together.
            if (tunnel.Count == 2) { links[tunnel[0]] = tunnel[1]; links[tunnel[1]] = tunnel[0]; }

            // Return the completed maze.
            return new MazeBoard(width, rows.Length, floor, pellets, powers, player.Value,
                home.Value, starts.Values.ToList(), links);
        }

        // Determine whether the specified cell can be walked on.
        public bool IsFloor(Cell cell) => floor.Contains(cell);

        // Return every valid neighbouring cell that can be reached
        // from the current location.
        public IReadOnlyList<Cell> Neighbors(Cell cell)
        {
            // Ignore locations that are not part of the maze.
            if (!floor.Contains(cell)) return Array.Empty<Cell>();

            // Store all possible movements.
            var result = new List<Cell>(5);

            // Check every movement direction.
            foreach (var step in Steps)
            {
                var next = cell + step;

                // Only include walkable neighbours.
                if (floor.Contains(next)) result.Add(next);
            }

            // Add the connected portal destination if one exists.
            if (portals.TryGetValue(cell, out var exit)) result.Add(exit);

            return result;
        }

        // Check whether movement between two cells is allowed.
        public bool IsLegalStep(Cell from, Cell to) => Neighbors(from).Contains(to);

        // Calculate the shortest estimated distance between two cells.
        // Portal shortcuts are also considered.
        public int OpenDistance(Cell a, Cell b)
        {
            // Start with the normal Manhattan distance.
            int best = a.Manhattan(b);

            // Check whether travelling through a portal
            // produces a shorter route.
            foreach (var pair in portals)
                best = Math.Min(best, a.Manhattan(pair.Key) + 1 + pair.Value.Manhattan(b));

            return best;
        }
    }
}
