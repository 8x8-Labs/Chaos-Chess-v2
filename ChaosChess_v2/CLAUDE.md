# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ⚠️ Important Constraints for Claude

- **코드 수정만 수행할 것:** 이 저장소에서 Claude는 `.cs` 소스 파일, ScriptableObject 데이터, 설정 파일 등 **코드 및 에셋 파일의 편집에만** 집중해야 합니다.
- **Unity Play 모드 진입 금지:** Unity Editor의 Play 모드(▶ 버튼)를 실행하거나, `EditorApplication.isPlaying`을 `true`로 설정하거나, Play 모드 진입을 유발하는 어떠한 에디터 스크립트도 실행해서는 안 됩니다.
- **에디터 자동화 제한:** `ExecuteMenuItem`, `EditorApplication.EnterPlaymode` 등 Unity Editor 자동화 API를 통해 씬을 실행하는 행위는 금지됩니다.
- **검증은 코드 리뷰로:** 동작 확인이 필요한 경우 코드 정적 분석, 로직 리뷰, 또는 단위 테스트 작성으로 대체하고, 실제 런타임 실행은 사람이 직접 수행합니다.

---

## Project Overview

Chaos Chess v2 is a Unity-based hybrid chess+RPG game where players use cards to bend chess rules. It combines a full chess engine (Fairy Stockfish via UCI protocol) with a card/skill system that can modify pieces and tiles mid-game.

## Build & Development

This is a Unity project (no CLI build commands). Open `ChaosChess_v2.sln` or import the folder into Unity Editor. The main scene is `Assets/Scenes/MainScene.unity`.

- **Unity packages used:** DOTween (animations), TextMesh Pro (UI text), Universal Render Pipeline, Input System
- **AI engine:** `StreamingAssets/fairy-stockfish.exe` — must be present for AI moves to work
- **Test scenes:** `CardTestScene` (card system), `AITest` (AI integration)

## Architecture

### Core Systems

**`Assets/Script/ChessSystem/`** — Game orchestration layer:
- `GameManager.cs` — Singleton. Owns the turn loop: player input → move validation → AI request → repeat. Key methods: `SelectGrid()`, `NextTurn()`, `RequestAIMove()`, `EvaluateGameState()`
- `BoardManager.cs` — Owns the 8×8 `Piece[,]` board array. Handles FEN parsing/generation, move execution, castling, en passant, and pawn promotion. Converts between UCI notation (e.g. `"e2e4"`), `Vector3Int` grid coords, and world positions
- `InputManager.cs` — Mouse-to-grid selection via Tilemap raycasting
- `BoardUI.cs` — Tile highlight animations (DOTween bounce)
- `UIManager.cs` — Pawn promotion menu

**`Assets/Script/Pieces/`** — `Piece.cs` base class + `Pawn`, `Knight`, `Bishop`, `Rook`, `Queen`, `King`. Each piece tracks `Pos` (grid), `Color`, and `CanMovePos` (legal destinations). Movement visuals use DOTween.

**`FairyStockfishBridge.cs`** — UCI protocol bridge. Windows: spawns `fairy-stockfish.exe` as an external process. Android: JNI. Async callbacks return best move strings. Current settings: depth 12, movetime 2000ms.

### Card System

**`Assets/Script/Card/`** — Extensible card/skill framework:
- Cards implement `ICard` (Execute interface) and one of `IPieceCard` / `ITileCard` from `ISkillCards.cs`
- Three card types (`CardType` enum): `Piece`, `Tile`, `Global`
- Cards are configured via `CardDataSO` ScriptableObjects with name, image, description, targeting, and duration
- `PieceSelector` / `TileSelector` — choose which pieces/tiles a card targets
- `CardEffectArgs` — passed into Execute: contains target pieces, positions, turn limits
- New skills go in `Assets/Script/Card/skills/`; reference `TestSkill.cs` and `TestTileSkill.cs` as examples

### Shared Types

**`Assets/Script/Enums.cs`**:
```csharp
PieceType (Flags): Pawn, Knight, Bishop, Rook, King, Queen
PieceColor: White, Black
ApplyType: White, Black, All   // card targeting scope
CardType: Piece, Tile, Global
AdditionalDescription: Break, Revive
```

### Turn Flow

```
Player clicks tile
  → InputManager → GameManager.SelectGrid()
  → BoardManager validates / executes move
  → Promotion UI if pawn reaches back rank
  → NextTurn(): update FEN, query legal moves from Stockfish
  → EvaluateGameState(): check / checkmate / draw
  → RequestAIMove() async → ApplyUCIMove() → NextTurn()
```

### UI System

**`Assets/Script/UI/`** — Reusable menu/screen navigation framework:

- `ButtonParent` — Base class. Sets `isMainParent` flag; auto-calls `EnableParent()` or `DisableParent()` on Start.
- `ButtonCanvas` — Full-screen canvas unit. Fades in/out via `CanvasGroup`. On enable, fires staggered `BasicUIAnimation` slide-ins on registered buttons. Use `MainCanvas = true` for the default visible canvas.
- `ButtonPanel` — Lightweight overlay (popup). Fades via `CanvasGroup.DOFade`; blocks raycasts when visible.
- `UIButton` — Extends Unity's `Button`. Behaviour is driven by `ButtonType` enum (see below). Auto-resolves parent `ButtonCanvas` / `ButtonPanel` if fields are unset.
- `BackgroundBG` — Adjusts a shader material's `_TileOffset` to maintain aspect-ratio-correct tiling on resize.

**`ButtonType` enum** (defined in `UIButton.cs`):
```
None, ChangeCanvas, ChangePanel, OpenPopup, ClosePopup,
GoScene, Submit, GameStart, GoMain, Quit
```

**`Assets/Script/UI/Animation/`** — `IUIAnimation` implementations:
- `BasicUIAnimation` — Slides an `Image` in from x=2000 with configurable DOTween ease + `UnityEvent` hooks.
- Scene-specific variants: `ModeUIAnim`, `StartGameUIAnim`, `StartGuideUIAnim`, `StartPracticeUIAnim`.

**`Assets/Script/Editor/UIButtonEditor.cs`** — Custom inspector for `UIButton`. Extends `ButtonEditor`; shows context-sensitive fields per `ButtonType` (e.g. canvas refs for `ChangeCanvas`, scene name for `GoScene`).

## Key Conventions

- **Singleton pattern** used for `GameManager` and `FairyStockfishBridge`; `BoardManager` is not a singleton — access it via `GameManager`
- **ScriptableObjects** are used for card configuration — prefer `CardDataSO` over hardcoded values
- **FEN strings** are the canonical board state representation; `BoardManager` generates and consumes them
- **UCI notation** is the interface between `BoardManager` and `FairyStockfishBridge`
- **DOTween** is used for all animations; import it from Resources/DOTweenSettings if missing
- `UnityMainThreadDispatcher.cs` is used to marshal Stockfish async callbacks back to the Unity main thread