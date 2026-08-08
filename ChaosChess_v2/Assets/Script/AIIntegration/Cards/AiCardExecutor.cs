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

            AiCardTargetPlan plan;
            AiCardExecutionStatus failureStatus;
            string reason;
            bool createdPlan = recommendation.Plan != null
                ? targetPlanner.TryCreatePlan(
                    recommendation.Plan,
                    cardObject,
                    boardManager,
                    gameState,
                    actor,
                    out plan,
                    out failureStatus,
                    out reason)
                : targetPlanner.TryCreatePlan(
                    recommendation.Card.Id,
                    cardObject,
                    boardManager,
                    gameState,
                    actor,
                    out plan,
                    out failureStatus,
                    out reason);

            if (!createdPlan)
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

            return ExecuteUnityPlan(plan, aiCardHand, boardManager, recommendation);
        }

        public AiCardExecutionResult ExecutePlan(
            CardUsePlan usePlan,
            AiCardHand aiCardHand,
            global::BoardManager boardManager,
            GameState gameState,
            AiPieceColor actor)
        {
            if (usePlan == null)
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.NoRecommendation,
                    "CardUsePlan is null.");
            }

            if (aiCardHand == null)
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.CardNotInHand,
                    "AI card hand is null.");
            }

            if (!aiCardHand.TryFindByAiCardId(usePlan.CardId, out GameObject cardObject))
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.CardNotInHand,
                    $"AI hand does not contain card id '{usePlan.CardId}'.");
            }

            if (!targetPlanner.TryCreatePlan(
                    usePlan,
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
                    recommendation: null,
                    cardSO: failedCardSO);
            }

            return ExecuteUnityPlan(plan, aiCardHand, boardManager, recommendation: null);
        }

        public AiCardExecutionResult ExecuteCardId(
            string cardId,
            AiCardHand aiCardHand,
            global::BoardManager boardManager,
            GameState gameState,
            AiPieceColor actor)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.NoRecommendation,
                    "Card id is empty.");
            }

            if (aiCardHand == null)
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.CardNotInHand,
                    "AI card hand is null.");
            }

            if (!aiCardHand.TryFindByAiCardId(cardId, out GameObject cardObject))
            {
                return AiCardExecutionResult.Failure(
                    AiCardExecutionStatus.CardNotInHand,
                    $"AI hand does not contain card id '{cardId}'.");
            }

            if (!targetPlanner.TryCreatePlan(
                    cardId,
                    cardObject,
                    boardManager,
                    gameState,
                    actor,
                    out AiCardTargetPlan plan,
                    out AiCardExecutionStatus failureStatus,
                    out string reason))
            {
                return AiCardExecutionResult.Failure(
                    failureStatus,
                    reason,
                    recommendation: null,
                    cardSO: GetCardDataSO(cardObject));
            }

            return ExecuteUnityPlan(plan, aiCardHand, boardManager, recommendation: null);
        }

        private static AiCardExecutionResult ExecuteUnityPlan(
            AiCardTargetPlan plan,
            AiCardHand aiCardHand,
            global::BoardManager boardManager,
            CardUseRecommendation recommendation)
        {
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

                Debug.Log(
                    $"[AI Card] Executed '{plan.CardData.DataSO.CardName}' ({plan.UsePlan.CardId}), " +
                    $"caster={FormatCaster(plan)}, target={plan.UsePlan.Target.Kind}, " +
                    $"squares={FormatTargetSquares(plan)}.");
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

        private static string FormatTargetSquares(AiCardTargetPlan plan)
        {
            if (plan == null || plan.UsePlan == null || plan.UsePlan.Target == null)
                return "none";

            if (plan.UsePlan.Target.Squares.Count == 0)
                return "none";

            var labels = new string[plan.UsePlan.Target.Squares.Count];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = plan.UsePlan.Target.Squares[i].ToString();

            return string.Join(",", labels);
        }

        private static string FormatCaster(AiCardTargetPlan plan)
        {
            return plan != null && plan.Args != null
                ? plan.Args.ResolveCasterColor().ToString()
                : "unknown";
        }
    }
}
