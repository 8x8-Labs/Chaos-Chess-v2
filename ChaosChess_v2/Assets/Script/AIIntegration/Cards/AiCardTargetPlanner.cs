using System.Collections.Generic;
using ChaosChess.AI.Domain;
using UnityEngine;
using AiPieceColor = ChaosChess.AI.Domain.PieceColor;
using AiPieceKind = ChaosChess.AI.Domain.PieceKind;

namespace ChaosChess.Unity.AIIntegration.Cards
{
    public sealed class AiCardTargetPlanner
    {
        private readonly List<global::PieceEffector> pieceEffectorBuffer = new();
        private readonly DefaultCardPlanningCatalog planningCatalog;
        private readonly CardUsePlanValidator planValidator;

        public AiCardTargetPlanner()
            : this(new DefaultCardPlanningCatalog())
        {
        }

        public AiCardTargetPlanner(DefaultCardPlanningCatalog planningCatalog)
        {
            this.planningCatalog = planningCatalog ?? throw new System.ArgumentNullException(nameof(planningCatalog));
            planValidator = new CardUsePlanValidator(planningCatalog);
        }

        public bool TryCreatePlan(
            string cardId,
            GameObject cardObject,
            global::BoardManager boardManager,
            GameState gameState,
            AiPieceColor actor,
            out AiCardTargetPlan plan,
            out AiCardExecutionStatus failureStatus,
            out string reason)
        {
            plan = null;
            failureStatus = AiCardExecutionStatus.ExecutionFailed;
            reason = string.Empty;

            if (cardObject == null)
            {
                failureStatus = AiCardExecutionStatus.CardNotInHand;
                reason = "Card object is null.";
                return false;
            }

            if (boardManager == null)
            {
                failureStatus = AiCardExecutionStatus.TargetUnavailable;
                reason = "BoardManager is null.";
                return false;
            }

            if (gameState == null)
            {
                failureStatus = AiCardExecutionStatus.TargetUnavailable;
                reason = "GameState is null.";
                return false;
            }

            global::CardData cardData = cardObject.GetComponent<global::CardData>();
            global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;
            if (cardData == null || dataSO == null)
            {
                failureStatus = AiCardExecutionStatus.MissingCardData;
                reason = $"Card '{cardObject.name}' has no CardData/DataSO.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(cardId) ||
                !string.Equals(cardId, dataSO.AiCardId, System.StringComparison.OrdinalIgnoreCase))
            {
                failureStatus = AiCardExecutionStatus.MissingCardData;
                reason = $"Card '{dataSO.CardName}' has mismatched AI card id '{dataSO.AiCardId}'.";
                return false;
            }

            CardPlanningDefinition definition = planningCatalog.GetDefinition(cardId);
            if (!definition.IsSupported)
            {
                failureStatus = AiCardExecutionStatus.UnsupportedCardType;
                reason = $"Card '{cardId}' is not supported by the AI card planning catalog.";
                return false;
            }

            if (!ValidateUnityCardShape(
                    cardData,
                    definition,
                    out failureStatus,
                    out reason))
            {
                return false;
            }

            if (!TryCreateTargetSelection(
                    cardData,
                    boardManager,
                    actor,
                    definition,
                    out CardTargetSelection targetSelection,
                    out failureStatus,
                    out reason))
            {
                return false;
            }

            var usePlan = new CardUsePlan(cardId, actor, targetSelection);
            CardPlanValidationResult validation = planValidator.Validate(gameState, usePlan);
            if (!validation.IsValid)
            {
                failureStatus = AiCardExecutionStatus.TargetUnavailable;
                reason = $"CardUsePlan validation failed ({validation.Code}): {validation.Reason}";
                return false;
            }

            if (!TryCreateUnityArgs(
                    cardData,
                    boardManager,
                    usePlan,
                    out global::CardEffectArgs args,
                    out IReadOnlyList<global::Piece> targetPieces,
                    out IReadOnlyList<Vector3Int> targetPositions,
                    out reason))
            {
                failureStatus = AiCardExecutionStatus.TargetUnavailable;
                return false;
            }

            plan = new AiCardTargetPlan(cardData, usePlan, args, targetPieces, targetPositions);
            return true;
        }

