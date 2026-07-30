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
        private readonly bool isInCheck;

        public FairyStockfishSnapshotEngine(
            string fen,
            UciAnalysisSnapshot snapshot,
            bool isInCheck)
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
        }

        public IReadOnlyList<MoveCandidate> GetTopMoves(BoardState boardState, int variationCount)
        {
            EnsureMatchingBoard(boardState);

            if (variationCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(variationCount), variationCount, "Variation count must be positive.");

            int count = Math.Min(variationCount, snapshot.Moves.Count);
            var result = new List<MoveCandidate>(count);

            for (int i = 0; i < count; i++)
                result.Add(snapshot.Moves[i].ToMoveCandidate());

            return result.AsReadOnly();
        }

        public PositionEvaluation EvaluatePosition(BoardState boardState, int depth)
        {
            EnsureMatchingBoard(boardState);

            if (depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "Evaluation depth must be positive.");

            return snapshot.ToPositionEvaluation();
        }

        public bool IsInCheck(BoardState boardState)
        {
            EnsureMatchingBoard(boardState);
            return isInCheck;
        }

        private void EnsureMatchingBoard(BoardState boardState)
        {
            if (boardState == null)
                throw new ArgumentNullException(nameof(boardState));

            string requestedFen = FenParser.Serialize(boardState);
            if (!string.Equals(requestedFen, canonicalFen, StringComparison.Ordinal))
                throw new InvalidOperationException("Analysis snapshot does not match the requested board state.");
        }
    }
}
