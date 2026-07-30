using System.Collections.Generic;
using UnityEngine;

namespace ChaosChess.Unity.AIIntegration.Cards
{
    public sealed class AiCardTargetPlanner
    {
        private readonly List<global::PieceEffector> pieceEffectorBuffer = new();

        public bool TryCreatePlan(
            GameObject cardObject,
            global::BoardManager boardManager,
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

            global::CardData cardData = cardObject.GetComponent<global::CardData>();
            global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;
            if (cardData == null || dataSO == null)
            {
                failureStatus = AiCardExecutionStatus.MissingCardData;
                reason = $"Card '{cardObject.name}' has no CardData/DataSO.";
                return false;
            }

            switch (dataSO.Type)
            {
                case global::CardType.Global:
                    plan = new AiCardTargetPlan(
                        cardData,
                        args: null,
                        targetPieces: System.Array.Empty<global::Piece>(),
                        targetPositions: System.Array.Empty<Vector3Int>());
                    return true;

                case global::CardType.Piece:
                    return TryCreatePiecePlan(cardData, boardManager, out plan, out failureStatus, out reason);

                case global::CardType.Tile:
                    return TryCreateTilePlan(cardData, boardManager, out plan, out failureStatus, out reason);

                default:
                    failureStatus = AiCardExecutionStatus.UnsupportedCardType;
                    reason = $"Unsupported card type '{dataSO.Type}'.";
                    return false;
            }
        }

        private bool TryCreatePiecePlan(
            global::CardData cardData,
            global::BoardManager boardManager,
            out AiCardTargetPlan plan,
            out AiCardExecutionStatus failureStatus,
            out string reason)
        {
            plan = null;
            failureStatus = AiCardExecutionStatus.TargetUnavailable;
            reason = string.Empty;

            global::CardDataSO dataSO = cardData.DataSO;
            int requiredCount = Mathf.Max(0, dataSO.RequiredPieceCount);
            var targets = new List<global::Piece>(requiredCount);

            if (requiredCount > 0)
            {
                global::IPieceTargetFilter targetFilter = cardData.GetComponent<global::IPieceTargetFilter>();
                foreach (global::Piece piece in boardManager.GetAllPieces())
                {
                    if (!CanSelectPiece(piece, dataSO, targetFilter))
                        continue;

                    if (HasActivePieceEffector(piece))
                        continue;

                    targets.Add(piece);
                    if (targets.Count == requiredCount)
                        break;
                }

                if (targets.Count != requiredCount)
                {
                    reason = $"Card '{dataSO.CardName}' needs {requiredCount} piece target(s), but only {targets.Count} were available.";
                    return false;
                }
            }

            var args = new global::CardEffectArgs
            {
                Targets = targets,
                LimitTurn = dataSO.PieceLimitTurn
            };

            plan = new AiCardTargetPlan(
                cardData,
                args,
                targets,
                targetPositions: System.Array.Empty<Vector3Int>());
            return true;
        }

        private bool TryCreateTilePlan(
            global::CardData cardData,
            global::BoardManager boardManager,
            out AiCardTargetPlan plan,
            out AiCardExecutionStatus failureStatus,
            out string reason)
        {
            plan = null;
            failureStatus = AiCardExecutionStatus.TargetUnavailable;
            reason = string.Empty;

            global::CardDataSO dataSO = cardData.DataSO;
            int requiredCount = Mathf.Max(0, dataSO.TileCount);
            var targets = new List<Vector3Int>(requiredCount);
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

            if (targets.Count != requiredCount)
            {
                reason = $"Card '{dataSO.CardName}' needs {requiredCount} tile target(s), but only {targets.Count} were available.";
                return false;
            }

            var args = new global::CardEffectArgs
            {
                TargetPos = targets,
                LimitTurn = dataSO.MaintainTurn
            };

            plan = new AiCardTargetPlan(
                cardData,
                args,
                targetPieces: System.Array.Empty<global::Piece>(),
                targetPositions: targets);
            return true;
        }

        private bool CanSelectPiece(
            global::Piece piece,
            global::CardDataSO dataSO,
            global::IPieceTargetFilter targetFilter)
        {
            if (piece == null || dataSO == null)
                return false;

            if ((piece.Type & dataSO.PieceType) == 0)
                return false;

            if (piece.Color != dataSO.PieceTargetColor)
                return false;

            return targetFilter == null || targetFilter.CanSelectPiece(piece);
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
    }
}
