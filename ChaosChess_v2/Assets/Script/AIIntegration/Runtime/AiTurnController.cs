using System;
using System.Collections.Generic;
using System.Text;
using ChaosChess.AI.Decision;
using ChaosChess.AI.Decision.CardTargeting;
using ChaosChess.AI.Decision.TurnPlanning;
using ChaosChess.AI.Domain;
using ChaosChess.AI.Domain.CardEffects;
using ChaosChess.AI.Fen;
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
        [SerializeField] private int opponentReplyCandidateCount = TurnPlannerOptions.DefaultOpponentReplyCandidateCount;
        [SerializeField] private int maximumEngineCallCount = 64;
        [SerializeField] private bool allowCoarseCardEffects = true;
        [SerializeField] private int cardUseScoreTolerance = 120;
        [SerializeField] private int fullHandCardUseScoreTolerance = 1000;

        [Header("AI 디버그")]
        [SerializeField] private bool directCardFallbackEnabled = false;

        [Header("카테고리 점수")]
        [SerializeField] private int tacticalScore = 10;
        [SerializeField] private int defensiveScore = 8;
        [SerializeField] private int mobilityScore = 8;
        [SerializeField] private int boardControlScore = 10;
        [SerializeField] private int summonScore = 7;
        [SerializeField] private int transformationScore = 7;
        [SerializeField] private int utilityScore = 5;

        private readonly AiCardExecutor cardExecutor = new AiCardExecutor();
        private readonly AiCardTargetPlanner targetPlanner = new AiCardTargetPlanner();
        private readonly CardTargetingModule cardTargetingModule = new CardTargetingModule();
        private int requestSequence;
        private int activeRequestId;
        private bool isRequestRunning;
        private string queuedForcedCardId;

        public bool HasQueuedForcedCard => !string.IsNullOrWhiteSpace(queuedForcedCardId);
        public string QueuedForcedCardId => queuedForcedCardId;
        public AiCardHand CardHand => ResolveCardHand();

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

            Debug.Log(
                $"[AI Turn] Card hand mapped: unityHand={hand.AvailableCards.Count}, " +
                $"aiCards={mapping.GameState.AvailableCards.Count}, cards={FormatAvailableCards(mapping.GameState.AvailableCards)}.");

            var perspective = UnityAiColorMapper.ToAiColor(gameManager.turnColor);
            LogHandTargetDiagnostics(hand, boardManager, mapping.GameState, perspective);

            if (!string.IsNullOrWhiteSpace(queuedForcedCardId))
            {
                string forcedCardId = queuedForcedCardId;
                queuedForcedCardId = null;

                if (!ContainsMappedCard(mapping.GameState.AvailableCards, forcedCardId))
                {
                    CompleteAndRequestFallback(
                        requestId,
                        fallbackMoveRequest,
                        $"Queued forced card '{forcedCardId}' is not in the mapped AI hand. mappedCards={FormatAvailableCards(mapping.GameState.AvailableCards)}");
                    return true;
                }

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
                        AnalyzeOpponentReplyAndHandle(
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

        private void AnalyzeOpponentReplyAndHandle(
            int requestId,
            global::GameManager gameManager,
            global::BoardManager boardManager,
            AiCardHand hand,
            UnityGameStateMappingResult mapping,
            UciAnalysisSnapshot snapshot,
            Action fallbackMoveRequest)
        {
            int replyCount = Mathf.Max(0, opponentReplyCandidateCount);
            if (replyCount == 0)
            {
                HandleAnalysisComplete(
                    requestId,
                    gameManager,
                    boardManager,
                    hand,
                    mapping,
                    snapshot,
                    opponentReplyFen: null,
                    opponentReplySnapshot: null,
                    fallbackMoveRequest: fallbackMoveRequest);
                return;
            }

            string opponentReplyFen = CreateOpponentReplyFen(mapping.GameState);
            ChaosChess.AI.Domain.PieceColor opponent = Opponent(UnityAiColorMapper.ToAiColor(gameManager.turnColor));

            FairyStockfishBridge.Instance.AnalyzePositionAsync(
                opponentReplyFen,
                analysisDepth,
                replyCount,
                opponent,
                onComplete: (_, opponentReplySnapshot) =>
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
                            opponentReplyFen,
                            opponentReplySnapshot,
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

                    Debug.LogWarning($"[AI Turn] Opponent reply analysis failed; continuing without reply scoring. error={error}");
                    HandleAnalysisComplete(
                        requestId,
                        gameManager,
                        boardManager,
                        hand,
                        mapping,
                        snapshot,
                        opponentReplyFen: null,
                        opponentReplySnapshot: null,
                        fallbackMoveRequest: fallbackMoveRequest);
                });
        }

        private void HandleAnalysisComplete(
            int requestId,
            global::GameManager gameManager,
            global::BoardManager boardManager,
            AiCardHand hand,
            UnityGameStateMappingResult mapping,
            UciAnalysisSnapshot snapshot,
            string opponentReplyFen,
            UciAnalysisSnapshot opponentReplySnapshot,
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
            UnifiedTurnPlanner planner = CreateTurnPlanner(mapping.Fen, snapshot, opponentReplyFen, opponentReplySnapshot);
            TurnPlannerResult result = planner.PlanTurn(mapping.GameState);
            LogTurnPlannerTrace(result);
            LogPlannerCardCandidates(result);

            TurnPlan selectedPlan = SelectCardBiasedPlan(result);
            selectedPlan = SelectExtraActionConstrainedPlan(result, selectedPlan, gameManager);
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
            LogSelectedPlanScoreComponents(selectedPlan);

            if (TrySelectAwakenedUpgradeAction(
                    boardManager,
                    gameManager,
                    gameManager.turnColor,
                    selectedPlan,
                    out global::FatherEnemyEffector awakenedUpgrade,
                    out string awakenedUpgradeSummary,
                    out int awakenedUpgradeScore))
            {
                ExecuteAwakenedUpgradeAction(
                    requestId,
                    awakenedUpgrade,
                    awakenedUpgradeSummary,
                    awakenedUpgradeScore,
                    fallbackMoveRequest);
                return;
            }

            if (!selectedPlan.UsesCard)
            {
                if (directCardFallbackEnabled &&
                    !HasExecutableCardPlan(result) &&
                    TryCreateDirectFallbackCardPlan(
                        hand,
                        boardManager,
                        mapping.GameState,
                        actor,
                        out TurnPlan directFallbackPlan))
                {
                    ExecuteCardPlanAndReanalyze(
                        requestId,
                        gameManager,
                        boardManager,
                        hand,
                        directFallbackPlan,
                        mapping.GameState,
                        actor,
                        fallbackMoveRequest);
                    return;
                }

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
                            execution.UsePlan,
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
                            executedCardPlan: execution.UsePlan,
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
            CardUsePlan executedCardPlan,
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
            CardUsePlan committedCardPlan = originalPlan?.CardPlan ?? executedCardPlan;

            if (ShouldPreserveOriginalPostCardMove(originalPlan) &&
                TryGetExecutableOriginalMove(
                    originalPlan,
                    boardManager,
                    out string plannedMove) &&
                IsAllowedCommittedCardMove(committedCardPlan, plannedMove) &&
                !gameManager.ShouldRejectCardAwareAIMove(plannedMove))
            {
                if (moveResult.HasRecommendations &&
                    !string.Equals(moveResult.Recommendations[0].Candidate.UciMove, plannedMove, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log(
                        $"[AI Turn] Keeping planned post-card move '{plannedMove}' over actual filtered " +
                        $"'{moveResult.Recommendations[0].Candidate.UciMove}' to preserve the selected card+move TurnPlan.");
                }

                ExecuteUciMoveOrFallback(
                    requestId,
                    gameManager,
                    boardManager,
                    plannedMove,
                    fallbackMoveRequest,
                    "post-card planned TurnPlan");
                return;
            }
            else if (ShouldPreserveOriginalPostCardMove(originalPlan) &&
                originalPlan?.MovePlan != null &&
                !IsAllowedCommittedCardMove(committedCardPlan, originalPlan.MovePlan.UciMove))
            {
                Debug.Log(
                    $"[AI Turn] Planned post-card move '{originalPlan.MovePlan.UciMove}' does not move the committed " +
                    $"card target for '{committedCardPlan?.CardId}'. Using the next engine-filtered recommendation.");
            }
            else if (ShouldPreserveOriginalPostCardMove(originalPlan) &&
                originalPlan?.MovePlan != null &&
                gameManager.ShouldRejectCardAwareAIMove(originalPlan.MovePlan.UciMove))
            {
                Debug.Log(
                    $"[AI Turn] Planned post-card move '{originalPlan.MovePlan.UciMove}' was rejected by card-aware safety. " +
                    "Using the next engine-filtered recommendation.");
            }

            if (originalPlan != null &&
                originalPlan.MovePlan != null &&
                !ShouldPreserveOriginalPostCardMove(originalPlan))
            {
                Debug.Log(
                    $"[AI Turn] Using actual post-card analysis for '{originalPlan.CardPlan?.CardId}' " +
                    $"instead of coarse planned move '{originalPlan.MovePlan.UciMove}'.");
            }

            if (!moveResult.HasRecommendations)
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    "Post-card MoveFilter returned no executable recommendations.");
                return;
            }

            if (!TrySelectCardAwareRecommendation(gameManager, committedCardPlan, moveResult, out MoveRecommendation recommendation))
            {
                if (TrySelectCommittedTargetLegalMove(
                    committedCardPlan,
                    boardManager,
                    FairyStockfishBridge.Instance,
                    gameManager,
                    out string committedTargetMove))
                {
                    ExecuteUciMoveOrFallback(
                        requestId,
                        gameManager,
                        boardManager,
                        committedTargetMove,
                        fallbackMoveRequest,
                        "post-card committed target legal fallback");
                    return;
                }

                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    "Post-card MoveFilter recommendations were all rejected by card-aware safety.");
                return;
            }

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

        private static bool TrySelectCardAwareRecommendation(
            global::GameManager gameManager,
            CardUsePlan committedCardPlan,
            MoveFilterResult moveResult,
            out MoveRecommendation recommendation)
        {
            recommendation = null;

            if (moveResult == null || !moveResult.HasRecommendations)
                return false;

            foreach (MoveRecommendation candidate in moveResult.Recommendations)
            {
                string uciMove = candidate?.Candidate?.UciMove;
                if (string.IsNullOrWhiteSpace(uciMove))
                    continue;

                if (!IsAllowedCommittedCardMove(committedCardPlan, uciMove))
                {
                    Debug.Log(
                        $"[AI Turn] Skipped engine-filtered move '{uciMove}' because it does not move the committed " +
                        $"card target for '{committedCardPlan?.CardId}'.");
                    continue;
                }

                if (gameManager.ShouldRejectCardAwareAIMove(uciMove))
                {
                    Debug.Log(
                        $"[AI Turn] Skipped engine-filtered move '{uciMove}' because card-aware safety rejected it.");
                    continue;
                }

                recommendation = candidate;
                return true;
            }

            return false;
        }

        private static bool TrySelectCommittedTargetLegalMove(
            CardUsePlan cardPlan,
            global::BoardManager boardManager,
            FairyStockfishBridge stockfish,
            global::GameManager gameManager,
            out string selectedMove)
        {
            selectedMove = null;

            if (!RequiresCommittedTargetMove(cardPlan) ||
                boardManager == null ||
                stockfish == null ||
                gameManager == null)
            {
                return false;
            }

            string targetSource = cardPlan.Target.Piece?.Square.ToString();
            if (string.IsNullOrWhiteSpace(targetSource))
                return false;

            string[] legalMoves = stockfish.GetLegalMoves();
            if (legalMoves == null || legalMoves.Length == 0)
                return false;

            string firstLegal = null;
            string bestCapture = null;
            int bestCaptureValue = int.MinValue;

            foreach (string move in legalMoves)
            {
                if (!boardManager.IsValidUciMove(move) ||
                    !gameManager.IsMoveAllowedByExtraAction(move) ||
                    !IsAllowedCommittedCardMove(cardPlan, move) ||
                    gameManager.ShouldRejectCardAwareAIMove(move))
                {
                    continue;
                }

                firstLegal ??= move;

                Vector3Int destination = boardManager.UCIToGrid(move.Substring(2, 2));
                global::Piece capturedPiece = boardManager.GetPiece(destination);
                if (capturedPiece == null)
                    continue;

                int captureValue = GetRuntimePieceValue(capturedPiece.Type);
                if (captureValue > bestCaptureValue)
                {
                    bestCaptureValue = captureValue;
                    bestCapture = move;
                }
            }

            selectedMove = bestCapture ?? firstLegal;
            if (selectedMove == null)
                return false;

            Debug.Log(
                $"[AI Turn] Using committed card target legal fallback '{selectedMove}' for '{cardPlan.CardId}'.");
            return true;
        }

        private static int GetRuntimePieceValue(global::PieceType type)
        {
            switch (type)
            {
                case global::PieceType.Pawn:
                    return 100;
                case global::PieceType.Knight:
                case global::PieceType.Bishop:
                case global::PieceType.King:
                    return 320;
                case global::PieceType.Rook:
                    return 500;
                case global::PieceType.KnightRider:
                    return 700;
                case global::PieceType.Queen:
                case global::PieceType.Chancellor:
                    return 900;
                case global::PieceType.Amazon:
                    return 1300;
                default:
                    return 0;
            }
        }

        private static bool IsAllowedCommittedCardMove(CardUsePlan cardPlan, string uciMove)
        {
            if (!RequiresCommittedTargetMove(cardPlan))
                return true;

            if (string.IsNullOrWhiteSpace(uciMove) || uciMove.Length < 2)
                return false;

            string targetSource = cardPlan.Target.Piece?.Square.ToString();
            return !string.IsNullOrWhiteSpace(targetSource) &&
                string.Equals(uciMove.Substring(0, 2), targetSource, StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequiresCommittedTargetMove(CardUsePlan cardPlan)
        {
            if (cardPlan == null || cardPlan.Target?.Piece == null)
                return false;

            return IsEffectMoveCommitmentCard(cardPlan.CardId);
        }

        private static bool TryGetExecutableOriginalMove(
            TurnPlan originalPlan,
            global::BoardManager boardManager,
            out string uciMove)
        {
            uciMove = null;

            if (originalPlan == null ||
                originalPlan.MovePlan == null ||
                string.IsNullOrWhiteSpace(originalPlan.MovePlan.UciMove) ||
                boardManager == null)
            {
                return false;
            }

            string plannedMove = originalPlan.MovePlan.UciMove;
            if (!boardManager.IsValidUciMove(plannedMove))
                return false;

            Vector3Int from = boardManager.UCIToGrid(plannedMove.Substring(0, 2));
            Vector3Int to = boardManager.UCIToGrid(plannedMove.Substring(2, 2));
            global::Piece piece = boardManager.GetPiece(from);
            if (piece == null || !piece.CanMoveTo(boardManager, to))
            {
                Debug.LogWarning(
                    $"[AI Turn] Planned post-card move '{plannedMove}' is no longer executable. " +
                    "Using actual post-card MoveFilter instead.");
                return false;
            }

            uciMove = plannedMove;
            return true;
        }

        private static void LogSelectedPlanScoreComponents(TurnPlan selectedPlan)
        {
            if (selectedPlan == null || selectedPlan.Score == null)
                return;

            var builder = new StringBuilder();
            builder.Append("[AI Turn] Selected TurnPlan scoreComponents=");

            var components = selectedPlan.Score.Components;
            for (int i = 0; i < components.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                TurnPlanScoreComponent component = components[i];
                builder.Append(component.Code);
                builder.Append(':');
                builder.Append(component.Value);
            }

            Debug.Log(builder.ToString());
        }

        private static bool ShouldPreserveOriginalPostCardMove(TurnPlan originalPlan)
        {
            if (originalPlan == null || originalPlan.CardPlan == null)
                return false;

            if (IsImmediateMovementOverrideCard(originalPlan.CardPlan.CardId))
                return true;

            if (IsEffectMoveCommitmentCard(originalPlan.CardPlan.CardId))
                return true;

            return originalPlan.CardApplicationStatus == CardEffectApplicationStatus.Exact;
        }

        private static bool IsEffectMoveCommitmentCard(string cardId)
        {
            switch (cardId)
            {
                case "desperado":
                case "sunset_blade":
                case "giant":
                    return true;
                default:
                    return false;
            }
        }

        private UnifiedTurnPlanner CreateTurnPlanner(
            string fen,
            UciAnalysisSnapshot snapshot,
            string opponentReplyFen,
            UciAnalysisSnapshot opponentReplySnapshot)
        {
            var snapshotEngine = new FairyStockfishSnapshotEngine(
                fen,
                snapshot,
                FairyStockfishBridge.Instance.IsInCheck(),
                opponentReplyFen,
                opponentReplySnapshot);
            var moveFilter = new MoveFilter(snapshotEngine);
            var options = new TurnPlannerOptions(
                noCardMoveCandidateCount: variationCount,
                cardCandidateCount: Mathf.Clamp(cardCandidateCount, 1, AiCardHand.MaxCards),
                targetCandidateCount: Mathf.Max(1, targetCandidateCount),
                postCardMoveCandidateCount: variationCount,
                opponentReplyCandidateCount: Mathf.Max(0, opponentReplyCandidateCount),
                beamWidth: CalculatePlannerBeamWidth(),
                maximumEngineCallCount: Mathf.Max(1, maximumEngineCallCount),
                allowCoarseCardEffects: allowCoarseCardEffects);

            return new UnifiedTurnPlanner(
                moveFilter,
                cardTargetingModule,
                options: options);
        }

        private static string CreateOpponentReplyFen(GameState gameState)
        {
            BoardState board = gameState.BoardState;
            var opponentBoard = new BoardState(
                board.Pieces,
                Opponent(board.SideToMove),
                board.CastlingRights,
                board.EnPassantTarget,
                board.HalfmoveClock,
                board.FullmoveNumber);

            return FenParser.Serialize(opponentBoard);
        }

        private static ChaosChess.AI.Domain.PieceColor Opponent(ChaosChess.AI.Domain.PieceColor color)
        {
            return color == ChaosChess.AI.Domain.PieceColor.White
                ? ChaosChess.AI.Domain.PieceColor.Black
                : ChaosChess.AI.Domain.PieceColor.White;
        }

        private int CalculatePlannerBeamWidth()
        {
            int noCardCandidates = Mathf.Max(1, variationCount);
            int cardCandidates = Mathf.Clamp(cardCandidateCount, 1, AiCardHand.MaxCards);
            int postCardCandidates = Mathf.Max(1, variationCount);
            return noCardCandidates + (cardCandidates * postCardCandidates);
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

            string moveToExecute = uciMove;
            if (gameManager.TrySelectCardAwareAIMove(uciMove, out string cardAwareMove) &&
                boardManager.IsValidUciMove(cardAwareMove))
            {
                moveToExecute = cardAwareMove;
                Debug.Log($"[AI Turn] Card-aware move post-processor replaced '{uciMove}' with '{moveToExecute}'.");
            }

            if (!gameManager.IsMoveAllowedByExtraAction(moveToExecute))
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    $"{source} selected extra-action-blocked move '{moveToExecute}'.");
                return;
            }

            if (gameManager.ShouldRejectCardAwareAIMove(moveToExecute))
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    $"{source} selected card-aware rejected move '{moveToExecute}'.");
                return;
            }

            Debug.Log($"[AI Turn] Executing {source} move '{moveToExecute}'.");
            CompleteRequest(requestId);
            boardManager.ApplyUCIMove(moveToExecute);
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
            Debug.Log(
                "[AI Turn] UnifiedTurnPlanner skip trace: " +
                $"cardTargeting={trace.CardTargetingSkipCount}, cardEffect={trace.CardEffectSkipCount}, " +
                $"engineLimit={trace.EngineCallLimitSkipCount}, opponentReplyDeferred={trace.OpponentReplyDeferredCandidateCount}.");
        }

        private void LogHandTargetDiagnostics(
            AiCardHand hand,
            global::BoardManager boardManager,
            ChaosChess.AI.Domain.GameState gameState,
            ChaosChess.AI.Domain.PieceColor actor)
        {
            if (hand == null || hand.AvailableCards == null)
                return;

            foreach (GameObject cardObject in hand.AvailableCards)
            {
                global::CardData cardData = cardObject != null
                    ? cardObject.GetComponent<global::CardData>()
                    : null;
                global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;
                if (dataSO == null)
                {
                    Debug.Log("[AI Turn] Hand candidate skipped: missing CardDataSO.");
                    continue;
                }

                if (!dataSO.AiSupported || string.IsNullOrWhiteSpace(dataSO.AiCardId))
                {
                    Debug.Log(
                        $"[AI Turn] Hand candidate skipped: card={FormatUnityCard(dataSO)}, " +
                        "reason=AI metadata disabled or missing.");
                    continue;
                }

                if (targetPlanner.TryCreatePlan(
                        dataSO.AiCardId,
                        cardObject,
                        boardManager,
                        gameState,
                        actor,
                        out AiCardTargetPlan plan,
                        out AiCardExecutionStatus failureStatus,
                        out string reason))
                {
                    Debug.Log(
                        $"[AI Turn] Hand candidate target ok: card={FormatUnityCard(dataSO)}, " +
                        $"target={plan.UsePlan.Target.Kind}.");
                    continue;
                }

                Debug.Log(
                    $"[AI Turn] Hand candidate target rejected: card={FormatUnityCard(dataSO)}, " +
                    $"status={failureStatus}, reason={reason}");
            }
        }

        private static void LogPlannerCardCandidates(TurnPlannerResult result)
        {
            if (result == null)
                return;

            int cardPlanCount = 0;
            int logged = 0;
            foreach (TurnPlanCandidate candidate in result.Candidates)
            {
                if (candidate == null || !candidate.HasPlan || candidate.Plan == null || !candidate.Plan.UsesCard)
                    continue;

                cardPlanCount++;
                if (logged >= 8)
                    continue;

                TurnPlan plan = candidate.Plan;
                Debug.Log(
                    $"[AI Turn] Planner card candidate: card={plan.CardPlan?.CardId ?? "<none>"}, " +
                    $"score={plan.Score.Total}, move={plan.MovePlan?.UciMove ?? "<none>"}, " +
                    $"rank='{plan.DeterministicRankKey}'.");
                logged++;
            }

            if (cardPlanCount == 0)
            {
                Debug.Log("[AI Turn] Planner produced no executable card candidates.");
                LogPlannerSkippedCardCandidates(result);
            }
            else if (cardPlanCount > logged)
            {
                Debug.Log($"[AI Turn] Planner card candidates logged {logged}/{cardPlanCount}.");
            }
        }

        private static void LogPlannerSkippedCardCandidates(TurnPlannerResult result)
        {
            int logged = 0;
            foreach (TurnPlanCandidate candidate in result.Candidates)
            {
                if (candidate == null || candidate.HasPlan)
                    continue;

                string cardId = candidate.SkippedCardPlan != null
                    ? candidate.SkippedCardPlan.CardId
                    : "<none>";
                Debug.Log(
                    $"[AI Turn] Planner skipped candidate: " +
                    $"card={cardId}, code={candidate.SkipCode}, reason={candidate.SkipReason}, " +
                    $"cardApplication={candidate.SkippedCardApplicationStatus}/{candidate.SkippedCardApplicationCode}.");

                logged++;
                if (logged >= 12)
                    break;
            }

            if (logged == 0)
                Debug.Log("[AI Turn] Planner had no skipped candidate details.");
        }

        private TurnPlan SelectCardBiasedPlan(TurnPlannerResult result)
        {
            if (result == null || !result.HasPlan)
                return null;

            TurnPlan selectedPlan = result.SelectedPlan;
            if (selectedPlan == null)
                return selectedPlan;

            TurnPlan bestCardPlan = null;
            foreach (TurnPlanCandidate candidate in result.Candidates)
            {
                if (candidate == null || !candidate.HasPlan || candidate.Plan == null || !candidate.Plan.UsesCard)
                    continue;

                TurnPlan candidatePlan = candidate.Plan;
                if (bestCardPlan == null || candidatePlan.Score.Total > bestCardPlan.Score.Total)
                    bestCardPlan = candidatePlan;
            }

            if (bestCardPlan == null)
            {
                Debug.Log("[AI Turn] Card-biased selection found no executable card plan.");
                return selectedPlan;
            }

            int scoreGap = selectedPlan.Score.Total - bestCardPlan.Score.Total;
            if (!selectedPlan.UsesCard && bestCardPlan.Score.Total <= 0)
            {
                Debug.Log(
                    $"[AI Turn] Card-biased selection kept no-card plan because best card score is not positive. " +
                    $"noCardScore={selectedPlan.Score.Total}, cardScore={bestCardPlan.Score.Total}, " +
                    $"gap={scoreGap}, card={bestCardPlan.CardPlan?.CardId}.");
                return selectedPlan;
            }

            if (!selectedPlan.UsesCard && scoreGap >= 0)
            {
                Debug.Log(
                    $"[AI Turn] Card-biased selection kept no-card plan because card plan did not beat it. " +
                    $"noCardScore={selectedPlan.Score.Total}, cardScore={bestCardPlan.Score.Total}, " +
                    $"gap={scoreGap}, card={bestCardPlan.CardPlan?.CardId}.");
                return selectedPlan;
            }

            Debug.Log(
                $"[AI Turn] Card-biased selection chose higher-scoring card plan. " +
                $"noCardScore={selectedPlan.Score.Total}, cardScore={bestCardPlan.Score.Total}, " +
                $"gap={scoreGap}, card={bestCardPlan.CardPlan?.CardId}.");
            return bestCardPlan;
        }

        private static TurnPlan SelectExtraActionConstrainedPlan(
            TurnPlannerResult result,
            TurnPlan selectedPlan,
            global::GameManager gameManager)
        {
            if (result == null || selectedPlan == null || gameManager == null)
                return selectedPlan;

            if (selectedPlan.MovePlan != null &&
                gameManager.IsMoveAllowedByExtraAction(selectedPlan.MovePlan.UciMove))
            {
                return selectedPlan;
            }

            foreach (TurnPlanCandidate candidate in result.Candidates)
            {
                TurnPlan candidatePlan = candidate?.Plan;
                if (candidatePlan == null ||
                    candidatePlan.MovePlan == null ||
                    !gameManager.IsMoveAllowedByExtraAction(candidatePlan.MovePlan.UciMove))
                {
                    continue;
                }

                Debug.Log(
                    $"[AI Turn] Extra action constrained move selection from '{selectedPlan.MovePlan?.UciMove ?? "<none>"}' " +
                    $"to '{candidatePlan.MovePlan.UciMove}'.");
                return candidatePlan;
            }

            Debug.LogWarning(
                $"[AI Turn] Extra action had no candidate move for the locked piece. " +
                $"selected={selectedPlan.MovePlan?.UciMove ?? "<none>"}.");
            return selectedPlan;
        }

        private static bool TrySelectAwakenedUpgradeAction(
            global::BoardManager boardManager,
            global::GameManager gameManager,
            global::PieceColor actorColor,
            TurnPlan selectedPlan,
            out global::FatherEnemyEffector selectedEffector,
            out string selectedSummary,
            out int selectedScore)
        {
            selectedEffector = null;
            selectedSummary = null;
            selectedScore = int.MinValue;

            if (boardManager == null || gameManager == null)
                return false;

            IReadOnlyList<global::Piece> pieces = boardManager.GetAllPieces();
            if (pieces == null || pieces.Count == 0)
                return false;

            foreach (global::Piece piece in pieces)
            {
                if (piece == null ||
                    piece.Color != actorColor ||
                    !piece.IsAwakened)
                {
                    continue;
                }

                global::FatherEnemyEffector effector = piece.GetComponent<global::FatherEnemyEffector>();
                if (effector == null ||
                    !effector.TryGetNextUpgradeType(out global::PieceType nextType))
                {
                    continue;
                }

                string sourceSquare = boardManager.GridTOUCI(piece.Pos);
                if (!gameManager.IsMoveAllowedByExtraAction(sourceSquare + sourceSquare))
                    continue;

                int score = EstimateAwakenedUpgradeScore(piece.Type, nextType);
                string summary = $"{sourceSquare}:{piece.Type}->{nextType}";
                Debug.Log(
                    $"[AI Turn] Awakened upgrade candidate {summary}, " +
                    $"score={score}, selectedTurnPlanScore={selectedPlan?.Score?.Total ?? 0}.");

                if (score > selectedScore)
                {
                    selectedEffector = effector;
                    selectedSummary = summary;
                    selectedScore = score;
                }
            }

            if (selectedEffector == null)
                return false;

            int selectedPlanScore = selectedPlan?.Score?.Total ?? int.MinValue;
            return selectedScore > selectedPlanScore;
        }

        private static int EstimateAwakenedUpgradeScore(
            global::PieceType currentType,
            global::PieceType nextType)
        {
            int materialGain = GetAwakenedUpgradePieceValue(nextType) -
                GetAwakenedUpgradePieceValue(currentType);
            int progressionBonus = nextType == global::PieceType.Queen ? 160 : 120;
            return materialGain + progressionBonus;
        }

        private static int GetAwakenedUpgradePieceValue(global::PieceType type)
        {
            switch (type)
            {
                case global::PieceType.Pawn:
                    return 100;
                case global::PieceType.Knight:
                case global::PieceType.Bishop:
                    return 320;
                case global::PieceType.Queen:
                    return 900;
                default:
                    return 0;
            }
        }

        private void ExecuteAwakenedUpgradeAction(
            int requestId,
            global::FatherEnemyEffector effector,
            string summary,
            int score,
            Action fallbackMoveRequest)
        {
            if (!IsActiveRequest(requestId))
                return;

            if (effector == null)
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    "Selected awakened upgrade action has no active FatherEnemy effector.");
                return;
            }

            Debug.Log(
                $"[AI Turn] Executing awakened Father Enemy upgrade action {summary}, score={score}.");

            if (!effector.TryUpgradePiece())
            {
                CompleteAndRequestFallback(
                    requestId,
                    fallbackMoveRequest,
                    "Selected awakened upgrade action was no longer executable.");
                return;
            }

            if (global::BoardManager.Instance != null)
            {
                global::BoardManager.Instance.UpdateFEN();
                string upgradedFen = global::BoardManager.Instance.GetFEN();
                FairyStockfishBridge.Instance.SetPosition(upgradedFen);
                Debug.Log($"[AI Turn] Awakened upgrade state recaptured. FEN: {upgradedFen}");
            }

            CompleteRequest(requestId);
            if (global::GameManager.Instance != null &&
                global::GameManager.Instance.FinishType == global::GameResult.None &&
                !global::GameManager.Instance.IsEndGame)
            {
                global::GameManager.Instance.NextTurn(() => global::GameManager.Instance.RequestAIMove());
            }
        }

        private static bool IsImmediateMovementOverrideCard(string cardId)
        {
            switch (cardId)
            {
                case "aim":
                case "fast_march":
                case "sneak_pawn":
                case "thunderclap_flash":
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasExecutableCardPlan(TurnPlannerResult result)
        {
            if (result == null)
                return false;

            foreach (TurnPlanCandidate candidate in result.Candidates)
            {
                if (candidate != null &&
                    candidate.HasPlan &&
                    candidate.Plan != null &&
                    candidate.Plan.UsesCard)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryCreateDirectFallbackCardPlan(
            AiCardHand hand,
            global::BoardManager boardManager,
            ChaosChess.AI.Domain.GameState gameState,
            ChaosChess.AI.Domain.PieceColor actor,
            out TurnPlan plan)
        {
            plan = null;

            if (hand == null || hand.AvailableCards == null)
                return false;

            foreach (GameObject cardObject in hand.AvailableCards)
            {
                global::CardData cardData = cardObject != null
                    ? cardObject.GetComponent<global::CardData>()
                    : null;
                global::CardDataSO dataSO = cardData != null ? cardData.DataSO : null;
                if (dataSO == null ||
                    !dataSO.AiSupported ||
                    string.IsNullOrWhiteSpace(dataSO.AiCardId))
                {
                    continue;
                }

                if (!targetPlanner.TryCreatePlan(
                        dataSO.AiCardId,
                        cardObject,
                        boardManager,
                        gameState,
                        actor,
                        out AiCardTargetPlan targetPlan,
                        out AiCardExecutionStatus failureStatus,
                        out string reason))
                {
                    Debug.Log(
                        $"[AI Turn] Direct card fallback skipped: card={FormatUnityCard(dataSO)}, " +
                        $"status={failureStatus}, reason={reason}");
                    continue;
                }

                plan = new TurnPlan(
                    actor,
                    "direct-card-fallback",
                    new TurnPlanScore(0, Array.Empty<TurnPlanScoreComponent>()),
                    "direct-card|" + dataSO.AiCardId.ToLowerInvariant(),
                    CardEffectApplicationStatus.Coarse,
                    CardEffectApplicationCode.CoarseApplied,
                    targetPlan.UsePlan,
                    movePlan: null);

                Debug.Log(
                    $"[AI Turn] Direct card fallback selected '{dataSO.AiCardId}' because the planner produced no executable card candidates.");
                return true;
            }

            Debug.Log("[AI Turn] Direct card fallback found no executable Unity card target.");
            return false;
        }

        private static string FormatAvailableCards(IReadOnlyList<ChaosChess.AI.Domain.CardInfo> cards)
        {
            if (cards == null || cards.Count == 0)
                return "<none>";

            var parts = new List<string>(cards.Count);
            foreach (ChaosChess.AI.Domain.CardInfo card in cards)
            {
                if (card == null)
                    continue;

                parts.Add(card.Id + "x" + card.RemainingUses);
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "<none>";
        }

        private static string FormatUnityCard(global::CardDataSO dataSO)
        {
            if (dataSO == null)
                return "<missing>";

            string name = string.IsNullOrWhiteSpace(dataSO.CardName) ? "<unnamed>" : dataSO.CardName;
            string aiId = string.IsNullOrWhiteSpace(dataSO.AiCardId) ? "no-ai-id" : dataSO.AiCardId;
            return $"{name} [{aiId}]";
        }

        private static bool ContainsMappedCard(
            IReadOnlyList<ChaosChess.AI.Domain.CardInfo> cards,
            string cardId)
        {
            if (cards == null || string.IsNullOrWhiteSpace(cardId))
                return false;

            foreach (ChaosChess.AI.Domain.CardInfo card in cards)
            {
                if (card != null && string.Equals(card.Id, cardId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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