        public bool TryCreatePlan(
            CardUsePlan usePlan,
            GameObject cardObject,
            global::BoardManager boardManager,
            GameState gameState,
            AiPieceColor actor,
            out AiCardTargetPlan plan,
            out AiCardExecutionStatus failureStatus,
            out string reason)
        {
            plan = null;
            failureStatus = AiCardExecutionStatus.ExecutionFailed;
            reason = string.Empty;

            if (usePlan == null)
            {
                failureStatus = AiCardExecutionStatus.TargetUnavailable;
                reason = "CardUsePlan is null.";
                return false;
            }

            if (cardObject == null)
            {
                failureStatus = AiCardExecutionStatus.CardNotInHand;
                reason = "Card object is null.";
                return false;
            }

            if (boardManager == null)
            {
                failureStatus = AiCardExecutionStatus.TargetUnavailable;
                reason = "BoardManager is null.";
                return false;
            }

            if (gameState == null)
            {
                failureStatus = AiCardExecutionStatus.TargetUnavailable;
                reason = "GameState is null.";
                return false;
            }

            global::CardData cardData = cardObject.GetComponent<global::CardData>();
            global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;
            if (cardData == null || dataSO == null)
            {
                failureStatus = AiCardExecutionStatus.MissingCardData;
                reason = $"Card '{cardObject.name}' has no CardData/DataSO.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(usePlan.CardId) ||
                !string.Equals(usePlan.CardId, dataSO.AiCardId, System.StringComparison.OrdinalIgnoreCase))
            {
                failureStatus = AiCardExecutionStatus.MissingCardData;
                reason = $"Card '{dataSO.CardName}' has mismatched AI card id '{dataSO.AiCardId}'.";
                return false;
            }

            CardPlanningDefinition definition = planningCatalog.GetDefinition(usePlan.CardId);
            if (!definition.IsSupported)
            {
                failureStatus = AiCardExecutionStatus.UnsupportedCardType;
                reason = $"Card '{usePlan.CardId}' is not supported by the AI card planning catalog.";
                return false;
            }

            if (!ValidateUnityCardShape(
                    cardData,
                    definition,
                    out failureStatus,
                    out reason))
            {
                return false;
            }

            CardPlanValidationResult validation = planValidator.Validate(gameState, usePlan);
            if (!validation.IsValid)
            {
                failureStatus = AiCardExecutionStatus.TargetUnavailable;
                reason = $"CardUsePlan validation failed ({validation.Code}): {validation.Reason}";
                return false;
            }

            if (!ValidateUnityTargetSelection(
                    cardData,
                    boardManager,
                    actor,
                    usePlan,
                    definition,
                    out failureStatus,
                    out reason))
            {
                return false;
            }

            if (!TryCreateUnityArgs(
                    cardData,
                    boardManager,
                    usePlan,
                    out global::CardEffectArgs args,
                    out IReadOnlyList<global::Piece> targetPieces,
                    out IReadOnlyList<Vector3Int> targetPositions,
                    out reason))
            {
                failureStatus = AiCardExecutionStatus.TargetUnavailable;
                return false;
            }

            plan = new AiCardTargetPlan(cardData, usePlan, args, targetPieces, targetPositions);
            return true;
        }

        private bool TryCreateTargetSelection(
            global::CardData cardData,
            global::BoardManager boardManager,
            AiPieceColor actor,
            CardPlanningDefinition definition,
            out CardTargetSelection targetSelection,
            out AiCardExecutionStatus failureStatus,
            out string reason)
        {
            targetSelection = null;
            failureStatus = AiCardExecutionStatus.TargetUnavailable;
            reason = string.Empty;

            global::CardDataSO dataSO = cardData.DataSO;
            switch (definition.RequiredTargetKind)
            {
                case CardTargetKind.None:
                    targetSelection = CardTargetSelection.None();
                    return true;

                case CardTargetKind.PieceAtSquare:
                    return TryCreatePieceTargetSelection(
                        cardData,
                        boardManager,
                        actor,
                        definition,
                        out targetSelection,
                        out failureStatus,
                        out reason);

                case CardTargetKind.OrderedPieces:
                    return TryCreateOrderedPieceTargetSelection(
                        cardData,
                        boardManager,
                        actor,
                        definition,
                        out targetSelection,
                        out failureStatus,
                        out reason);

                case CardTargetKind.PieceAndSquare:
                    return TryCreatePieceAndSquareTargetSelection(
                        cardData,
                        boardManager,
                        actor,
                        definition,
                        out targetSelection,
                        out failureStatus,
                        out reason);

                case CardTargetKind.BoardSquare:
                case CardTargetKind.OrderedSquares:
                    return TryCreateTileTargetSelection(
                        cardData,
                        boardManager,
                        definition,
                        out targetSelection,
                        out failureStatus,
                        out reason);

                default:
                    failureStatus = AiCardExecutionStatus.UnsupportedCardType;
                    reason = $"Unsupported card type '{dataSO.Type}'.";
                    return false;
            }
        }

