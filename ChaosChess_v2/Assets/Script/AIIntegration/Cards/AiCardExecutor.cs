using System;
using ChaosChess.AI.Decision;
using ChaosChess.AI.Domain;
using UnityEngine;
using AiPieceColor = ChaosChess.AI.Domain.PieceColor;

namespace ChaosChess.Unity.AIIntegration.Cards
{
    public sealed class AiCardExecutor
    {
        private readonly AiCardTargetPlanner targetPlanner;

        public AiCardExecutor()
            : this(new AiCardTargetPlanner())
        {
        }

        public AiCardExecutor(AiCardTargetPlanner targetPlanner)
        {
            this.targetPlanner = targetPlanner ?? throw new ArgumentNullException(nameof(targetPlanner));
        }

        public AiCardExecutionResult ExecuteFirstRecommended(
            CardDecisionResult decisionResult,
            AiCardHand aiCardHand,
            global::BoardManager boardManager,
            GameState gameState,
            AiPieceColor actor)
        {
            if (decisionResult == null || !decisionResult.ShouldUseCards)
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.NoRecommendation,
                    "AI card decision has no recommendation.");
            }

            AiCardExecutionResult lastFailure = null;

            foreach (CardUseRecommendation recommendation in decisionResult.Recommendations)
            {
                AiCardExecutionResult result = TryExecute(
                    recommendation,
                    aiCardHand,
                    boardManager,
                    gameState,
                    actor);
                if (result.Executed)
                    return result;

                lastFailure = result;
                Debug.Log($"[AI Card] Skipped '{recommendation.Card.Id}': {result.Reason}");
            }

            return lastFailure ?? AiCardExecutionResult.Failure(
                AiCardExecutionStatus.NoRecommendation,
                "AI card decision had no executable recommendation.");
        }

        public AiCardExecutionResult TryExecute(
            CardUseRecommendation recommendation,
            AiCardHand aiCardHand,
            global::BoardManager boardManager,
            GameState gameState,
            AiPieceColor actor)
        {
            if (recommendation == null)
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.NoRecommendation,
                    "Recommendation is null.");
            }

            if (aiCardHand == null)
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.CardNotInHand,
                    "AI card hand is null.",
                    recommendation);
            }

            if (!aiCardHand.TryFindByAiCardId(recommendation.Card.Id, out GameObject cardObject))
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.CardNotInHand,
                    $"AI hand does not contain card id '{recommendation.Card.Id}'.",
                    recommendation);
            }

            if (!targetPlanner.TryCreatePlan(
                    recommendation.Card.Id,
                    cardObject,
                    boardManager,
                    gameState,
                    actor,
                    out AiCardTargetPlan plan,
                    out AiCardExecutionStatus failureStatus,
                    out string reason))
            {
                global::CardDataSO failedCardSO = cardObject != null
                    ? GetCardDataSO(cardObject)
                    : null;
                return AiCardExecutionResult.Failure(
                    failureStatus,
                    reason,
                    recommendation,
                    failedCardSO);
            }

            global::ICard cardExecutor = plan.CardData.GetComponent<global::ICard>();
            if (cardExecutor == null)
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.MissingExecutor,
                    $"Card '{plan.CardData.DataSO.CardName}' has no ICard executor.",
                    recommendation,
                    plan.CardData.DataSO);
            }

            try
            {
                cardExecutor.Execute(plan.Args);
                aiCardHand.Consume(plan.CardData.DataSO);
                boardManager?.RefreshMoves();

                Debug.Log($"[AI Card] Executed '{plan.CardData.DataSO.CardName}' ({recommendation.Card.Id}) with {plan.UsePlan.Target.Kind} target.");
                return AiCardExecutionResult.Success(recommendation, plan.CardData.DataSO);
            }
            catch (Exception ex)
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.ExecutionFailed,
                    $"Card '{plan.CardData.DataSO.CardName}' execution failed: {ex.Message}",
                    recommendation,
                    plan.CardData.DataSO,
                    ex);
            }
        }

        private static global::CardDataSO GetCardDataSO(GameObject cardObject)
        {
            global::CardData cardData = cardObject != null
                ? cardObject.GetComponent<global::CardData>()
                : null;

            return cardData != null ? cardData.DataSO : null;
        }
    }
}
