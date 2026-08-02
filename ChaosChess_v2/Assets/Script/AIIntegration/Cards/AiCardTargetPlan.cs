using System;
using System.Collections.Generic;
using ChaosChess.AI.Domain;
using UnityEngine;

namespace ChaosChess.Unity.AIIntegration.Cards
{
    public sealed class AiCardTargetPlan
    {
        public AiCardTargetPlan(
            global::CardData cardData,
            CardUsePlan usePlan,
            global::CardEffectArgs args,
            IReadOnlyList<global::Piece> targetPieces,
            IReadOnlyList<Vector3Int> targetPositions)
        {
            CardData = cardData != null
                ? cardData
                : throw new ArgumentNullException(nameof(cardData));
            UsePlan = usePlan ?? throw new ArgumentNullException(nameof(usePlan));
            Args = args;
            TargetPieces = targetPieces ?? Array.Empty<global::Piece>();
            TargetPositions = targetPositions ?? Array.Empty<Vector3Int>();
        }

        public global::CardData CardData { get; }
        public CardUsePlan UsePlan { get; }
        public global::CardEffectArgs Args { get; }
        public IReadOnlyList<global::Piece> TargetPieces { get; }
        public IReadOnlyList<Vector3Int> TargetPositions { get; }
    }
}