        private static bool ValidateUnityCardShape(
            global::CardData cardData,
            CardPlanningDefinition definition,
            out AiCardExecutionStatus failureStatus,
            out string reason)
        {
            failureStatus = AiCardExecutionStatus.TargetUnavailable;
            reason = string.Empty;

            global::CardDataSO dataSO = cardData.DataSO;
            switch (dataSO.Type)
            {
                case global::CardType.Global:
                    if (definition.RequiredTargetKind != CardTargetKind.None)
                    {
                        failureStatus = AiCardExecutionStatus.UnsupportedCardType;
                        reason = $"Card '{dataSO.CardName}' is global but requires '{definition.RequiredTargetKind}' target.";
                        return false;
                    }

                    return true;

                case global::CardType.Piece:
                    switch (definition.RequiredTargetKind)
                    {
                        case CardTargetKind.None:
                            if (Mathf.Max(0, dataSO.RequiredPieceCount) != 0 ||
                                Mathf.Max(0, dataSO.TileCount) != 0)
                            {
                                reason = $"Card '{dataSO.CardName}' target count differs from AI contract.";
                                return false;
                            }

                            return true;

                        case CardTargetKind.PieceAtSquare:
                        case CardTargetKind.OrderedPieces:
                            if (Mathf.Max(0, dataSO.RequiredPieceCount) != definition.RequiredTargetCount)
                            {
                                reason = $"Card '{dataSO.CardName}' target count differs from AI contract.";
                                return false;
                            }

                            return true;

                        case CardTargetKind.PieceAndSquare:
                            if (Mathf.Max(0, dataSO.RequiredPieceCount) != 1 ||
                                Mathf.Max(0, dataSO.TileCount) != 1)
                            {
                                reason = $"Card '{dataSO.CardName}' target count differs from AI contract.";
                                return false;
                            }

                            return true;

                        default:
                            failureStatus = AiCardExecutionStatus.UnsupportedCardType;
                            reason = $"Card '{dataSO.CardName}' is piece-targeted but requires '{definition.RequiredTargetKind}' target.";
                            return false;
                    }

                case global::CardType.Tile:
                    if (definition.RequiredTargetKind != CardTargetKind.BoardSquare &&
                        definition.RequiredTargetKind != CardTargetKind.OrderedSquares)
                    {
                        failureStatus = AiCardExecutionStatus.UnsupportedCardType;
                        reason = $"Card '{dataSO.CardName}' is tile-targeted but requires '{definition.RequiredTargetKind}' target.";
                        return false;
                    }

                    if (Mathf.Max(0, dataSO.TileCount) != definition.RequiredTargetCount)
                    {
                        reason = $"Card '{dataSO.CardName}' target count differs from AI contract.";
                        return false;
                    }

                    return true;

                default:
                    failureStatus = AiCardExecutionStatus.UnsupportedCardType;
                    reason = $"Unsupported card type '{dataSO.Type}'.";
                    return false;
            }
        }

