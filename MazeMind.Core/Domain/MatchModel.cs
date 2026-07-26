using System;
using System.Collections.Generic;
using System.Linq;

namespace MazeMind
{
    public sealed class PursuerState
    {
        public PursuerRole Role { get; }
        public Cell Position { get; internal set; }
        public PursuerMode Mode { get; internal set; }
        internal PursuerState(PursuerRole role, Cell position)
        { Role = role; Position = position; Mode = PursuerMode.Roam; }
    }

    public readonly struct MatchStepResult
    {
        public readonly bool PelletEaten, PowerEaten, LifeLost, RoundCleared, MatchEnded;
        public readonly int PursuersEaten;
        public MatchStepResult(bool pellet, bool power, int eaten, bool life, bool round, bool ended)
        { PelletEaten = pellet; PowerEaten = power; PursuersEaten = eaten;
          LifeLost = life; RoundCleared = round; MatchEnded = ended; }
    }

    public sealed class MatchModel
    {
        private const int PelletPoints = 10, PowerPoints = 50, PursuerPoints = 200;
        private const int PhaseTicks = 40, PowerTicks = 28;
        private readonly List<PursuerState> pursuers = new List<PursuerState>();

        public MazeBoard Board { get; }
        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int Round { get; private set; }
        public int SurvivalTicks { get; private set; }
        public bool Paused { get; set; }
        public bool IsOver { get; private set; }
        public Cell PlayerPosition { get; private set; }
        public IReadOnlyList<PursuerState> Pursuers => pursuers;
        public HashSet<Cell> Pellets { get; private set; }
        public HashSet<Cell> PowerPellets { get; private set; }
        public int PhaseRemaining { get; private set; }
        public int PowerRemaining { get; private set; }
        public PursuerMode GlobalPhase { get; private set; }

        public MatchModel(MazeBoard board)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            if (board.PursuerStarts.Count != 4) throw new ArgumentException("Exactly four pursuer starts are required.");
            for (int i = 0; i < 4; i++) pursuers.Add(new PursuerState((PursuerRole)i, board.PursuerStarts[i]));
            Restart();
        }

        public void Restart()
        {
            Score = 0; Lives = 3; Round = 1; SurvivalTicks = 0;
            Paused = false; IsOver = false;
            ResetBoardState();
        }

        private void ResetBoardState()
        {
            Pellets = new HashSet<Cell>(Board.InitialPellets);
            PowerPellets = new HashSet<Cell>(Board.InitialPowerPellets);
            GlobalPhase = PursuerMode.Roam; PhaseRemaining = PhaseTicks; PowerRemaining = 0;
            ResetActors();
        }

        private void ResetActors()
        {
            PlayerPosition = Board.PlayerStart;
            for (int i = 0; i < pursuers.Count; i++)
            {
                pursuers[i].Position = Board.PursuerStarts[i];
                pursuers[i].Mode = GlobalPhase == PursuerMode.Hunt ? PursuerMode.Hunt : PursuerMode.Roam;
            }
        }

        public MatchStepResult ApplyStep(Cell requestedPlayer, IReadOnlyList<Cell> requestedPursuers)
        {
            if (Paused || IsOver) return default;
            if (requestedPursuers == null || requestedPursuers.Count != 4)
                throw new ArgumentException("Four pursuer decisions are required.");

            SurvivalTicks++;
            UpdateTimersAndModes();
            Cell oldPlayer = PlayerPosition;
            var oldPursuers = pursuers.Select(p => p.Position).ToArray();
            PlayerPosition = LegalOrStay(oldPlayer, requestedPlayer);
            for (int i = 0; i < 4; i++)
                pursuers[i].Position = LegalOrStay(oldPursuers[i], requestedPursuers[i]);

            bool pellet = Pellets.Remove(PlayerPosition);
            bool power = PowerPellets.Remove(PlayerPosition);
            if (pellet) Score += PelletPoints;
            if (power)
            {
                Score += PowerPoints; PowerRemaining = PowerTicks;
                foreach (var actor in pursuers)
                    if (actor.Mode != PursuerMode.Returning) actor.Mode = PursuerMode.Vulnerable;
            }

            var collisions = new List<int>();
            for (int i = 0; i < 4; i++)
                if (pursuers[i].Position == PlayerPosition ||
                    (oldPursuers[i] == PlayerPosition && pursuers[i].Position == oldPlayer)) collisions.Add(i);

            bool dangerous = collisions.Any(i => pursuers[i].Mode != PursuerMode.Vulnerable &&
                                                  pursuers[i].Mode != PursuerMode.Returning);
            int eaten = 0;
            if (dangerous)
            {
                Lives--; PowerRemaining = 0;
                if (Lives <= 0) IsOver = true; else ResetActors();
                return new MatchStepResult(pellet, power, 0, true, false, IsOver);
            }
            foreach (int i in collisions)
            {
                if (pursuers[i].Mode != PursuerMode.Vulnerable) continue;
                pursuers[i].Mode = PursuerMode.Returning; Score += PursuerPoints; eaten++;
            }
            foreach (var actor in pursuers)
                if (actor.Mode == PursuerMode.Returning && actor.Position == Board.Home)
                    actor.Mode = GlobalPhase;

            bool cleared = Pellets.Count == 0 && PowerPellets.Count == 0;
            if (cleared) { Round++; ResetBoardState(); }
            return new MatchStepResult(pellet, power, eaten, false, cleared, false);
        }

        private Cell LegalOrStay(Cell from, Cell requested) =>
            requested == from || Board.IsLegalStep(from, requested) ? requested : from;

        private void UpdateTimersAndModes()
        {
            if (PowerRemaining > 0)
            {
                PowerRemaining--;
                if (PowerRemaining == 0)
                    foreach (var actor in pursuers)
                        if (actor.Mode == PursuerMode.Vulnerable) actor.Mode = GlobalPhase;
            }
            PhaseRemaining--;
            if (PhaseRemaining > 0) return;
            GlobalPhase = GlobalPhase == PursuerMode.Hunt ? PursuerMode.Roam : PursuerMode.Hunt;
            PhaseRemaining = PhaseTicks;
            foreach (var actor in pursuers)
                if (actor.Mode == PursuerMode.Hunt || actor.Mode == PursuerMode.Roam)
                    actor.Mode = GlobalPhase;
        }
    }
}