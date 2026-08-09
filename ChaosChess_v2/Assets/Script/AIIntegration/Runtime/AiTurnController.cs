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
    public sealed class AiTurnController : global::TurnProvider
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
        private readonly CardTargetingModule cardTargetingModule = new CardTargetingModule();
        private int requestSequence;
        private int activeRequestId;
        private bool isRequestRunning;

        public override bool TryRequestTurn(
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

            TurnPlan selectedPlan = result.SelectedPlan;
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
            if (originalPlan.MovePlan != null &&
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
                postCardMoveCandidateCount: variationCount,
                opponentReplyCandidateCount: 0,
                beamWidth: Mathf.Max(1, variationCount));

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
