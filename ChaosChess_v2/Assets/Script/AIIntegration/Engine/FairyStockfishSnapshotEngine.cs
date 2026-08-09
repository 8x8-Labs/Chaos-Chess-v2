using System;
using System.Collections.Generic;
using ChaosChess.AI.Abstractions;
using ChaosChess.AI.Domain;
using ChaosChess.AI.Fen;

namespace ChaosChess.Unity.AIIntegration.Engine
{
    public sealed class FairyStockfishSnapshotEngine : IChessEngine
    {
        private readonly string canonicalFen;
        private readonly UciAnalysisSnapshot snapshot;
        private readonly string opponentReplyFen;
        private readonly UciAnalysisSnapshot opponentReplySnapshot;
        private readonly bool isInCheck;

        public FairyStockfishSnapshotEngine(
            string fen,
            UciAnalysisSnapshot snapshot,
            bool isInCheck)
            : this(fen, snapshot, isInCheck, null, null)
        {
        }

        public FairyStockfishSnapshotEngine(
            string fen,
            UciAnalysisSnapshot snapshot,
            bool isInCheck,
            string opponentReplyFen,
            UciAnalysisSnapshot opponentReplySnapshot)
        {
            if (string.IsNullOrWhiteSpace(fen))
                throw new ArgumentException("FEN cannot be empty.", nameof(fen));

            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            if (!snapshot.HasMoves)
                throw new ArgumentException("Analysis snapshot must contain at least one move.", nameof(snapshot));

            BoardState boardState = FenParser.Parse(fen);
            canonicalFen = FenParser.Serialize(boardState);
            this.snapshot = snapshot;
            this.isInCheck = isInCheck;

            if (!string.IsNullOrWhiteSpace(opponentReplyFen) && opponentReplySnapshot != null)
            {
                BoardState opponentBoardState = FenParser.Parse(opponentReplyFen);
                this.opponentReplyFen = FenParser.Serialize(opponentBoardState);
                this.opponentReplySnapshot = opponentReplySnapshot;
            }
        }

        public IReadOnlyList<MoveCandidate> GetTopMoves(BoardState boardState, int variationCount)
        {
            UciAnalysisSnapshot resolvedSnapshot = ResolveSnapshot(boardState);

            if (variationCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(variationCount), variationCount, "Variation count must be positive.");

            int count = Math.Min(variationCount, resolvedSnapshot.Moves.Count);
            var result = new List<MoveCandidate>(count);

            for (int i = 0; i < count; i++)
                result.Add(resolvedSnapshot.Moves[i].ToMoveCandidate());

            return result.AsReadOnly();
        }

        public PositionEvaluation EvaluatePosition(BoardState boardState, int depth)
        {
            UciAnalysisSnapshot resolvedSnapshot = ResolveSnapshot(boardState);

            if (depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "Evaluation depth must be positive.");

            return resolvedSnapshot.ToPositionEvaluation();
        }

        public bool IsInCheck(BoardState boardState)
        {
            ResolveSnapshot(boardState);
            return isInCheck;
        }

        private UciAnalysisSnapshot ResolveSnapshot(BoardState boardState)
        {
            if (boardState == null)
                throw new ArgumentNullException(nameof(boardState));

            string requestedFen = FenParser.Serialize(boardState);
            if (!string.Equals(requestedFen, canonicalFen, StringComparison.Ordinal))
            {
                if (opponentReplySnapshot != null &&
                    string.Equals(requestedFen, opponentReplyFen, StringComparison.Ordinal))
                    return opponentReplySnapshot;

                throw new InvalidOperationException("Analysis snapshot does not match the requested board state.");
            }

            return snapshot;
        }
    }
}
