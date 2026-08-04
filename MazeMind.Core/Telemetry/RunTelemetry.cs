using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MazeMind
{
    // Records performance information collected during gameplay.
    // This data is later exported as a CSV file for analysis.
    public sealed class RunTelemetry
    {
        // Stores each telemetry record as a CSV row.
        private readonly List<string> rows = new List<string>();

        // Stores the execution time of the player algorithm.
        public double PlayerMilliseconds { get; private set; }

        // Stores the execution time of the pursuer algorithm.
        public double PursuerMilliseconds { get; private set; }

        // Stores the number of search nodes explored
        // by the player algorithm.
        public int PlayerExplored { get; private set; }

        // Stores the number of search nodes explored
        // by the pursuer algorithm.
        public int PursuerExplored { get; private set; }

        // Returns the total number of telemetry samples collected.
        public int Samples => rows.Count;

        // Record performance data for the current game step.
        public void Add(MatchModel model, double playerMs, int playerExplored,
            double pursuerMs, int pursuerExplored, SearchStyle style)
        {
            // Ensure a valid game model has been provided.
            if (model == null) throw new ArgumentNullException(nameof(model));

            // Store the player's execution time.
            // Negative values are prevented.
            PlayerMilliseconds = Math.Max(0, playerMs);

            // Store the pursuer's execution time.
            PursuerMilliseconds = Math.Max(0, pursuerMs);

            // Store the number of explored nodes
            // for the player search.
            PlayerExplored = Math.Max(0, playerExplored);

            // Store the number of explored nodes
            // for the pursuer search.
            PursuerExplored = Math.Max(0, pursuerExplored);

            // Save the current game statistics as a CSV row.
            rows.Add(string.Join(",", model.SurvivalTicks, model.Score, model.Lives, model.Round,
                style, F(PlayerMilliseconds), PlayerExplored, F(PursuerMilliseconds), PursuerExplored));
        }

        // Convert every recorded telemetry entry
        // into CSV format.
        public string ToCsv()
        {
            // Create the CSV header row.
            var text = new StringBuilder("tick,score,lives,round,algorithm,player_ms,player_explored,pursuer_ms,pursuer_explored\n");

            // Append every recorded telemetry entry.
            foreach (string row in rows) text.AppendLine(row);

            // Return the completed CSV document.
            return text.ToString();
        }

        // Export the telemetry data to a CSV file.
        public string Export(string directory)
        {
            // Ensure a valid export location has been provided.
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("An export directory is required.");

            // Create the directory if it does not already exist.
            Directory.CreateDirectory(directory);

            // Create the full file path.
            string path = Path.Combine(directory, "mazemind-telemetry.csv");

            // Write the CSV data using UTF-8 encoding.
            File.WriteAllText(path, ToCsv(), Encoding.UTF8);

            // Return the location of the exported file.
            return path;
        }

        // Format a decimal value using three decimal places
        // and culture-independent formatting.
        private static string F(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
    }
}