        private bool ValidateUnityTargetSelection(
            global::CardData cardData,
            global::BoardManager boardManager,
            AiPieceColor actor,
            CardUsePlan usePlan,
            CardPlanningDefinition definition,
            out AiCardExecutionStatus failureStatus,
            out string reason)
        {
            failureStatus = AiCardExecutionStatus.TargetUnavailable;
            reason = string.Empty;

            switch (usePlan.Target.Kind)
            {
                case CardTargetKind.None:
                    return true;

                case CardTargetKind.PieceAtSquare:
                    if (!TryResolvePieceTarget(boardManager, usePlan.Target.Piece, out global::Piece piece, out reason))
                        return false;

                    global::IPieceTargetFilter targetFilter = cardData.GetComponent<global::IPieceTargetFilter>();
                    if (!CanSelectPiece(piece, cardData.DataSO, targetFilter, actor, definition))
                    {
                        reason = $"CardUsePlan piece target {usePlan.Target.Piece.Square} is not selectable by Unity card constraints.";
                        return false;
                    }

                    if (HasActivePieceEffector(piece))
                    {
                        reason = $"CardUsePlan piece target {usePlan.Target.Piece.Square} already has an active effector.";
                        return false;
                    }

                    return true;

                case CardTargetKind.PieceAndSquare:
                    if (!TryResolvePieceTarget(boardManager, usePlan.Target.Piece, out global::Piece pieceAndSquarePiece, out reason))
                        return false;

                    global::IPieceTargetFilter pieceAndSquareFilter = cardData.GetComponent<global::IPieceTargetFilter>();
                    if (!CanSelectPiece(pieceAndSquarePiece, cardData.DataSO, pieceAndSquareFilter, actor, definition))
                    {
                        reason = $"CardUsePlan piece target {usePlan.Target.Piece.Square} is not selectable by Unity card constraints.";
                        return false;
                    }

                    if (HasActivePieceEffector(pieceAndSquarePiece))
                    {
                        reason = $"CardUsePlan piece target {usePlan.Target.Piece.Square} already has an active effector.";
                        return false;
                    }

                    if (usePlan.Target.Squares.Count != 1)
                    {
                        reason = $"CardUsePlan piece-and-square target must contain exactly one square.";
                        return false;
                    }

                    HashSet<Vector3Int> pieceAndSquareOccupiedTiles = GetOccupiedEffectTiles(boardManager);
                    Vector3Int pieceAndSquarePos = ToVector3Int(usePlan.Target.Squares[0]);
                    if (!CanSelectTile(pieceAndSquarePos, cardData.DataSO, boardManager, pieceAndSquareOccupiedTiles))
                    {
                        reason = $"CardUsePlan tile target {usePlan.Target.Squares[0]} is not selectable by Unity card constraints.";
                        return false;
                    }

                    return true;

                case CardTargetKind.OrderedPieces:
                    if (usePlan.Target.Pieces.Count != definition.RequiredTargetCount)
                    {
                        reason = $"CardUsePlan ordered piece target count differs from AI contract.";
                        return false;
                    }

                    var occupiedPieceSquares = new HashSet<Square>();
                    global::IPieceTargetFilter orderedPieceFilter = cardData.GetComponent<global::IPieceTargetFilter>();
                    foreach (PieceTargetSnapshot pieceTarget in usePlan.Target.Pieces)
                    {
                        if (!TryResolvePieceTarget(boardManager, pieceTarget, out global::Piece orderedPiece, out reason))
                            return false;

                        if (!occupiedPieceSquares.Add(pieceTarget.Square))
                        {
                            reason = $"CardUsePlan ordered piece target {pieceTarget.Square} is duplicated.";
                            return false;
                        }

                        if (!CanSelectPiece(orderedPiece, cardData.DataSO, orderedPieceFilter, actor, definition))
                        {
                            reason = $"CardUsePlan piece target {pieceTarget.Square} is not selectable by Unity card constraints.";
                            return false;
                        }

                        if (HasActivePieceEffector(orderedPiece))
                        {
                            reason = $"CardUsePlan piece target {pieceTarget.Square} already has an active effector.";
                            return false;
                        }
                    }

                    return true;

                case CardTargetKind.BoardSquare:
                case CardTargetKind.OrderedSquares:
                    HashSet<Vector3Int> occupiedEffectTiles = GetOccupiedEffectTiles(boardManager);
                    foreach (Square square in usePlan.Target.Squares)
                    {
                        Vector3Int pos = ToVector3Int(square);
                        if (!CanSelectTile(pos, cardData.DataSO, boardManager, occupiedEffectTiles))
                        {
                            reason = $"CardUsePlan tile target {square} is not selectable by Unity card constraints.";
                            return false;
                        }
                    }

                    return true;

                default:
                    failureStatus = AiCardExecutionStatus.UnsupportedCardType;
                    reason = $"Unsupported CardUsePlan target kind '{usePlan.Target.Kind}'.";
                    return false;
            }
        }

