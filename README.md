# Maze Mind

Maze Mind is a standalone .NET 8 Windows desktop maze-chase artefact. It demonstrates classical AI through a native Windows Forms interface and a pure C# simulation. It uses no Unity, game engine, copied art, external package, or third-party runtime dependency.

## Structure

- `MazeMind.Core/Domain`: coordinates, validated ASCII maze, portals, score, lives, rounds, modes and authoritative step rules.
- `MazeMind.Core/Search`: selectable A*/Dijkstra route search and Minimax with alpha-beta pruning.
- `MazeMind.Core/Brains`: Collect/Evade player policy and four pursuer roles.
- `MazeMind.Core/Telemetry`: separate player/pursuer measurements and CSV export.
- `MazeMind.Desktop`: native Windows Forms host and procedurally drawn game window.
- `Verification`: framework-free checks against the same core library.

## Algorithms

A* and Dijkstra both search legal cardinal and tunnel edges. Dijkstra uses travelled cost only. A* adds a portal-aware admissible estimate. The selected algorithm controls real routes rather than running only for metrics.

The player uses an explicit **Collect** or **Evade** intention. It evades when a dangerous pursuer is within four route steps; otherwise it routes to the nearest pellet. The pursuer roles are **Spear** (direct pressure), **Seer** (ahead-of-player ambush), **Keeper** (area cutoff), and **Rover** (seeded wandering). Spear and Seer use depth-limited Minimax with alpha-beta pruning while hunting.

Each pursuer owns exactly one mode: Home, Roam, Hunt, Vulnerable, or Returning. Reinforcement learning is not claimed or implemented.

## Rules and tick order

A normal pellet scores 10, a power pellet scores 50, and a vulnerable pursuer scores 200. The player starts with three lives. Clearing the maze advances the round and restores pellets.

Every fixed step follows this order:

1. Decrement timers and update the global mode.
2. Validate and apply one-cell player/pursuer moves.
3. Consume a pellet.
4. Activate vulnerability when a power pellet was consumed.
5. Detect same-cell and edge-swap collisions, with dangerous contact taking priority.
6. Mark eaten pursuers Returning and recover them at home.
7. Reset a lost life or completed round.

## Build and run

The repository pins .NET SDK `8.0.423` in `global.json`.

```powershell
dotnet build MazeMind.sln -c Release
dotnet run --project MazeMind.Desktop
```

The application opens a resizable native Windows window. No asset download or scene setup is required.

## Controls

- **Space**: pause/resume.
- **R**: restart the match.
- **Tab**: change actual routing between A* and Dijkstra.
- **E**: export telemetry to `%LOCALAPPDATA%\MazeMind\mazemind-telemetry.csv`.

The HUD shows score, lives, round, intention, global mode, selected algorithm, decision time and explored-cell counts.

## Verification

Run all framework-free behavior checks:

```powershell
dotnet run --project Verification -- --check
```

Checks cover board validation, bidirectional portals, equal A*/Dijkstra optimal cost, evasion, pellet/power scoring, vulnerable pursuer scoring, life loss/game over, round reset, four legal role choices, alpha-beta legality, and deterministic seeded movement.

## Student-level limitations

This is deliberately small: one independently authored maze, basic generated shapes, a list-based search frontier, fixed Minimax depth, keyboard host controls, no audio, no saved matches, and no reinforcement learning. The list frontier is clear and adequate for this maze; a priority queue would be the next step for large boards.

## BSc objective mapping

- **A* and Dijkstra:** selectable route search with explored-cell evidence.
- **Minimax:** alternating player/pursuer choices with alpha-beta pruning.
- **Agent behavior:** explicit player intention, singular pursuer modes and four roles.
- **Grid navigation:** validated dense grid with cardinal and portal edges.
- **Game state:** deterministic score, lives, timers, collision and round rules.
- **Evaluation:** split decision timings and explored counts with CSV export.
- **Testing:** standalone checks compile against the production core library.
