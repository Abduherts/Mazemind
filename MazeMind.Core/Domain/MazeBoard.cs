using System;
using System.Collections.Generic;
using System.Linq;

namespace MazeMind
{
    public sealed class MazeBoard
    {
        private static readonly Cell[] Steps =
        {
            new Cell(0, 1), new Cell(-1, 0), new Cell(1, 0), new Cell(0, -1)
        };
        private readonly HashSet<Cell> floor;
        private readonly Dictionary<Cell, Cell> portals;

        public int Width { get; }
        public int Height { get; }
        public Cell PlayerStart { get; }
        public Cell Home { get; }
        public IReadOnlyList<Cell> PursuerStarts { get; }
        public IReadOnlyCollection<Cell> InitialPellets { get; }
        public IReadOnlyCollection<Cell> InitialPowerPellets { get; }
        public IReadOnlyCollection<Cell> Floor => floor;
        public IReadOnlyDictionary<Cell, Cell> Portals => portals;

        private MazeBoard(int width, int height, HashSet<Cell> floor,
            HashSet<Cell> pellets, HashSet<Cell> powers, Cell player,
            Cell home, List<Cell> starts, Dictionary<Cell, Cell> portals)
        {
            Width = width; Height = height; this.floor = floor;
            InitialPellets = pellets; InitialPowerPellets = powers;
            PlayerStart = player; Home = home; PursuerStarts = starts;
            this.portals = portals;
        }

        public static MazeBoard Parse(params string[] rows)
        {
            if (rows == null || rows.Length < 3) throw new ArgumentException("Maze needs at least three rows.");
            int width = rows[0]?.Length ?? 0;
            if (width < 3 || rows.Any(r => r == null || r.Length != width))
                throw new ArgumentException("Maze rows must be rectangular and at least three cells wide.");

            var floor = new HashSet<Cell>(); var pellets = new HashSet<Cell>();
            var powers = new HashSet<Cell>(); var starts = new SortedDictionary<int, Cell>();
            var tunnel = new List<Cell>(); Cell? player = null; Cell? home = null;
            for (int row = 0; row < rows.Length; row++)
            for (int x = 0; x < width; x++)
            {
                char token = rows[row][x]; var cell = new Cell(x, rows.Length - 1 - row);
                if (token == '#') continue;
                if (" .oPHT1234".IndexOf(token) < 0)
                    throw new ArgumentException($"Unsupported maze token '{token}' at row {row}, column {x}.");
                floor.Add(cell);
                switch (token)
                {
                    case '.': pellets.Add(cell); break;
                    case 'o': powers.Add(cell); break;
                    case 'P': if (player.HasValue) throw new ArgumentException("Maze has multiple player starts."); player = cell; break;
                    case 'H': if (home.HasValue) throw new ArgumentException("Maze has multiple homes."); home = cell; break;
                    case 'T': tunnel.Add(cell); break;
                    default:
                        if (token >= '1' && token <= '4') starts[token - '0'] = cell;
                        break;
                }
            }
            if (!player.HasValue || !home.HasValue || starts.Count != 4 ||
                !Enumerable.Range(1, 4).All(starts.ContainsKey))
                throw new ArgumentException("Maze requires P, H, and starts 1 through 4.");
            if (tunnel.Count != 0 && tunnel.Count != 2)
                throw new ArgumentException("Tunnel token T must occur exactly twice.");
            var links = new Dictionary<Cell, Cell>();
            if (tunnel.Count == 2) { links[tunnel[0]] = tunnel[1]; links[tunnel[1]] = tunnel[0]; }
            return new MazeBoard(width, rows.Length, floor, pellets, powers, player.Value,
                home.Value, starts.Values.ToList(), links);
        }

        public bool IsFloor(Cell cell) => floor.Contains(cell);

        public IReadOnlyList<Cell> Neighbors(Cell cell)
        {
            if (!floor.Contains(cell)) return Array.Empty<Cell>();
            var result = new List<Cell>(5);
            foreach (var step in Steps)
            {
                var next = cell + step;
                if (floor.Contains(next)) result.Add(next);
            }
            if (portals.TryGetValue(cell, out var exit)) result.Add(exit);
            return result;
        }

        public bool IsLegalStep(Cell from, Cell to) => Neighbors(from).Contains(to);

        public int OpenDistance(Cell a, Cell b)
        {
            int best = a.Manhattan(b);
            foreach (var pair in portals)
                best = Math.Min(best, a.Manhattan(pair.Key) + 1 + pair.Value.Manhattan(b));
            return best;
        }
    }
}