        private bool TryCreateOrderedPieceTargetSelection(
            global::CardData cardData,
            global::BoardManager boardManager,
            AiPieceColor actor,
            CardPlanningDefinition definition,
            out CardTargetSelection targetSelection,
            out AiCardExecutionStatus failureStatus,
            out string reason)
        {
            targetSelection = null;
            failureStatus = AiCardExecutionStatus.TargetUnavailable;

            if (!TryCollectPieceTargets(
                    cardData,
                    boardManager,
                    actor,
                    definition,
                    definition.RequiredTargetCount,
                    out List<global::Piece> targets,
                    out reason))
            {
                return false;
            }

            targetSelection = CardTargetSelection.OrderedPieces(ToPieceSnapshots(targets));
            return true;
        }

        private bool TryCreatePieceAndSquareTargetSelection(
            global::CardData cardData,
            global::BoardManager boardManager,
            AiPieceColor actor,
            CardPlanningDefinition definition,
            out CardTargetSelection targetSelection,
            out AiCardExecutionStatus failureStatus,
            out string reason)
        {
            targetSelection = null;
            failureStatus = AiCardExecutionStatus.TargetUnavailable;

            if (!TryCollectPieceTargets(
                    cardData,
                    boardManager,
                    actor,
                    definition,
                    1,
                    out List<global::Piece> pieceTargets,
                    out reason))
            {
                return false;
            }

            if (!TryCollectTileTargets(
                    cardData,
                    boardManager,
                    1,
                    out List<Vector3Int> tileTargets,
                    out reason))
            {
                return false;
            }

            targetSelection = CardTargetSelection.PieceAndSquare(
                ToPieceSnapshot(pieceTargets[0]),
                ToSquare(tileTargets[0]));
            return true;
        }

        private bool TryCreatePieceTargetSelection(
            global::CardData cardData,
            global::BoardManager boardManager,
            AiPieceColor actor,
            CardPlanningDefinition definition,
            out CardTargetSelection targetSelection,
            out AiCardExecutionStatus failureStatus,
            out string reason)
        {
            targetSelection = null;
            failureStatus = AiCardExecutionStatus.TargetUnavailable;
            reason = string.Empty;

            global::CardDataSO dataSO = cardData.DataSO;
            int requiredCount = definition.RequiredTargetCount;
            if (!TryCollectPieceTargets(
                    cardData,
                    boardManager,
                    actor,
                    definition,
                    requiredCount,
                    out List<global::Piece> targets,
                    out reason))
            {
                return false;
            }

            if (targets.Count != 1)
            {
                reason = $"Card '{dataSO.CardName}' requires exactly one piece target for CardUsePlan.";
                return false;
            }

            targetSelection = CardTargetSelection.PieceAtSquare(ToPieceSnapshot(targets[0]));
            return true;
        }

        private bool TryCreateTileTargetSelection(
            global::CardData cardData,
            global::BoardManager boardManager,
            CardPlanningDefinition definition,
            out CardTargetSelection targetSelection,
            out AiCardExecutionStatus failureStatus,
            out string reason)
        {
            targetSelection = null;
            failureStatus = AiCardExecutionStatus.TargetUnavailable;
            reason = string.Empty;

            global::CardDataSO dataSO = cardData.DataSO;
            if (definition.RequiredTargetKind != CardTargetKind.BoardSquare &&
                definition.RequiredTargetKind != CardTargetKind.OrderedSquares)
            {
                failureStatus = AiCardExecutionStatus.UnsupportedCardType;
                reason = $"Card '{dataSO.CardName}' is tile-targeted but requires '{definition.RequiredTargetKind}' target.";
                return false;
            }

            int requiredCount = definition.RequiredTargetCount;
            if (!TryCollectTileTargets(
                    cardData,
                    boardManager,
                    requiredCount,
                    out List<Vector3Int> targets,
                    out reason))
            {
                return false;
            }

            if (definition.RequiredTargetKind == CardTargetKind.BoardSquare)
            {
                targetSelection = CardTargetSelection.BoardSquare(ToSquare(targets[0]));
                return true;
            }

            targetSelection = CardTargetSelection.OrderedSquares(ToSquares(targets));
            return true;
        }

