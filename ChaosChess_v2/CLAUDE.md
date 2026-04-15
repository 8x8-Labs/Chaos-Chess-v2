# CLAUDE.md

## ⚠️ Constraints

- **Edit only** `.cs`, ScriptableObject, and config files
- **Never enter Play Mode** (including setting `EditorApplication.isPlaying`)
- Verify behavior via code review / static analysis only
- **Never access `.meta` files** — do not read, edit, or reference them
- **Scope file access strictly** — if a file path is provided, read only that file and directly related files; do not browse or inspect other files without explicit request

---

## Project Overview

Unity-based chess+RPG hybrid. Players use cards to modify chess rules. Integrates Fairy Stockfish AI via UCI protocol.

- **Packages:** DOTween, TextMesh Pro, URP, Input System
- **AI Engine:** `StreamingAssets/fairy-stockfish.exe`
- **Main Scene:** `Assets/Scenes/MainScene.unity`

---

## Core Architecture

| Script | Role |
|---|---|
| `GameManager.cs` | Singleton. Orchestrates the turn loop |
| `BoardManager.cs` | 8×8 board array, FEN/UCI conversion, move execution |
| `FairyStockfishBridge.cs` | UCI protocol bridge (depth 12, 2000ms) |
| `InputManager.cs` | Mouse-to-grid input |
| `BoardUI.cs` / `UIManager.cs` | Tile highlights, promotion UI |

**Pieces:** `Piece.cs` base class + Pawn/Knight/Bishop/Rook/Queen/King

**Card System (`Assets/Script/Card/`):**
- Implement `ICard` → `IPieceCard` / `ITileCard`
- Configure cards via `CardDataSO` ScriptableObjects
- New skills go in `skills/`; see `TestSkill.cs` as reference

---

## Turn Flow

```
Click → SelectGrid() → validate/execute move → NextTurn()
→ update FEN → query Stockfish → EvaluateGameState()
→ RequestAIMove() → ApplyUCIMove() → NextTurn()
```

---

## Conventions

- `GameManager`, `FairyStockfishBridge` — Singleton
- `BoardManager` — not a Singleton; access via `GameManager`
- Board state: **FEN strings** | AI interface: **UCI notation**
- All animations: **DOTween**
- Async callback marshalling: `UnityMainThreadDispatcher.cs`