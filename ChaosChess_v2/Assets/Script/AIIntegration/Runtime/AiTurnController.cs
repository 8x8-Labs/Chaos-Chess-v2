using System;
using System.Collections.Generic;
using ChaosChess.AI.Decision;
using ChaosChess.AI.Decision.CardTargeting;
using ChaosChess.AI.Decision.TurnPlanning;
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
        [SerializeField] private int cardCandidateCount = AiCardHand.MaxCards;
        [SerializeField] private int targetCandidateCount = 32;
        [SerializeField] private int maximumEngineCallCount = 64;
        [SerializeField] private bool allowCoarseCardEffects = true;
        [SerializeField] private int cardUseScoreTolerance = 25;

        [Header("카테고리 점수")]
        [SerializeField] private int tacticalScore = 10;
        [SerializeField] private int defensiveScore = 8;
        [SerializeField] private int mobilityScore = 8;
        [SerializeField] private int boardControlScore = 10;
        [SerializeField] private int summonScore = 7;
        [SerializeField] private int transformationScore = 7;
        [SerializeField] private int utilityScore = 5;

        private readonly AiCardExecutor cardExecutor = new AiCardExecutor();
        private readonly CardTargetingModule cardTargetingModule = new CardTargetingModule();
        private int requestSequence;
        private int activeRequestId;
        private bool isRequestRunning;
        private string queuedForcedCardId;

        public bool HasQueuedForcedCard => !string.IsNullOrWhiteSpace(queuedForcedCardId);
        public string QueuedForcedCardId => queuedForcedCardId;

        public void QueueForcedCardForNextAiTurn(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                Debug.LogWarning("[AI Turn] Ignored empty forced card request.");
                return;
            }

            queuedForcedCardId = cardId;
            Debug.Log($"[AI Turn] Queued forced AI card '{queuedForcedCardId}' for the next AI turn.");
        }

        public void ClearQueuedForcedCard()
        {
            if (string.IsNullOrWhiteSpace(queuedForcedCardId))
                return;

            Debug.Log($"[AI Turn] Cleared queued forced AI card '{queuedForcedCardId}'.");
            queuedForcedCardId = null;
        }

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

            if (!string.IsNullOrWhiteSpace(queuedForcedCardId))
            {
                string forcedCardId = queuedForcedCardId;
                queuedForcedCardId = null;

                ExecuteForcedCardAndReanalyze(
                    requestId,
                    gameManager,
                    boardManager,
                    hand,
                    forcedCardId,
                    mapping.GameState,
                    perspective,
                    fallbackMoveRequest);
                return true;
            }

            FairyStockfishBridge.Instance.AnalyzePositionAsync(
                mapping.Fen,
                analysisDepth,
                variationCount,
                perspective,
                onComplete: (_, snapshot) =>
                {
                    if (!IsActiveRequest(requestId))
                        return;

                    try
                    {
                        HandleAnalysisComplete(
                            requestId,
                            gameManager,
                            boardManager,
                            hand,
                            mapping,
                            snapshot,
                            fallbackMoveRequest);
                    }
                    catch (Exception ex)
                    {
                        CompleteAndRequestFallback(
                            requestId,
                            fallbackMoveRequest,
                            $"Turn planning failed: {ex.Message}");
                    }
                },
                onError: (_, error) =>
                {
                    if (!IsActiveRequest(requestId))
                        return;

                    CompleteAndRequestFallback(
                        requestId,
                        fallbackMoveRequest,
                        $"Card analysis failed: {error}");
                });

            return true;
        }

        private void HandleAnalysisComplete(
            int requestId,
            global::GameManager gameManager,
            global::BoardManager boardManager,
            AiCardHand hand,
            UnityGameStateMappingResult mapping,
            UciAnalysisSnapshot snapshot,
            Action fallbackMoveRequest)
        {
            if (!IsActiveRequest(requestId))
                return;

            if (this == null || gameManager == null || boardManager == null)
            {
                CompleteRequest(requestId);
                return;
            }

            if (gameManager.IsEndGame)
            {
                CompleteRequest(requestId);
                return;
            }

            if (snapshot == null || !snapshot.HasMoves)
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    "Card analysis returned no moves.");
                return;
            }

            var actor = UnityAiColorMapper.ToAiColor(gameManager.turnColor);
            UnifiedTurnPlanner planner = CreateTurnPlanner(mapping.Fen, snapshot);
            TurnPlannerResult result = planner.PlanTurn(mapping.GameState);
            LogTurnPlannerTrace(result);

            TurnPlan selectedPlan = SelectCardBiasedPlan(result);
            if (selectedPlan == null)
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    "UnifiedTurnPlanner selected no executable plan.");
                return;
            }

            Debug.Log(
                $"[AI Turn] Selected TurnPlan rank='{selectedPlan.DeterministicRankKey}', " +
                $"usesCard={selectedPlan.UsesCard}, hasMove={selectedPlan.HasMove}, score={selectedPlan.Score.Total}.");

            if (!selectedPlan.UsesCard)
            {
                ExecuteSelectedMoveOrFallback(
                    requestId,
                    gameManager,
                    boardManager,
                    selectedPlan.MovePlan,
                    fallbackMoveRequest,
                    "no-card TurnPlan");
                return;
            }

            ExecuteCardPlanAndReanalyze(
                requestId,
                gameManager,
                boardManager,
                hand,
                selectedPlan,
                mapping.GameState,
                actor,
                fallbackMoveRequest);
        }

        private void ExecuteCardPlanAndReanalyze(
            int requestId,
            global::GameManager gameManager,
            global::BoardManager boardManager,
            AiCardHand hand,
            TurnPlan selectedPlan,
            ChaosChess.AI.Domain.GameState planningGameState,
            ChaosChess.AI.Domain.PieceColor actor,
            Action fallbackMoveRequest)
        {
            AiCardExecutionResult execution = cardExecutor.ExecutePlan(
                selectedPlan.CardPlan,
                hand,
                boardManager,
                planningGameState,
                actor);

            if (!execution.Executed)
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    $"Selected card plan failed: {execution.Status} - {execution.Reason}");
                return;
            }

            if (gameManager.FinishType != global::GameResult.None || gameManager.IsEndGame)
            {
                gameManager.ReevaluateGameState();
                CompleteRequest(requestId);
                return;
            }

            UnityGameStateMappingResult actualMapping;
            try
            {
                actualMapping = UnityGameStateMapper.Capture(boardManager, hand);
            }
            catch (Exception ex)
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    $"Post-card actual state recapture failed: {ex.Message}");
                return;
            }

            foreach (string warning in actualMapping.Warnings)
                Debug.LogWarning($"[AI Turn] Post-card recapture: {warning}");

            Debug.Log($"[AI Turn] Post-card actual state recaptured. Reanalyzing FEN: {actualMapping.Fen}");

            var perspective = UnityAiColorMapper.ToAiColor(gameManager.turnColor);
            FairyStockfishBridge.Instance.AnalyzePositionAsync(
                actualMapping.Fen,
                analysisDepth,
                variationCount,
                perspective,
                onComplete: (_, postCardSnapshot) =>
                {
                    if (!IsActiveRequest(requestId))
                        return;

                    try
                    {
                        HandlePostCardAnalysisComplete(
                            requestId,
                            gameManager,
                            boardManager,
                            actualMapping,
                            selectedPlan,
                            postCardSnapshot,
                            fallbackMoveRequest);
                    }
                    catch (Exception ex)
                    {
                        CompleteAndRequestFallback(
                            requestId,
                            fallbackMoveRequest,
                            $"Post-card move planning failed: {ex.Message}");
                    }
                },
                onError: (_, error) =>
                {
                    if (!IsActiveRequest(requestId))
                        return;

                    CompleteAndRequestFallback(
                        requestId,
                        fallbackMoveRequest,
                        $"Post-card analysis failed: {error}");
                });
        }

        private void ExecuteForcedCardAndReanalyze(
            int requestId,
            global::GameManager gameManager,
            global::BoardManager boardManager,
            AiCardHand hand,
            string forcedCardId,
            ChaosChess.AI.Domain.GameState planningGameState,
            ChaosChess.AI.Domain.PieceColor actor,
            Action fallbackMoveRequest)
        {
            Debug.Log($"[AI Turn] Executing queued forced card '{forcedCardId}'.");
            AiCardExecutionResult execution = cardExecutor.ExecuteCardId(
                forcedCardId,
                hand,
                boardManager,
                planningGameState,
                actor);

            if (!execution.Executed)
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    $"Queued forced card '{forcedCardId}' failed: {execution.Status} - {execution.Reason}");
                return;
            }

            if (gameManager.FinishType != global::GameResult.None || gameManager.IsEndGame)
            {
                gameManager.ReevaluateGameState();
                CompleteRequest(requestId);
                return;
            }

            UnityGameStateMappingResult actualMapping;
            try
            {
                actualMapping = UnityGameStateMapper.Capture(boardManager, hand);
            }
            catch (Exception ex)
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    $"Post-forced-card actual state recapture failed: {ex.Message}");
                return;
            }

            foreach (string warning in actualMapping.Warnings)
                Debug.LogWarning($"[AI Turn] Post-forced-card recapture: {warning}");

            Debug.Log($"[AI Turn] Queued forced card applied. Reanalyzing FEN: {actualMapping.Fen}");

            var perspective = UnityAiColorMapper.ToAiColor(gameManager.turnColor);
            FairyStockfishBridge.Instance.AnalyzePositionAsync(
                actualMapping.Fen,
                analysisDepth,
                variationCount,
                perspective,
                onComplete: (_, postCardSnapshot) =>
                {
                    if (!IsActiveRequest(requestId))
                        return;

                    try
                    {
                        HandlePostCardAnalysisComplete(
                            requestId,
                            gameManager,
                            boardManager,
                            actualMapping,
                            originalPlan: null,
                            postCardSnapshot,
                            fallbackMoveRequest);
                    }
                    catch (Exception ex)
                    {
                        CompleteAndRequestFallback(
                            requestId,
                            fallbackMoveRequest,
                            $"Post-forced-card move planning failed: {ex.Message}");
                    }
                },
                onError: (_, error) =>
                {
                    if (!IsActiveRequest(requestId))
                        return;

                    CompleteAndRequestFallback(
                        requestId,
                        fallbackMoveRequest,
                        $"Post-forced-card analysis failed: {error}");
                });
        }

        private void HandlePostCardAnalysisComplete(
            int requestId,
            global::GameManager gameManager,
            global::BoardManager boardManager,
            UnityGameStateMappingResult actualMapping,
            TurnPlan originalPlan,
            UciAnalysisSnapshot postCardSnapshot,
            Action fallbackMoveRequest)
        {
            if (!IsActiveRequest(requestId))
                return;

            if (this == null || gameManager == null || boardManager == null)
            {
                CompleteRequest(requestId);
                return;
            }

            if (gameManager.IsEndGame)
            {
                CompleteRequest(requestId);
                return;
            }

            if (postCardSnapshot == null || !postCardSnapshot.HasMoves)
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    "Post-card analysis returned no moves.");
                return;
            }

            var snapshotEngine = new FairyStockfishSnapshotEngine(
                actualMapping.Fen,
                postCardSnapshot,
                FairyStockfishBridge.Instance.IsInCheck());
            var moveFilter = new MoveFilter(snapshotEngine);
            MoveFilterResult moveResult = moveFilter.GetFilteredMoves(
                actualMapping.GameState,
                variationCount);

            if (!moveResult.HasRecommendations)
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    "Post-card MoveFilter returned no executable recommendations.");
                return;
            }

            MoveRecommendation recommendation = moveResult.Recommendations[0];
            if (originalPlan != null &&
                originalPlan.MovePlan != null &&
                !string.Equals(originalPlan.MovePlan.UciMove, recommendation.Candidate.UciMove, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    $"[AI Turn] Simulated/actual move mismatch. Planned '{originalPlan.MovePlan.UciMove}', " +
                    $"actual filtered '{recommendation.Candidate.UciMove}'. Using actual filtered move.");
            }

            ExecuteUciMoveOrFallback(
                requestId,
                gameManager,
                boardManager,
                recommendation.Candidate.UciMove,
                fallbackMoveRequest,
                "post-card actual MoveFilter");
        }

        private UnifiedTurnPlanner CreateTurnPlanner(
            string fen,
            UciAnalysisSnapshot snapshot)
        {
            var snapshotEngine = new FairyStockfishSnapshotEngine(
                fen,
                snapshot,
                FairyStockfishBridge.Instance.IsInCheck());
            var moveFilter = new MoveFilter(snapshotEngine);
            var options = new TurnPlannerOptions(
                noCardMoveCandidateCount: variationCount,
                cardCandidateCount: Mathf.Clamp(cardCandidateCount, 1, AiCardHand.MaxCards),
                targetCandidateCount: Mathf.Max(1, targetCandidateCount),
                postCardMoveCandidateCount: variationCount,
                opponentReplyCandidateCount: 0,
                beamWidth: Mathf.Max(1, variationCount),
                maximumEngineCallCount: Mathf.Max(1, maximumEngineCallCount),
                allowCoarseCardEffects: allowCoarseCardEffects);

            return new UnifiedTurnPlanner(
                moveFilter,
                cardTargetingModule,
                options: options);
        }

        private void ExecuteSelectedMoveOrFallback(
            int requestId,
            global::GameManager gameManager,
            global::BoardManager boardManager,
            MovePlan movePlan,
            Action fallbackMoveRequest,
            string source)
        {
            if (movePlan == null)
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    $"{source} did not contain a move plan.");
                return;
            }

            ExecuteUciMoveOrFallback(
                requestId,
                gameManager,
                boardManager,
                movePlan.UciMove,
                fallbackMoveRequest,
                source);
        }

        private void ExecuteUciMoveOrFallback(
            int requestId,
            global::GameManager gameManager,
            global::BoardManager boardManager,
            string uciMove,
            Action fallbackMoveRequest,
            string source)
        {
            if (!IsActiveRequest(requestId))
                return;

            if (this == null || gameManager == null || boardManager == null)
            {
                CompleteRequest(requestId);
                return;
            }

            if (gameManager.IsEndGame)
            {
                CompleteRequest(requestId);
                return;
            }

            if (!boardManager.IsValidUciMove(uciMove))
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    $"{source} selected invalid UCI move '{uciMove}'.");
                return;
            }

            Debug.Log($"[AI Turn] Executing {source} move '{uciMove}'.");
            CompleteRequest(requestId);
            boardManager.ApplyUCIMove(uciMove);
        }

        private void CompleteAndRequestFallback(
            int requestId,
            Action fallbackMoveRequest,
            string reason)
        {
            if (!IsActiveRequest(requestId))
                return;

            Debug.LogWarning($"[AI Turn] Falling back to move-only path: {reason}");
            CompleteRequest(requestId);
            fallbackMoveRequest?.Invoke();
        }

        private static void LogTurnPlannerTrace(TurnPlannerResult result)
        {
            if (result == null)
                return;

            TurnPlannerTraceSummary trace = result.TraceSummary;
            Debug.Log(
                "[AI Turn] UnifiedTurnPlanner trace: " +
                $"selected={trace.SelectedCandidateCount}, skipped={trace.SkippedCandidateCount}, " +
                $"rootMoves={trace.RootNoCardMoveCandidateCount}, consideredCards={trace.ConsideredCardCandidateCount}, " +
                $"postCardMoves={trace.PostCardMoveCandidateCount}, engineCalls={trace.EngineCallCount}/{trace.MaximumEngineCallCount}, " +
                $"beamPruned={trace.BeamPrunedCandidateCount}.");
        }

        private TurnPlan SelectCardBiasedPlan(TurnPlannerResult result)
        {
            if (result == null || !result.HasPlan)
                return null;

            TurnPlan selectedPlan = result.SelectedPlan;
            if (selectedPlan == null || selectedPlan.UsesCard)
                return selectedPlan;

            TurnPlan bestCardPlan = null;
            foreach (TurnPlanCandidate candidate in result.Candidates)
            {
                if (candidate == null || !candidate.HasPlan || candidate.Plan == null || !candidate.Plan.UsesCard)
                    continue;

                if (bestCardPlan == null || candidate.Plan.Score.Total > bestCardPlan.Score.Total)
                    bestCardPlan = candidate.Plan;
            }

            if (bestCardPlan == null)
                return selectedPlan;

            int tolerance = Mathf.Max(0, cardUseScoreTolerance);
            int scoreGap = selectedPlan.Score.Total - bestCardPlan.Score.Total;
            if (scoreGap > tolerance)
                return selectedPlan;

            Debug.Log(
                $"[AI Turn] Card-biased selection chose card plan within tolerance. " +
                $"noCardScore={selectedPlan.Score.Total}, cardScore={bestCardPlan.Score.Total}, " +
                $"gap={scoreGap}, tolerance={tolerance}, card={bestCardPlan.CardPlan?.CardId}.");
            return bestCardPlan;
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