        private bool TryCollectPieceTargets(
            global::CardData cardData,
            global::BoardManager boardManager,
            AiPieceColor actor,
            CardPlanningDefinition definition,
            int requiredCount,
            out List<global::Piece> targets,
            out string reason)
        {
            targets = new List<global::Piece>(requiredCount);
            reason = string.Empty;

            global::CardDataSO dataSO = cardData.DataSO;
            if (Mathf.Max(0, dataSO.RequiredPieceCount) != requiredCount)
            {
                reason = $"Card '{dataSO.CardName}' target count differs from AI contract.";
                return false;
            }

            global::IPieceTargetFilter targetFilter = cardData.GetComponent<global::IPieceTargetFilter>();
            foreach (global::Piece piece in boardManager.GetAllPieces())
            {
                if (!CanSelectPiece(piece, dataSO, targetFilter, actor, definition))
                    continue;

                if (HasActivePieceEffector(piece))
                    continue;

                targets.Add(piece);
                if (targets.Count == requiredCount)
                    break;
            }

            if (targets.Count == requiredCount)
                return true;

            reason = $"Card '{dataSO.CardName}' needs {requiredCount} piece target(s), but only {targets.Count} were available.";
            return false;
        }

        private static bool TryCollectTileTargets(
            global::CardData cardData,
            global::BoardManager boardManager,
            int requiredCount,
            out List<Vector3Int> targets,
            out string reason)
        {
            targets = new List<Vector3Int>(requiredCount);
            reason = string.Empty;

            global::CardDataSO dataSO = cardData.DataSO;
            if (Mathf.Max(0, dataSO.TileCount) != requiredCount)
            {
                reason = $"Card '{dataSO.CardName}' target count differs from AI contract.";
                return false;
            }

            HashSet<Vector3Int> occupiedEffectTiles = GetOccupiedEffectTiles(boardManager);
            for (int y = 0; y < 8 && targets.Count < requiredCount; y++)
            {
                for (int x = 0; x < 8 && targets.Count < requiredCount; x++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    if (!CanSelectTile(pos, dataSO, boardManager, occupiedEffectTiles))
                        continue;

                    targets.Add(pos);
                }
            }

            if (targets.Count == requiredCount)
                return true;

            reason = $"Card '{dataSO.CardName}' needs {requiredCount} tile target(s), but only {targets.Count} were available.";
            return false;
        }

        private static bool TryCreateUnityArgs(
            global::CardData cardData,
            global::BoardManager boardManager,
            CardUsePlan usePlan,
            out global::CardEffectArgs args,
            out IReadOnlyList<global::Piece> targetPieces,
            out IReadOnlyList<Vector3Int> targetPositions,
            out string reason)
        {
            args = null;
            targetPieces = System.Array.Empty<global::Piece>();
            targetPositions = System.Array.Empty<Vector3Int>();
            reason = string.Empty;

            switch (usePlan.Target.Kind)
            {
                case CardTargetKind.None:
                    args = CreateUnityArgs(usePlan);
                    return true;

                case CardTargetKind.PieceAtSquare:
                    if (!TryResolvePieceTarget(boardManager, usePlan.Target.Piece, out global::Piece piece, out reason))
                        return false;

                    var pieces = new List<global::Piece> { piece };
                    args = CreateUnityArgs(usePlan);
                    args.Targets = pieces;
                    args.LimitTurn = cardData.DataSO.PieceLimitTurn;
                    targetPieces = pieces;
                    return true;

                case CardTargetKind.PieceAndSquare:
                    if (!TryResolvePieceTarget(boardManager, usePlan.Target.Piece, out global::Piece pieceAndSquarePiece, out reason))
                        return false;

                    var pieceAndSquarePieces = new List<global::Piece> { pieceAndSquarePiece };
                    var pieceAndSquarePositions = new List<Vector3Int>(usePlan.Target.Squares.Count);
                    foreach (Square square in usePlan.Target.Squares)
                    {
                        pieceAndSquarePositions.Add(ToVector3Int(square));
                    }

                    args = CreateUnityArgs(usePlan);
                    args.Targets = pieceAndSquarePieces;
                    args.TargetPos = pieceAndSquarePositions;
                    args.LimitTurn = cardData.DataSO.PieceLimitTurn;
                    targetPieces = pieceAndSquarePieces;
                    targetPositions = pieceAndSquarePositions;
                    return true;

                case CardTargetKind.OrderedPieces:
                    var orderedPieces = new List<global::Piece>(usePlan.Target.Pieces.Count);
                    foreach (PieceTargetSnapshot pieceTarget in usePlan.Target.Pieces)
                    {
                        if (!TryResolvePieceTarget(boardManager, pieceTarget, out global::Piece orderedPiece, out reason))
                            return false;

                        orderedPieces.Add(orderedPiece);
                    }

                    args = CreateUnityArgs(usePlan);
                    args.Targets = orderedPieces;
                    args.LimitTurn = cardData.DataSO.PieceLimitTurn;
                    targetPieces = orderedPieces;
                    return true;

                case CardTargetKind.BoardSquare:
                case CardTargetKind.OrderedSquares:
                    var positions = new List<Vector3Int>(usePlan.Target.Squares.Count);
                    foreach (Square square in usePlan.Target.Squares)
                    {
                        positions.Add(ToVector3Int(square));
                    }

                    args = CreateUnityArgs(usePlan);
                    args.TargetPos = positions;
                    args.LimitTurn = cardData.DataSO.MaintainTurn;
                    targetPositions = positions;
                    return true;

                default:
                    reason = $"Unsupported CardUsePlan target kind '{usePlan.Target.Kind}'.";
                    return false;
            }
        }

