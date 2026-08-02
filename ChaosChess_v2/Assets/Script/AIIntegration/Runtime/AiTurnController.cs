using System;
using System.Collections.Generic;
using ChaosChess.AI.Decision;
using ChaosChess.AI.Evaluation;
using ChaosChess.Unity.AIIntegration.Cards;
using ChaosChess.Unity.AIIntegration.Engine;
using ChaosChess.Unity.AIIntegration.Mapping;
using UnityEngine;

namespace ChaosChess.Unity.AIIntegration.Runtime
{
    public sealed class AiTurnController : MonoBehaviour
    {
        [Header("AI 카드")]
        [SerializeField] private bool aiCardUsageEnabled = true;
        [SerializeField] private AiCardHand aiCardHand;

        [Header("AI 분석")]
        [SerializeField] private int analysisDepth = 12;
        [SerializeField] private int variationCount = 3;
        [SerializeField] private int minimumCardScoreGain = 1;

        [Header("카테고리 점수")]
        [SerializeField] private int tacticalScore = 10;
        [SerializeField] private int defensiveScore = 8;
        [SerializeField] private int mobilityScore = 8;
        [SerializeField] private int boardControlScore = 10;
        [SerializeField] private int summonScore = 7;
        [SerializeField] private int transformationScore = 7;
        [SerializeField] private int utilityScore = 5;

        private readonly AiCardExecutor cardExecutor = new AiCardExecutor();
        private int requestSequence;
        private int activeRequestId;
        private bool isRequestRunning;

        public bool TryRequestTurn(
            global::GameManager gameManager,
            global::BoardManager boardManager,
            Action fallbackMoveRequest)
        {
            if (!aiCardUsageEnabled)
                return false;

            if (gameManager == null || boardManager == null)
                return false;

            if (isRequestRunning)
            {
                Debug.LogWarning("[AI Turn] Ignored duplicate AI turn request while card analysis is running.");
                return true;
            }

            AiCardHand hand = ResolveCardHand();
            if (hand == null || hand.AvailableCards == null || hand.AvailableCards.Count == 0)
                return false;

            if (analysisDepth <= 0 || variationCount <= 0)
            {
                Debug.LogWarning("[AI Turn] Invalid AI card analysis settings. Falling back to move only.");
                return false;
            }

            int requestId = ++requestSequence;
            activeRequestId = requestId;
            isRequestRunning = true;

            UnityGameStateMappingResult mapping;
            try
            {
                mapping = UnityGameStateMapper.Capture(boardManager, hand);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AI Turn] Failed to capture game state for card decision: {ex.Message}");
                CompleteRequest(requestId);
                return false;
            }

            foreach (string warning in mapping.Warnings)
                Debug.LogWarning($"[AI Turn] {warning}");

            var perspective = UnityAiColorMapper.ToAiColor(gameManager.turnColor);

            FairyStockfishBridge.Instance.AnalyzePositionAsync(
                mapping.Fen,
                analysisDepth,
                variationCount,
                perspective,
                onComplete: (_, snapshot) =>
                {
                    if (!IsActiveRequest(requestId))
                        return;

                    bool shouldFallback = true;
                    try
                    {
                        shouldFallback = HandleAnalysisComplete(
                            requestId,
                            gameManager,
                            boardManager,
                            hand,
                            mapping,
                            snapshot);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AI Turn] Card decision failed: {ex.Message}");
                        shouldFallback = true;
                    }
                    finally
                    {
                        CompleteRequest(requestId);
                    }

                    if (shouldFallback)
                        fallbackMoveRequest?.Invoke();
                },
                onError: (_, error) =>
                {
                    if (!IsActiveRequest(requestId))
                        return;

                    Debug.LogWarning($"[AI Turn] Card analysis failed: {error}");
                    CompleteRequest(requestId);
                    fallbackMoveRequest?.Invoke();
                });

            return true;
        }

        private bool HandleAnalysisComplete(
            int requestId,
            global::GameManager gameManager,
            global::BoardManager boardManager,
            AiCardHand hand,
            UnityGameStateMappingResult mapping,
            UciAnalysisSnapshot snapshot)
        {
            if (!IsActiveRequest(requestId))
                return false;

            if (this == null || gameManager == null || boardManager == null)
                return false;

            if (gameManager.IsEndGame)
                return false;

            if (snapshot == null || !snapshot.HasMoves)
            {
                Debug.LogWarning("[AI Turn] Card analysis returned no moves. Falling back to move only.");
                return true;
            }

            var snapshotEngine = new FairyStockfishSnapshotEngine(
                mapping.Fen,
                snapshot,
                FairyStockfishBridge.Instance.IsInCheck());
            var actor = UnityAiColorMapper.ToAiColor(gameManager.turnColor);
            var evaluator = new GameStateEvaluator(
                snapshotEngine,
                new EvaluationOptions(searchDepth: analysisDepth));
            EvaluationResult evaluation = evaluator.Evaluate(
                mapping.GameState,
                actor);
            var decisionModule = new CardDecisionModule(
                new ConfiguredCardScorer(BuildCategoryScores()),
                new EloCardProfile(
                    minimumScoreGain: Mathf.Max(0, minimumCardScoreGain),
                    maximumCardsPerTurn: 1));

            CardDecisionResult decision = decisionModule.Decide(
                mapping.GameState,
                evaluation,
                actor);
            AiCardExecutionResult execution = cardExecutor.ExecuteFirstRecommended(
                decision,
                hand,
                boardManager,
                mapping.GameState,
                actor);

            if (!execution.Executed)
            {
                Debug.Log($"[AI Turn] No AI card executed: {execution.Status} - {execution.Reason}");
                return true;
            }

            boardManager.RefreshMoves();

            if (gameManager.FinishType != global::GameResult.None || gameManager.IsEndGame)
            {
                gameManager.ReevaluateGameState();
                return false;
            }

            return true;
        }

        private AiCardHand ResolveCardHand()
        {
            if (aiCardHand == null)
                aiCardHand = FindFirstObjectByType<AiCardHand>();

            return aiCardHand;
        }

        private bool IsActiveRequest(int requestId)
        {
            return isRequestRunning && activeRequestId == requestId;
        }

        private void CompleteRequest(int requestId)
        {
            if (activeRequestId != requestId)
                return;

            isRequestRunning = false;
            activeRequestId = 0;
        }

        private IReadOnlyDictionary<string, int> BuildCategoryScores()
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [global::AiCardCategory.Tactical.ToString()] = tacticalScore,
                [global::AiCardCategory.Defensive.ToString()] = defensiveScore,
                [global::AiCardCategory.Mobility.ToString()] = mobilityScore,
                [global::AiCardCategory.BoardControl.ToString()] = boardControlScore,
                [global::AiCardCategory.Summon.ToString()] = summonScore,
                [global::AiCardCategory.Transformation.ToString()] = transformationScore,
                [global::AiCardCategory.Utility.ToString()] = utilityScore
            };
        }
    }
}
