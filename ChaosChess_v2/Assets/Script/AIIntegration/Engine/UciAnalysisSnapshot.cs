using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChaosChess.AI.Domain;
using AiPieceColor = ChaosChess.AI.Domain.PieceColor;

namespace ChaosChess.Unity.AIIntegration.Engine
{
    public sealed class UciAnalysisSnapshot
    {
        private readonly ReadOnlyCollection<UciMoveAnalysis> moves;
        private readonly ReadOnlyCollection<string> warnings;

        public UciAnalysisSnapshot(
            AiPieceColor perspective,
            int requestedVariationCount,
            int selectedDepth,
            IEnumerable<UciMoveAnalysis> moves,
            string bestMove,
            IEnumerable<string> warnings)
        {
            if (perspective != AiPieceColor.White && perspective != AiPieceColor.Black)
                throw new ArgumentOutOfRangeException(nameof(perspective), perspective, "Unknown piece color.");

            if (requestedVariationCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(requestedVariationCount), requestedVariationCount, "Requested variation count must be positive.");

            if (selectedDepth < 0)
                throw new ArgumentOutOfRangeException(nameof(selectedDepth), selectedDepth, "Selected depth cannot be negative.");

            if (moves == null)
                throw new ArgumentNullException(nameof(moves));

            if (warnings == null)
                throw new ArgumentNullException(nameof(warnings));

            Perspective = perspective;
            RequestedVariationCount = requestedVariationCount;
            SelectedDepth = selectedDepth;
            this.moves = new List<UciMoveAnalysis>(moves).AsReadOnly();
            BestMove = string.IsNullOrWhiteSpace(bestMove) ? null : bestMove;
            this.warnings = new List<string>(warnings).AsReadOnly();
        }

        public AiPieceColor Perspective { get; }
        public int RequestedVariationCount { get; }
        public int SelectedDepth { get; }
        public IReadOnlyList<UciMoveAnalysis> Moves => moves;
        public string BestMove { get; }
        public IReadOnlyList<string> Warnings => warnings;
        public bool HasMoves => moves.Count > 0;

        public IReadOnlyList<MoveCandidate> ToMoveCandidates()
        {
            var candidates = new List<MoveCandidate>(moves.Count);
            foreach (UciMoveAnalysis move in moves)
                candidates.Add(move.ToMoveCandidate());

            return candidates.AsReadOnly();
        }

        public PositionEvaluation ToPositionEvaluation()
        {
            if (moves.Count == 0)
                throw new InvalidOperationException("Cannot create a position evaluation from an empty analysis snapshot.");

            return moves[0].Score.ToPositionEvaluation(Perspective);
        }
    }
}