        private static global::CardEffectArgs CreateUnityArgs(CardUsePlan usePlan)
        {
            return new global::CardEffectArgs
            {
                HasCasterColor = true,
                CasterColor = ToUnityColor(usePlan.Actor),
                SuppressAutomaticTurnEnd = true
            };
        }

        private bool CanSelectPiece(
            global::Piece piece,
            global::CardDataSO dataSO,
            global::IPieceTargetFilter targetFilter,
            AiPieceColor actor,
            CardPlanningDefinition definition)
        {
            if (piece == null || dataSO == null)
                return false;

            if ((piece.Type & dataSO.PieceType) == 0)
                return false;

            if (!MatchesTargetOwnerRelation(piece, actor, definition))
                return false;

            return targetFilter == null || targetFilter.CanSelectPiece(piece);
        }

        private static bool MatchesTargetOwnerRelation(
            global::Piece piece,
            AiPieceColor actor,
            CardPlanningDefinition definition)
        {
            AiPieceColor targetColor = ToAiColor(piece.Color);
            string relation = GetRequiredTargetOwnerRelation(definition);

            switch (relation)
            {
                case "Self":
                    return targetColor == actor;
                case "Opponent":
                    return targetColor != actor;
                case "Any":
                    return true;
                default:
                    return false;
            }
        }

