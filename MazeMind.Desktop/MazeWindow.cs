using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MazeMind.Desktop
{
    public sealed class MazeWindow : Form
    {
        private static readonly string[] DemoMap =
        {
            "#####################",
            "#P....#.......#....1#",
            "#.##.#.#.###.#.#.##.#",
            "#o...#...#.#...#...o#",
            "###.###.#...#.###.###",
            "T.....#...H...#.....T",
            "#.###.#.##.##.#.###.#",
            "#...#....234....#...#",
            "#.#.#####.#.#####.#.#",
            "#.........o.........#",
            "#####################"
        };

        private readonly Timer gameClock = new Timer { Interval = 180 };
        private readonly Font titleFont = new Font("Segoe UI", 15f, FontStyle.Bold);
        private readonly Font hudFont = new Font("Consolas", 10f);
        private MatchModel match;
        private PlayerPlanner playerMind;
        private PursuerPlanner pursuerMind;
        private RunTelemetry telemetry;
        private SearchStyle algorithm = SearchStyle.AStar;
        private string notice = "Space pause · R restart · Tab algorithm · E export CSV";

        public MazeWindow()
        {
            Text = "Maze Mind — Classical AI Demonstrator";
            ClientSize = new Size(1000, 720);
            MinimumSize = new Size(720, 560);
            BackColor = Color.FromArgb(4, 6, 18);
            KeyPreview = true;
            DoubleBuffered = true;
            StartNewMatch();
            gameClock.Tick += OnGameTick;
            KeyDown += OnKeyPressed;
            Resize += delegate { Invalidate(); };
            gameClock.Start();
        }

        private void StartNewMatch()
        {
            match = new MatchModel(MazeBoard.Parse(DemoMap));
            playerMind = new PlayerPlanner(match.Board, algorithm);
            pursuerMind = new PursuerPlanner(match.Board, 2026, algorithm);
            telemetry = new RunTelemetry();
            notice = "Space pause · R restart · Tab algorithm · E export CSV";
            Invalidate();
        }
        private void OnGameTick(object sender, EventArgs e)
        {
            if (match.Paused || match.IsOver) return;

            var clock = Stopwatch.StartNew();
            Cell playerMove = playerMind.Choose(match);
            clock.Stop();
            double playerMilliseconds = clock.Elapsed.TotalMilliseconds;

            clock.Restart();
            IReadOnlyList<Cell> pursuerMoves = pursuerMind.ChooseAll(match);
            clock.Stop();

            match.ApplyStep(playerMove, pursuerMoves);
            telemetry.Add(match, playerMilliseconds, playerMind.LastExplored,
                clock.Elapsed.TotalMilliseconds, pursuerMind.LastExplored, algorithm);
            Invalidate();
        }

        private void OnKeyPressed(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                match.Paused = !match.Paused;
                notice = match.Paused ? "Paused" : "Running";
            }
            else if (e.KeyCode == Keys.R) StartNewMatch();
            else if (e.KeyCode == Keys.Tab)
            {
                algorithm = algorithm == SearchStyle.AStar ? SearchStyle.Dijkstra : SearchStyle.AStar;
                playerMind.SearchStyle = algorithm;
                pursuerMind.SearchStyle = algorithm;
                notice = "Routing changed to " + algorithm;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.E) ExportTelemetry();
            Invalidate();
        }

        private void ExportTelemetry()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MazeMind");
            notice = "Saved " + telemetry.Export(folder);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            DrawHud(e.Graphics);
            DrawBoard(e.Graphics);
        }

        private void DrawHud(Graphics canvas)
        {
            using var primary = new SolidBrush(Color.White);
            using var secondary = new SolidBrush(Color.FromArgb(160, 205, 255));
            canvas.DrawString("MAZE MIND", titleFont, primary, 14, 10);
            canvas.DrawString($"Score {match.Score:0000}   Lives {match.Lives}   Round {match.Round}   " +
                $"{algorithm}   Player {playerMind.Intent}   Global {match.GlobalPhase}", hudFont, primary, 16, 42);
            canvas.DrawString($"Player {telemetry.PlayerMilliseconds:0.000} ms / {telemetry.PlayerExplored} cells   " +
                $"Pursuers {telemetry.PursuerMilliseconds:0.000} ms / {telemetry.PursuerExplored} cells",
                hudFont, secondary, 16, 62);
            string state = match.IsOver ? "GAME OVER — press R" : match.Paused ? "PAUSED" : notice;
            canvas.DrawString(state, hudFont, secondary, 16, 82);
        }

        private void DrawBoard(Graphics canvas)
        {
            const int top = 110;
            float size = Math.Min((ClientSize.Width - 24f) / match.Board.Width,
                (ClientSize.Height - top - 16f) / match.Board.Height);
            float left = (ClientSize.Width - size * match.Board.Width) * 0.5f;

            using var floorBrush = new SolidBrush(Color.FromArgb(8, 12, 28));
            using var wallBrush = new SolidBrush(Color.FromArgb(30, 70, 195));
            for (int y = 0; y < match.Board.Height; y++)
            for (int x = 0; x < match.Board.Width; x++)
            {
                RectangleF area = Area(new Cell(x, y), left, top, size);
                canvas.FillRectangle(match.Board.IsFloor(new Cell(x, y)) ? floorBrush : wallBrush, area);
            }

            foreach (Cell pellet in match.Pellets)
                DrawCircle(canvas, pellet, left, top, size, Color.FromArgb(255, 215, 110), 0.13f);
            foreach (Cell power in match.PowerPellets)
                DrawCircle(canvas, power, left, top, size, Color.FromArgb(255, 125, 220), 0.28f);

            DrawCircle(canvas, match.PlayerPosition, left, top, size, Color.Gold, 0.72f);
            Color[] roleColors =
            {
                Color.FromArgb(245, 55, 55), Color.FromArgb(55, 220, 245),
                Color.FromArgb(250, 105, 185), Color.FromArgb(255, 145, 35)
            };
            for (int i = 0; i < match.Pursuers.Count; i++)
            {
                PursuerState actor = match.Pursuers[i];
                Color color = actor.Mode == PursuerMode.Vulnerable ? Color.RoyalBlue :
                    actor.Mode == PursuerMode.Returning ? Color.White : roleColors[i];
                DrawCircle(canvas, actor.Position, left, top, size, color, 0.7f);
            }
        }

        private RectangleF Area(Cell cell, float left, float top, float size)
        {
            float screenY = match.Board.Height - 1 - cell.Y;
            return new RectangleF(left + cell.X * size, top + screenY * size, size + 0.5f, size + 0.5f);
        }

        private void DrawCircle(Graphics canvas, Cell cell, float left, float top,
            float size, Color color, float scale)
        {
            RectangleF tile = Area(cell, left, top, size);
            float diameter = size * scale;
            float x = tile.X + (size - diameter) * 0.5f;
            float y = tile.Y + (size - diameter) * 0.5f;
            using var brush = new SolidBrush(color);
            canvas.FillEllipse(brush, x, y, diameter, diameter);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                gameClock.Dispose();
                titleFont.Dispose();
                hudFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}