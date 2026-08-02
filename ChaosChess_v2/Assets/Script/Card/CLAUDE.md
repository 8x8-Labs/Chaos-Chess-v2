# Card & Effector System (`Assets/Script/Card/`)

Cards are the primary mechanism for rewriting chess rules mid-match. The system has three cooperating layers:

1. **`CardDataSO`** — ScriptableObject holding a card's static configuration (name, description, targeting rules, durations like `PieceLimitTurn`/`MaintainTurn`/`LimitTurn`, blocked-tile masks, etc.). Lives in `Card/SO/`.
2. **Card behaviour classes** — implement `ICard` (`Execute(CardEffectArgs args)`) via `IPieceCard`/`ITileCard`, and inherit `CardData` (`Card/CardData.cs`) to get `CreatePieceEffector<T>`/`CreateTileEffector<T>`/`CreateGlobalEffector<T>` factory helpers that read their config straight from `DataSO`. New skills go in `Card/Skills/`; `TestSkill.cs`/`TestCardUI.cs` are references. Selection of targets is delegated to `PieceSelector`/`TileSelector` (`Card/Selector/`).
3. **Effectors** (`Card/Effector/`) — the runtime objects a card creates to actually apply a lasting effect. **Read `Card/Effector/Effector.md` before writing or modifying any effect** — it documents the full contract:
   - `PieceEffector` — attaches to a `Piece` GameObject; hooks `OnPieceMove`/`OnPieceCapture`/`OnPieceCaptured`
   - `TileEffector` — attaches to a board tile; hooks `OnPieceEnter`/`OnPieceExit`
   - `GlobalEffector` — watches all pieces of a given `PieceType`/color; hooks `OnPieceAct`
   - `Apply()`/`Revert()` are **sealed**; subclasses override `OnApply()`/`OnRevert()` only. Duration/expiry is handled automatically via `GameManager.OnTurnChanged` — do not wire turn-counting yourself.
