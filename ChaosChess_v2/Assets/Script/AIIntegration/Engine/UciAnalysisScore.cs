using System;
using ChaosChess.AI.Domain;
using AiPieceColor = ChaosChess.AI.Domain.PieceColor;

namespace ChaosChess.Unity.AIIntegration.Engine
{
    public enum UciScoreBound
    {
        Exact,
        Lower,
        Upper
    }

    public sealed class UciAnalysisScore
    {
        public UciAnalysisScore(int? scoreCentipawns, int? mateIn, UciScoreBound bound)
        {
            if (scoreCentipawns.HasValue == mateIn.HasValue)
                throw new ArgumentException("Exactly one of centipawn score or mate distance must be supplied.");

            if (mateIn == 0)
                throw new ArgumentOutOfRangeException(nameof(mateIn), mateIn, "Predicted mate distance cannot be zero.");

            ScoreCentipawns = scoreCentipawns;
            MateIn = mateIn;
            Bound = bound;
        }

        public int? ScoreCentipawns { get; }
        public int? MateIn { get; }
        public UciScoreBound Bound { get; }

        public MoveCandidate ToMoveCandidate(string uciMove)
        {
            return new MoveCandidate(uciMove, ScoreCentipawns, MateIn);
        }

        public PositionEvaluation ToPositionEvaluation(AiPieceColor perspective)
        {
            return new PositionEvaluation(perspective, ScoreCentipawns, MateIn);
        }
    }
}