        private static string GetRequiredTargetOwnerRelation(CardPlanningDefinition definition)
        {
            if (definition == null)
                return "Self";

            System.Reflection.PropertyInfo property = definition
                .GetType()
                .GetProperty(
                    "RequiredTargetOwnerRelation",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            object value = property != null ? property.GetValue(definition, null) : null;
            return value != null ? value.ToString() : "Self";
        }

        private bool HasActivePieceEffector(global::Piece piece)
        {
            if (piece == null)
                return false;

            if (global::PieceEffector.HasActiveMovementOverride(piece))
                return true;

            pieceEffectorBuffer.Clear();
            try
            {
                piece.GetComponents(pieceEffectorBuffer);
                foreach (global::PieceEffector effector in pieceEffectorBuffer)
                {
                    if (effector != null && !effector.IsSuspended)
                        return true;
                }
            }
            finally
            {
                pieceEffectorBuffer.Clear();
            }

            return false;
        }

        private static HashSet<Vector3Int> GetOccupiedEffectTiles(global::BoardManager boardManager)
        {
            var occupied = new HashSet<Vector3Int>();
            foreach (global::TileEffector effector in boardManager.GetAllTileEffectors())
            {
                if (effector != null)
                    occupied.Add(effector.TilePos);
            }

            return occupied;
        }

        private static bool CanSelectTile(
            Vector3Int pos,
            global::CardDataSO dataSO,
            global::BoardManager boardManager,
            ISet<Vector3Int> occupiedEffectTiles)
        {
            if (!boardManager.IsInside(pos))
                return false;

            if (boardManager.GetPiece(pos) != null)
                return false;

            int index = pos.y * 8 + pos.x;
            if (dataSO.RestrictTiles &&
                (dataSO.BlockedTiles == null ||
                 index < 0 ||
                 index >= dataSO.BlockedTiles.Length ||
                 dataSO.BlockedTiles[index]))
            {
                return false;
            }

            return occupiedEffectTiles == null || !occupiedEffectTiles.Contains(pos);
        }

        private static bool TryResolvePieceTarget(
            global::BoardManager boardManager,
            PieceTargetSnapshot pieceTarget,
            out global::Piece piece,
            out string reason)
        {
            piece = null;
            reason = string.Empty;

            if (pieceTarget == null)
            {
                reason = "CardUsePlan contains no piece target snapshot.";
                return false;
            }

            Vector3Int pos = ToVector3Int(pieceTarget.Square);
            piece = boardManager.GetPiece(pos);
            if (piece == null)
            {
                reason = $"No piece exists at CardUsePlan target {pieceTarget.Square}.";
                return false;
            }

            if (ToAiColor(piece.Color) != pieceTarget.ExpectedColor)
            {
                reason = $"Piece at CardUsePlan target {pieceTarget.Square} has a different color.";
                return false;
            }

            if (ToAiPieceKind(piece.Type) != pieceTarget.ExpectedKind)
            {
                reason = $"Piece at CardUsePlan target {pieceTarget.Square} has a different kind.";
                return false;
            }

            return true;
        }

        private static Square ToSquare(Vector3Int pos)
        {
            return new Square(pos.x, pos.y);
        }

        private static Vector3Int ToVector3Int(Square square)
        {
            return new Vector3Int(square.File, square.Rank, 0);
        }

        private static IEnumerable<Square> ToSquares(IEnumerable<Vector3Int> positions)
        {
            foreach (Vector3Int position in positions)
            {
                yield return ToSquare(position);
            }
        }

        private static IEnumerable<PieceTargetSnapshot> ToPieceSnapshots(IEnumerable<global::Piece> pieces)
        {
            foreach (global::Piece piece in pieces)
            {
                yield return ToPieceSnapshot(piece);
            }
        }

        private static PieceTargetSnapshot ToPieceSnapshot(global::Piece piece)
        {
            AiPieceKind targetKind = ToAiPieceKind(piece.Type);
            return new PieceTargetSnapshot(
                ToSquare(piece.Pos),
                ToAiColor(piece.Color),
                targetKind,
                piece.IsPromotioned,
                piece.IsPromotioned ? (Square?)ToSquare(piece.StartPos) : null);
        }

        private static AiPieceColor ToAiColor(global::PieceColor color)
        {
            return color == global::PieceColor.White
                ? AiPieceColor.White
                : AiPieceColor.Black;
        }

        private static global::PieceColor ToUnityColor(AiPieceColor color)
        {
            return color == AiPieceColor.White
                ? global::PieceColor.White
                : global::PieceColor.Black;
        }

        private static AiPieceKind ToAiPieceKind(global::PieceType type)
        {
            switch (type)
            {
                case global::PieceType.Pawn:
                    return AiPieceKind.Pawn;
                case global::PieceType.Knight:
                    return AiPieceKind.Knight;
                case global::PieceType.Bishop:
                    return AiPieceKind.Bishop;
                case global::PieceType.Rook:
                    return AiPieceKind.Rook;
                case global::PieceType.Queen:
                    return AiPieceKind.Queen;
                case global::PieceType.King:
                    return AiPieceKind.King;
                case global::PieceType.Wall:
                    return AiPieceKind.Wall;
                case global::PieceType.Amazon:
                    return AiPieceKind.Amazon;
                case global::PieceType.Chancellor:
                    return AiPieceKind.Chancellor;
                case global::PieceType.KnightRider:
                    return AiPieceKind.KnightRider;
                default:
                    return AiPieceKind.Unknown;
            }
        }
    }
}
