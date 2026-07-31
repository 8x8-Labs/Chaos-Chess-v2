using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChaosChess.Unity.AIIntegration.Cards
{
    public sealed class AiCardTargetPlan
    {
        public AiCardTargetPlan(
            global::CardData cardData,
            global::CardEffectArgs args,
            IReadOnlyList<global::Piece> targetPieces,
            IReadOnlyList<Vector3Int> targetPositions)
        {
            CardData = cardData != null
                ? cardData
                : throw new ArgumentNullException(nameof(cardData));
            Args = args;
            TargetPieces = targetPieces ?? Array.Empty<global::Piece>();
            TargetPositions = targetPositions ?? Array.Empty<Vector3Int>();
        }

        public global::CardData CardData { get; }
        public global::CardEffectArgs Args { get; }
        public IReadOnlyList<global::Piece> TargetPieces { get; }
        public IReadOnlyList<Vector3Int> TargetPositions { get; }
    }
}
