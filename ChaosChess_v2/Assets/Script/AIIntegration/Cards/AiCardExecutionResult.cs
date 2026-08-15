using System;
using ChaosChess.AI.Decision;
using ChaosChess.AI.Domain;

namespace ChaosChess.Unity.AIIntegration.Cards
{
    public enum AiCardExecutionStatus
    {
        Executed,
        NoRecommendation,
        CardNotInHand,
        MissingCardData,
        MissingExecutor,
        UnsupportedCardType,
        TargetUnavailable,
        CardUseBlocked,
        ExecutionFailed
    }

    public sealed class AiCardExecutionResult
    {
        private AiCardExecutionResult(
            AiCardExecutionStatus status,
            CardUseRecommendation recommendation,
            global::CardDataSO cardSO,
            CardUsePlan usePlan,
            string reason,
            Exception exception)
        {
            Status = status;
            Recommendation = recommendation;
            CardSO = cardSO;
            UsePlan = usePlan;
            Reason = reason ?? string.Empty;
            Exception = exception;
        }

        public AiCardExecutionStatus Status { get; }
        public CardUseRecommendation Recommendation { get; }
        public global::CardDataSO CardSO { get; }
        public CardUsePlan UsePlan { get; }
        public string Reason { get; }
        public Exception Exception { get; }
        public bool Executed => Status == AiCardExecutionStatus.Executed;

        public static AiCardExecutionResult Success(
            CardUseRecommendation recommendation,
            global::CardDataSO cardSO,
            CardUsePlan usePlan)
        {
            return new AiCardExecutionResult(
                AiCardExecutionStatus.Executed,
                recommendation,
                cardSO,
                usePlan,
                "Executed.",
                exception: null);
        }

        public static AiCardExecutionResult Failure(
            AiCardExecutionStatus status,
            string reason,
            CardUseRecommendation recommendation = null,
            global::CardDataSO cardSO = null,
            CardUsePlan usePlan = null,
            Exception exception = null)
        {
            if (status == AiCardExecutionStatus.Executed)
                throw new ArgumentOutOfRangeException(nameof(status), status, "Use Success for executed cards.");

            return new AiCardExecutionResult(status, recommendation, cardSO, usePlan, reason, exception);
        }
    }
}
