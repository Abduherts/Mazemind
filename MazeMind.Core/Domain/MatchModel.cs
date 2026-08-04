using System;
using System.Collections.Generic;
using System.Linq;

namespace MazeMind
{
    // Represents an individual pursuer in the game.
    // Each pursuer has a unique role, current position,
    // and behaviour mode that changes during gameplay.
    public sealed class PursuerState
    {
        // Stores the personality assigned to the pursuer.
        public PursuerRole Role { get; }

        // Stores the current position of the pursuer.
        public Cell Position { get; internal set; }

        // Stores the current behaviour state of the pursuer.
        public PursuerMode Mode { get; internal set; }

        // Initialise the pursuer with its role and starting position.
        // Every pursuer begins the game in Roam mode.
        internal PursuerState(PursuerRole role, Cell position)
        { Role = role; Position = position; Mode = PursuerMode.Roam; }
    }

    // Stores the outcome produced after one game update.
    // These values are used by the game controller
    // to determine what happened during the current step.
    public readonly struct MatchStepResult
    {
        // Indicates whether a normal pellet was collected.
        public readonly bool PelletEaten, PowerEaten, LifeLost, RoundCleared, MatchEnded;

        // Stores the number of vulnerable pursuers eaten.
        public readonly int PursuersEaten;

        // Save all events that occurred during the current game step.
        public MatchStepResult(bool pellet, bool power, int eaten, bool life, bool round, bool ended)
        {
            PelletEaten = pellet;
            PowerEaten = power;
            PursuersEaten = eaten;
            LifeLost = life;
            RoundCleared = round;
            MatchEnded = ended;
        }
    }

    // Stores and manages the complete state of the game.
    // This includes the player, pursuers, score,
    // pellets, timers and game progression.
    public sealed class MatchModel
    {
        // Points awarded for collecting a normal pellet.
        private const int PelletPoints = 10, PowerPoints = 50, PursuerPoints = 200;

        // Controls how long the global chase phase
        // and power pellet effect remain active.
        private const int PhaseTicks = 40, PowerTicks = 28;

        // Stores all pursuers currently active in the game.
        private readonly List<PursuerState> pursuers = new List<PursuerState>();

        // Reference to the maze used during gameplay.
        public MazeBoard Board { get; }

        // Current player score.
        public int Score { get; private set; }

        // Number of remaining lives.
        public int Lives { get; private set; }

        // Current game round.
        public int Round { get; private set; }

        // Counts how long the player has survived.
        public int SurvivalTicks { get; private set; }

        // Indicates whether the game is currently paused.
        public bool Paused { get; set; }

        // Indicates whether the match has ended.
        public bool IsOver { get; private set; }

        // Stores the player's current position.
        public Cell PlayerPosition { get; private set; }

        // Provides read-only access to every pursuer.
        public IReadOnlyList<PursuerState> Pursuers => pursuers;

        // Stores all remaining normal pellets.
        public HashSet<Cell> Pellets { get; private set; }

        // Stores all remaining power pellets.
        public HashSet<Cell> PowerPellets { get; private set; }

        // Remaining time before switching between Hunt and Roam.
        public int PhaseRemaining { get; private set; }

        // Remaining duration of the power pellet effect.
        public int PowerRemaining { get; private set; }

        // Stores the current global behaviour shared by pursuers.
        public PursuerMode GlobalPhase { get; private set; }

       // Create a new game using the supplied maze board.
public MatchModel(MazeBoard board)
{
    // Store the maze board used throughout the game.
    Board = board ?? throw new ArgumentNullException(nameof(board));

    // Ensure the maze contains exactly four pursuer starting positions.
    if (board.PursuerStarts.Count != 4) throw new ArgumentException("Exactly four pursuer starts are required.");

    // Create four pursuers and place each one at its starting position.
    for (int i = 0; i < 4; i++) pursuers.Add(new PursuerState((PursuerRole)i, board.PursuerStarts[i]));

    // Initialise the game state.
    Restart();
}

// Reset the game to its initial state.
public void Restart()
{
    // Reset the player's score, lives, round number and survival timer.
    Score = 0; Lives = 3; Round = 1; SurvivalTicks = 0;

    // Ensure the game is active and not paused.
    Paused = false; IsOver = false;

    // Restore pellets, timers and actor positions.
    ResetBoardState();
}

// Restore all pellets and reset round information.
private void ResetBoardState()
{
    // Reload every normal pellet onto the maze.
    Pellets = new HashSet<Cell>(Board.InitialPellets);

    // Reload every power pellet.
    PowerPellets = new HashSet<Cell>(Board.InitialPowerPellets);

    // Start the round with pursuers in Roam mode.
    GlobalPhase = PursuerMode.Roam; PhaseRemaining = PhaseTicks; PowerRemaining = 0;

    // Reset the player and pursuers.
    ResetActors();
}

// Return every character to its starting position.
private void ResetActors()
{
    // Place the player at the starting location.
    PlayerPosition = Board.PlayerStart;

    // Reset every pursuer.
    for (int i = 0; i < pursuers.Count; i++)
    {
        // Move the pursuer back to its initial position.
        pursuers[i].Position = Board.PursuerStarts[i];

        // Set the pursuer's behaviour to match the current game phase.
        pursuers[i].Mode = GlobalPhase == PursuerMode.Hunt ? PursuerMode.Hunt : PursuerMode.Roam;
    }
}
