# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ⚠️ Constraints

- **Edit only** `.cs`, ScriptableObject, and config files
- **Never enter Play Mode** (including setting `EditorApplication.isPlaying`)
- Verify behavior via code review / static analysis only — there is no CLI build/test workflow; this is a Unity Editor project (Unity 6000.0.68f1) and changes are validated by the Editor's compiler and by manual play-testing in-Editor (which you cannot do)
- **Never access `.meta` files** — do not read, edit, or reference them
- **Scope file access strictly** — if a file path is provided, read only that file and directly related files; do not browse or inspect other files without explicit request

---

## Project Overview

Unity-based chess + roguelike-deckbuilder hybrid. Players run a multi-floor campaign (`Map`), fighting Fairy Stockfish-controlled opponents in chess matches where cards can rewrite the rules mid-game. Integrates Fairy Stockfish AI via UCI protocol.

- **Packages:** DOTween, TextMesh Pro, URP, Input System
- **AI Engine:** `StreamingAssets/fairy-stockfish.exe`
- **Main Scene:** `Assets/Scenes/MainScene.unity`

---

## Two Layers of Architecture

The codebase has two distinct layers that future changes usually touch independently:

1. **Match layer** (`Assets/Script/ChessSystem/`, `Pieces/`, `Card/`) — a single chess game: board state, turn loop, move legality, AI queries, and card effects applied mid-match.
2. **Meta/run layer** (`Assets/Script/GameCycle/`, `Map/`, `Arena/`, `Buff/`, `Save/`) — the roguelike campaign wrapping matches: floor map navigation, persistent player state, buffs picked between matches, the Arena mini-game, and save/continue.

`GameCycleManager` is the entry point that ties them together (`StartGame` → `PlayerState.InitializeRun` → `MapManager.Init`; `StartPractice` for non-run practice matches).

---

## Match Layer

| Script | Role |
|---|---|
| `GameManager.cs` | Singleton. Orchestrates the turn loop, exposes turn-change events |
| `BoardManager.cs` | 8×8 board array, FEN/UCI conversion, move execution. **Not** a Singleton — access via `GameManager` |
| `FairyStockfishBridge.cs` | Singleton. UCI protocol bridge (depth 12, 2000ms) |
| `InputManager.cs` | Mouse-to-grid input |
| `BoardUI.cs` / `UIManager.cs` | Tile highlights, promotion UI |

**Pieces:** `Piece.cs` base class + Pawn/Knight/Bishop/Rook/Queen/King (plus card-introduced pieces: Amazon, Chancellor, KnightRider, Wall)

### Turn Flow

```
Click → SelectGrid() → validate/execute move → NextTurn()
→ update FEN → query Stockfish → EvaluateGameState()
→ RequestAIMove() → ApplyUCIMove() → NextTurn()
```

`GameManager` exposes `OnTurnChanged` (full turn), `OnHalfTurnChanged` (per side), and `OnPlayerTurnStarted`/`OnPlayerCheckStateChanged` events that effects/UI subscribe to instead of polling.

### Arena Mini-game

`ArenaCard` can launch a sub-match via `ArenaManager` (Singleton): non-arena pieces are hidden, both Kings become temporarily invincible, and the player has a limited number of moves (`MaxPlayerMoveCount = 8`) to capture the opponent's arena pieces before the main match resumes. See the doc comment at the top of `ArenaManager.cs` for the full win/timeout rules.

---

## Card & Effector System (`Assets/Script/Card/`)

Cards are the primary mechanism for rewriting chess rules mid-match. The system has three cooperating layers:

1. **`CardDataSO`** — ScriptableObject holding a card's static configuration (name, description, targeting rules, durations like `PieceLimitTurn`/`MaintainTurn`/`LimitTurn`, blocked-tile masks, etc.). Lives in `Card/SO/`.
2. **Card behaviour classes** — implement `ICard` (`Execute(CardEffectArgs args)`) via `IPieceCard`/`ITileCard`, and inherit `CardData` (`Card/CardData.cs`) to get `CreatePieceEffector<T>`/`CreateTileEffector<T>`/`CreateGlobalEffector<T>` factory helpers that read their config straight from `DataSO`. New skills go in `Card/Skills/`; `TestSkill.cs`/`TestCardUI.cs` are references. Selection of targets is delegated to `PieceSelector`/`TileSelector` (`Card/Selector/`).
3. **Effectors** (`Card/Effector/`) — the runtime objects a card creates to actually apply a lasting effect. **Read `Card/Effector/Effector.md` before writing or modifying any effect** — it documents the full contract:
   - `PieceEffector` — attaches to a `Piece` GameObject; hooks `OnPieceMove`/`OnPieceCapture`/`OnPieceCaptured`
   - `TileEffector` — attaches to a board tile; hooks `OnPieceEnter`/`OnPieceExit`
   - `GlobalEffector` — watches all pieces of a given `PieceType`/color; hooks `OnPieceAct`
   - `Apply()`/`Revert()` are **sealed**; subclasses override `OnApply()`/`OnRevert()` only. Duration/expiry is handled automatically via `GameManager.OnTurnChanged` — do not wire turn-counting yourself.

---

## Meta/Run Layer

| Script | Role |
|---|---|
| `GameCycleManager.cs` | Singleton, persists across scenes (`DontDestroyOnLoad`). Owns `GameMode` (Run vs Practice), starts/continues runs |
| `PlayerState.cs` | Singleton. Persistent per-run data: card pool, picked buffs, max card count, card-draw interval, win/draw/loss record |
| `MapManager.cs` / `Map.cs` / `MapUI.cs` | Floor-graph map (`totalFloors`, nodes-per-floor range), node selection/navigation, FEN sets per floor/boss |
| `ArenaManager.cs` | Singleton. Runs the in-match Arena mini-game (see above) |
| `Buff/Core` (`BuffSO`, `BuffPick`, `IBuffRuntime`, `BuffSide`) + `Buff/Buffs/*` | Run-scoped passive modifiers picked between matches (e.g. `EloRatingBuff`, `PawnReinforcementBuff`) |
| `SaveManager.cs` + `Save/*Saver.cs` + `RunSaveData`/`CollectionSaveData` | JSON-based save/continue. `ContinueRun()` caches loaded data, then `ApplyLoadedData()` restores it once `OnSavedSceneLoaded` fires — restoration is deliberately deferred past scene load |

---

## Conventions

- `GameManager`, `FairyStockfishBridge`, `GameCycleManager`, `PlayerState`, `ArenaManager`, `MapManager` — Singleton (`Instance`, often `DontDestroyOnLoad`)
- `BoardManager` — not a Singleton; access via `GameManager`
- Board state: **FEN strings** | AI interface: **UCI notation**
- All animations: **DOTween**
- Async callback marshalling: `UnityMainThreadDispatcher.cs`
- Card descriptions (`CardDataSO.CardDescription`) use TMP rich-text `<color=#RRGGBB>` tags and are stored in `.asset` YAML as `\uXXXX`-escaped Korean strings
