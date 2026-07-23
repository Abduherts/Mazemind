using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MazeMind
{
    public sealed class RunTelemetry
    {
        private readonly List<string> rows = new List<string>();
        public double PlayerMilliseconds { get; private set; }
        public double PursuerMilliseconds { get; private set; }
        public int PlayerExplored { get; private set; }
        public int PursuerExplored { get; private set; }
        public int Samples => rows.Count;

        public void Add(MatchModel model, double playerMs, int playerExplored,
            double pursuerMs, int pursuerExplored, SearchStyle style)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            PlayerMilliseconds = Math.Max(0, playerMs);
            PursuerMilliseconds = Math.Max(0, pursuerMs);
            PlayerExplored = Math.Max(0, playerExplored);
            PursuerExplored = Math.Max(0, pursuerExplored);
            rows.Add(string.Join(",", model.SurvivalTicks, model.Score, model.Lives, model.Round,
                style, F(PlayerMilliseconds), PlayerExplored, F(PursuerMilliseconds), PursuerExplored));
        }

        public string ToCsv()
        {
            var text = new StringBuilder("tick,score,lives,round,algorithm,player_ms,player_explored,pursuer_ms,pursuer_explored\n");
            foreach (string row in rows) text.AppendLine(row);
            return text.ToString();
        }

        public string Export(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("An export directory is required.");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "mazemind-telemetry.csv");
            File.WriteAllText(path, ToCsv(), Encoding.UTF8);
            return path;
        }

        private static string F(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
    }
}