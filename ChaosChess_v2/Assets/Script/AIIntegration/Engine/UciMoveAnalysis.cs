using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChaosChess.AI.Domain;

namespace ChaosChess.Unity.AIIntegration.Engine
{
    public sealed class UciMoveAnalysis
    {
        private readonly ReadOnlyCollection<string> principalVariation;

        public UciMoveAnalysis(
            int variationIndex,
            int depth,
            UciAnalysisScore score,
            IEnumerable<string> principalVariation)
        {
            if (variationIndex <= 0)
                throw new ArgumentOutOfRangeException(nameof(variationIndex), variationIndex, "Variation index must be positive.");

            if (depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "Depth must be positive.");

            if (score == null)
                throw new ArgumentNullException(nameof(score));

            if (principalVariation == null)
                throw new ArgumentNullException(nameof(principalVariation));

            var copy = new List<string>();
            foreach (string move in principalVariation)
            {
                if (!UciMultiPvParser.IsValidUciMove(move))
                    throw new ArgumentException("Principal variation contains an invalid UCI move.", nameof(principalVariation));

                copy.Add(move);
            }

            if (copy.Count == 0)
                throw new ArgumentException("Principal variation must contain at least one move.", nameof(principalVariation));

            VariationIndex = variationIndex;
            Depth = depth;
            Score = score;
            this.principalVariation = copy.AsReadOnly();
        }

        public int VariationIndex { get; }
        public int Depth { get; }
        public UciAnalysisScore Score { get; }
        public IReadOnlyList<string> PrincipalVariation => principalVariation;
        public string UciMove => principalVariation[0];

        public MoveCandidate ToMoveCandidate()
        {
            return Score.ToMoveCandidate(UciMove);
        }
    }
}
