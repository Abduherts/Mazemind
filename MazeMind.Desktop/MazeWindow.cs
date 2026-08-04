using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MazeMind.Desktop
{
    // Main application window that displays the maze,
    // runs the game loop, and handles user interaction.
    public sealed class MazeWindow : Form
    {
        // Built-in maze layout used when a new game starts.
        // Each character represents a different maze object.
        // # = Wall
        // . = Pellet
        // o = Power pellet
        // P = Player start position
        // H = Pursuer home
        // 1-4 = Pursuer starting positions
        // T = Portal/Tunnel
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

        // Timer that controls how frequently the game updates.
        // An interval of 180 milliseconds determines the game speed.
        private readonly Timer gameClock = new Timer { Interval = 180 };

        // Font used to draw the game title.
        private readonly Font titleFont = new Font("Segoe UI", 15f, FontStyle.Bold);

        // Font used to display gameplay statistics and messages.
        private readonly Font hudFont = new Font("Consolas", 10f);

        // Stores the current game model containing all game data.
        private MatchModel match;

        // Controls the player's AI movement decisions.
        private PlayerPlanner playerMind;

        // Controls all pursuer AI movement decisions.
        private PursuerPlanner pursuerMind;

        // Records execution time and search statistics.
        private RunTelemetry telemetry;

        // Stores the currently selected search algorithm.
        // A* is used when the game first starts.
        private SearchStyle algorithm = SearchStyle.AStar;

        // Message displayed in the information area.
        private string notice = "Space pause · R restart · Tab algorithm · E export CSV";

        // Creates the application window and prepares the game.
        public MazeWindow()
        {
            // Set the text displayed in the window title bar.
            Text = "Maze Mind — Classical AI Demonstrator";

            // Set the initial window size.
            ClientSize = new Size(1000, 720);

            // Prevent the window from being resized too small.
            MinimumSize = new Size(720, 560);

            // Set the background colour.
            BackColor = Color.FromArgb(4, 6, 18);

            // Allow keyboard input even when controls have focus.
            KeyPreview = true;

            // Enable double buffering to reduce screen flickering.
            DoubleBuffered = true;

            // Create and initialise a new game.
            StartNewMatch();

            // Execute the game update every timer tick.
            gameClock.Tick += OnGameTick;

            // Listen for keyboard input.
            KeyDown += OnKeyPressed;

            // Redraw the screen whenever the window size changes.
            Resize += delegate { Invalidate(); };

            // Start the game timer.
            gameClock.Start();
        }

        // Creates a completely new game session.
        private void StartNewMatch()
        {
            // Parse the predefined maze layout and create the game model.
            match = new MatchModel(MazeBoard.Parse(DemoMap));

            // Create the player AI using the selected search algorithm.
            playerMind = new PlayerPlanner(match.Board, algorithm);

            // Create the pursuer AI using the same search algorithm.
            // The seed ensures repeatable random behaviour.
            pursuerMind = new PursuerPlanner(match.Board, 2026, algorithm);

            // Reset telemetry measurements.
            telemetry = new RunTelemetry();

            // Restore the default information message.
            notice = "Space pause · R restart · Tab algorithm · E export CSV";

            // Request the window to redraw immediately.
            Invalidate();
        }

        // Executes once every timer interval.
        // This represents one complete game update.
        private void OnGameTick(object sender, EventArgs e)
        {
            // Stop updating while the game is paused
            // or after the game has finished.
            if (match.Paused || match.IsOver) return;

            // Start measuring the player's AI execution time.
            var clock = Stopwatch.StartNew();

            // Ask the player AI to choose its next move.
            Cell playerMove = playerMind.Choose(match);

            // Stop timing the player search.
            clock.Stop();

            // Store the elapsed execution time.
            double playerMilliseconds = clock.Elapsed.TotalMilliseconds;

            // Restart the timer to measure the pursuer AI.
            clock.Restart();

            // Ask every pursuer to choose its next move.
            IReadOnlyList<Cell> pursuerMoves = pursuerMind.ChooseAll(match);

            // Stop timing the pursuer search.
            clock.Stop();

            // Apply all calculated moves to the game model.
            match.ApplyStep(playerMove, pursuerMoves);

            // Record timing and search statistics.
            telemetry.Add(match, playerMilliseconds, playerMind.LastExplored,
                clock.Elapsed.TotalMilliseconds, pursuerMind.LastExplored, algorithm);

            // Redraw the updated game state.
            Invalidate();
        }

        // Handles all keyboard controls.
        private void OnKeyPressed(object sender, KeyEventArgs e)
        {
            // Space pauses or resumes gameplay.
            if (e.KeyCode == Keys.Space)
            {
                // Toggle pause state.
                match.Paused = !match.Paused;

                // Display the current state.
                notice = match.Paused ? "Paused" : "Running";
            }

            // R creates a completely new game.
            else if (e.KeyCode == Keys.R) StartNewMatch();

            // Tab switches between A* and Dijkstra.
            else if (e.KeyCode == Keys.Tab)
            {
                // Toggle the routing algorithm.
                algorithm = algorithm == SearchStyle.AStar ? SearchStyle.Dijkstra : SearchStyle.AStar;

                // Update the player's planner.
                playerMind.SearchStyle = algorithm;

                // Update the pursuer planner.
                pursuerMind.SearchStyle = algorithm;

                // Inform the user about the selected algorithm.
                notice = "Routing changed to " + algorithm;

                // Prevent Windows from processing the Tab key.
                e.SuppressKeyPress = true;
            }

            // E exports telemetry data.
            else if (e.KeyCode == Keys.E) ExportTelemetry();

            // Refresh the display after processing the key.
            Invalidate();
        }

        // Saves the recorded telemetry information to a CSV file.
        private void ExportTelemetry()
        {
                   // Exports the telemetry data collected during gameplay to a CSV file.
        // The file is saved inside the user's Local Application Data folder
        // so it can be accessed later for analysis or performance comparison.
        private void ExportTelemetry()
        {
            // Build the output directory path.
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MazeMind");

            // Export the telemetry data and display the saved file location.
            notice = "Saved " + telemetry.Export(folder);
        }

        // Automatically called whenever the window needs to be redrawn.
        // Responsible for rendering the complete user interface.
        protected override void OnPaint(PaintEventArgs e)
        {
            // Execute the default painting behaviour provided by Form.
            base.OnPaint(e);

            // Enable anti-aliasing to produce smoother graphics.
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw the information panel at the top.
            DrawHud(e.Graphics);

            // Draw the maze and all game objects.
            DrawBoard(e.Graphics);
        }

        // Draws the information panel showing game statistics and AI performance.
        private void DrawHud(Graphics canvas)
        {
            // Brush used for primary text.
            using var primary = new SolidBrush(Color.White);

            // Brush used for secondary information.
            using var secondary = new SolidBrush(Color.FromArgb(160, 205, 255));

            // Draw the game title.
            canvas.DrawString("MAZE MIND", titleFont, primary, 14, 10);

            // Display score, lives, round, selected search algorithm,
            // current player behaviour and current pursuer phase.
            canvas.DrawString($"Score {match.Score:0000}   Lives {match.Lives}   Round {match.Round}   " +
                $"{algorithm}   Player {playerMind.Intent}   Global {match.GlobalPhase}", hudFont, primary, 16, 42);

            // Display execution time and explored nodes for both AI systems.
            canvas.DrawString($"Player {telemetry.PlayerMilliseconds:0.000} ms / {telemetry.PlayerExplored} cells   " +
                $"Pursuers {telemetry.PursuerMilliseconds:0.000} ms / {telemetry.PursuerExplored} cells",
                hudFont, secondary, 16, 62);

            // Decide which status message should be displayed.
            string state = match.IsOver ? "GAME OVER — press R" : match.Paused ? "PAUSED" : notice;

            // Draw the current status message.
            canvas.DrawString(state, hudFont, secondary, 16, 82);
        }

        // Draws every tile and game object inside the maze.
        private void DrawBoard(Graphics canvas)
        {
            // Reserve space at the top for the HUD.
            const int top = 110;

            // Calculate the size of each maze cell so the board fits inside the window.
            float size = Math.Min((ClientSize.Width - 24f) / match.Board.Width,
                (ClientSize.Height - top - 16f) / match.Board.Height);

            // Centre the board horizontally.
            float left = (ClientSize.Width - size * match.Board.Width) * 0.5f;

            // Brushes used to draw floor tiles and wall tiles.
            using var floorBrush = new SolidBrush(Color.FromArgb(8, 12, 28));
            using var wallBrush = new SolidBrush(Color.FromArgb(30, 70, 195));

            // Draw every tile in the maze.
            for (int y = 0; y < match.Board.Height; y++)
            for (int x = 0; x < match.Board.Width; x++)
            {
                // Calculate the drawing area for the current cell.
                RectangleF area = Area(new Cell(x, y), left, top, size);

                // Draw either a floor tile or a wall tile.
                canvas.FillRectangle(match.Board.IsFloor(new Cell(x, y)) ? floorBrush : wallBrush, area);
            }

            // Draw all remaining normal pellets.
            foreach (Cell pellet in match.Pellets)
                DrawCircle(canvas, pellet, left, top, size, Color.FromArgb(255, 215, 110), 0.13f);

            // Draw all remaining power pellets.
            foreach (Cell power in match.PowerPellets)
                DrawCircle(canvas, power, left, top, size, Color.FromArgb(255, 125, 220), 0.28f);

            // Draw the player.
            DrawCircle(canvas, match.PlayerPosition, left, top, size, Color.Gold, 0.72f);

            // Assign colours to each pursuer role.
            Color[] roleColors =
            {
                Color.FromArgb(245, 55, 55),
                Color.FromArgb(55, 220, 245),
                Color.FromArgb(250, 105, 185),
                Color.FromArgb(255, 145, 35)
            };

            // Draw every pursuer.
            for (int i = 0; i < match.Pursuers.Count; i++)
            {
                // Retrieve the current pursuer.
                PursuerState actor = match.Pursuers[i];

                // Select the colour according to its current mode.
                Color color = actor.Mode == PursuerMode.Vulnerable ? Color.RoyalBlue :
                    actor.Mode == PursuerMode.Returning ? Color.White : roleColors[i];

                // Draw the pursuer.
                DrawCircle(canvas, actor.Position, left, top, size, color, 0.7f);
            }
        }

        // Converts a maze cell into its corresponding screen rectangle.
        private RectangleF Area(Cell cell, float left, float top, float size)
        {
            // Flip the Y-axis because screen coordinates start from the top.
            float screenY = match.Board.Height - 1 - cell.Y;

            // Return the rectangle representing this maze cell.
            return new RectangleF(left + cell.X * size, top + screenY * size, size + 0.5f, size + 0.5f);
        }

        // Draws a circular object centred inside a maze tile.
        private void DrawCircle(Graphics canvas, Cell cell, float left, float top,
            float size, Color color, float scale)
        {
            // Get the rectangle occupied by this maze tile.
            RectangleF tile = Area(cell, left, top, size);

            // Calculate the diameter using the supplied scaling factor.
            float diameter = size * scale;

            // Centre the circle horizontally.
            float x = tile.X + (size - diameter) * 0.5f;

            // Centre the circle vertically.
            float y = tile.Y + (size - diameter) * 0.5f;

            // Create the drawing brush.
            using var brush = new SolidBrush(color);

            // Draw the filled circle.
            canvas.FillEllipse(brush, x, y, diameter, diameter);
        }

        // Releases resources used by the window when it is closed.
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose the game timer.
                gameClock.Dispose();

                // Release the title font.
                titleFont.Dispose();

                // Release the HUD font.
                hudFont.Dispose();
            }

            // Execute the base Form cleanup.
            base.Dispose(disposing);
        }
    }
}
