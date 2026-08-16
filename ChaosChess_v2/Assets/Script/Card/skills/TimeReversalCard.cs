using System;
using System.Collections;
using UnityEngine;
using ChaosChess.Unity.AIIntegration.Engine;
using ChaosChess.Unity.AIIntegration.Mapping;
using AiPieceColor = ChaosChess.AI.Domain.PieceColor;
using AiPositionEvaluation = ChaosChess.AI.Domain.PositionEvaluation;

/// <summary>
/// 시간 역행 - 전역
/// 이 카드 사용 시 현재 판의 상태를 저장합니다.
/// 8턴 후 이 상태로 돌아올지 결정할 수 있습니다.
/// </summary>
public class TimeReversalCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        TimeReversalEffecter effect = CreateGlobalEffector<TimeReversalEffecter>();
        PieceColor casterColor = args != null
            ? args.ResolveCasterColor()
            : CardEffectArgs.ResolveDefaultCasterColor();
        effect.SetCasterColor(casterColor);

        effect.Apply();
    }
}

public class TimeReversalEffecter : GlobalEffector
{
    private const int AnalysisDepth = 3;
    private const int VariationCount = 1;
    private const float AnalysisTimeoutSeconds = 12f;
    private const float EngineReadyTimeoutSeconds = 3f;

    private string DefaultFEN;
    private PieceColor casterColor = PieceColor.White;
    private bool hasCasterColor;
    private bool aiDecisionFinished;

    public void SetCasterColor(PieceColor color)
    {
        casterColor = color;
        hasCasterColor = true;
    }

    protected override void OnApply()
    {
        DefaultFEN = BoardManager.Instance.GetFEN();
    }

    protected override void OnRevert()
    {
        string NewFEN = BoardManager.Instance.GetFEN();

        if (ShouldResolveForAiCaster())
        {
            StartCoroutine(ResolveAiDecisionWhenEngineReady(NewFEN));
            return;
        }

        LogAiResolveConditionFailed(NewFEN);

        BoardManager.Instance.ReplacePositionFromFen(DefaultFEN);

        GameManager.Instance.RequestTimeReversal(
            () =>
            {
                Destroy(gameObject);
            },
            () =>
            {
                Destroy(gameObject);
                BoardManager.Instance.ReplacePositionFromFen(NewFEN);
            }
        );
    }

    protected override void OnCancel()
    {
        Destroy(gameObject);
    }

    private bool ShouldResolveForAiCaster()
    {
        return hasCasterColor
            && GameManager.Instance != null
            && GameManager.Instance.AiAutoMoveEnabled
            && casterColor == GameManager.Instance.EnemyColor;
    }

    private void ResolveAiDecision(string currentFen)
    {
        LogAiDecision("ResolveAiDecision start", currentFen);

        try
        {
            AiPieceColor perspective = UnityAiColorMapper.ToAiColor(casterColor);

            AnalyzeFen(
                "saved FEN",
                DefaultFEN,
                currentFen,
                perspective,
                savedEvaluation =>
                {
                    AnalyzeFen(
                        "current FEN",
                        currentFen,
                        currentFen,
                        perspective,
                        currentEvaluation =>
                        {
                            bool shouldRevert = IsFirstEvaluationBetter(savedEvaluation, currentEvaluation);
                            FinishAiDecision(shouldRevert, currentFen, "analysis complete");
                        },
                        error => KeepCurrent(currentFen, error));
                },
                error => KeepCurrent(currentFen, error));
        }
        catch (Exception e)
        {
            KeepCurrent(currentFen, "ResolveAiDecision exception: " + e.Message);
        }
    }

    private IEnumerator ResolveAiDecisionWhenEngineReady(string currentFen)
    {
        LogAiDecision("ResolveAiDecision queued", currentFen);

        yield return null;

        float deadline = Time.realtimeSinceStartup + EngineReadyTimeoutSeconds;
        while (FairyStockfishBridge.Instance != null
            && FairyStockfishBridge.Instance.IsBusy
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (FairyStockfishBridge.Instance != null && FairyStockfishBridge.Instance.IsBusy)
        {
            KeepCurrent(currentFen, $"Engine stayed busy for {EngineReadyTimeoutSeconds:0.##} seconds before AI decision.");
            yield break;
        }

        ResolveAiDecision(currentFen);
    }

    private void AnalyzeFen(
        string phase,
        string fen,
        string currentFen,
        AiPieceColor perspective,
        Action<AiPositionEvaluation> onComplete,
        Action<string> onError)
    {
        LogAiDecision(phase + " analysis start", currentFen);

        if (FairyStockfishBridge.Instance == null)
        {
            string error = "FairyStockfishBridge.Instance is null.";
            LogAiDecision(phase + " analysis failed: " + error, currentFen);
            onError?.Invoke(error);
            return;
        }

        if (FairyStockfishBridge.Instance.IsBusy)
        {
            string error = "FairyStockfishBridge is busy.";
            LogAiDecision(phase + " analysis failed: " + error, currentFen);
            onError?.Invoke(error);
            return;
        }

        bool settled = false;
        Coroutine timeoutCoroutine = StartCoroutine(AnalyzeFenTimeout(
            currentFen,
            () =>
            {
                if (settled) return;

                settled = true;
                string error = $"Timed out after {AnalysisTimeoutSeconds:0.##} seconds.";
                LogAiDecision(phase + " analysis failed: " + error, currentFen);
                onError?.Invoke(error);
            }));

        try
        {
            FairyStockfishBridge.Instance.AnalyzePositionAsync(
                fen,
                AnalysisDepth,
                VariationCount,
                perspective,
                (_, snapshot) =>
                {
                    if (this == null || settled) return;

                    settled = true;
                    if (timeoutCoroutine != null)
                        StopCoroutine(timeoutCoroutine);

                    try
                    {
                        LogAiDecision(phase + " analysis complete", currentFen);
                        onComplete?.Invoke(snapshot.ToPositionEvaluation());
                    }
                    catch (Exception e)
                    {
                        string error = phase + " completion exception: " + e.Message;
                        LogAiDecision(phase + " analysis failed: " + error, currentFen);
                        onError?.Invoke(error);
                    }
                },
                (_, error) =>
                {
                    if (this == null || settled) return;

                    settled = true;
                    if (timeoutCoroutine != null)
                        StopCoroutine(timeoutCoroutine);

                    string message = string.IsNullOrWhiteSpace(error) ? "Analysis failed." : error;
                    LogAiDecision(phase + " analysis failed: " + message, currentFen);
                    onError?.Invoke(message);
                });
        }
        catch (Exception e)
        {
            if (settled) return;

            settled = true;
            if (timeoutCoroutine != null)
                StopCoroutine(timeoutCoroutine);

            string error = phase + " request exception: " + e.Message;
            LogAiDecision(phase + " analysis failed: " + error, currentFen);
            onError?.Invoke(error);
        }
    }

    private IEnumerator AnalyzeFenTimeout(string currentFen, Action onTimeout)
    {
        yield return new WaitForSecondsRealtime(AnalysisTimeoutSeconds);

        if (this == null)
            yield break;

        onTimeout?.Invoke();
    }

    private void FinishAiDecision(bool revertToSavedFen, string currentFen, string reason)
    {
        if (aiDecisionFinished) return;
        aiDecisionFinished = true;

        try
        {
            if (revertToSavedFen)
            {
                BoardManager.Instance.ReplacePositionFromFen(DefaultFEN);
                SyncBoardAndEngine();
                LogAiDecision("ResolveAiDecision complete: RevertToSavedFen (" + reason + ")", currentFen);
            }
            else
            {
                SyncBoardAndEngine();
                LogAiDecision("ResolveAiDecision complete: KeepCurrent (" + reason + ")", currentFen);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[TimeReversal][AI] FinishAiDecision failed. "
                + $"casterColor={casterColor}, DefaultFEN={DefaultFEN}, currentFen={currentFen}, error={e.Message}");
        }

        Destroy(gameObject);
    }

    private void KeepCurrent(string currentFen, string reason)
    {
        FinishAiDecision(false, currentFen, reason);
    }

    private static void SyncBoardAndEngine()
    {
        if (BoardManager.Instance == null)
            return;

        BoardManager.Instance.UpdateFEN();

        if (FairyStockfishBridge.Instance != null)
        {
            FairyStockfishBridge.Instance.SetPosition(BoardManager.Instance.GetFEN());

            if (FairyStockfishBridge.Instance.IsBusy)
                return;
        }

        BoardManager.Instance.RefreshMoves();
    }

    private void LogAiResolveConditionFailed(string currentFen)
    {
        Debug.Log(
            "[TimeReversal][AI] ShouldResolveForAiCaster=false; using player UI path. "
            + $"hasCasterColor={hasCasterColor}, casterColor={casterColor}, "
            + $"aiAutoMoveEnabled={GameManager.Instance != null && GameManager.Instance.AiAutoMoveEnabled}, "
            + $"enemyColor={(GameManager.Instance != null ? GameManager.Instance.EnemyColor.ToString() : "null")}, "
            + $"DefaultFEN={DefaultFEN}, currentFen={currentFen}");
    }

    private void LogAiDecision(string message, string currentFen)
    {
        Debug.Log(
            "[TimeReversal][AI] " + message + ". "
            + $"casterColor={casterColor}, DefaultFEN={DefaultFEN}, currentFen={currentFen}");
    }

    private static bool IsFirstEvaluationBetter(AiPositionEvaluation first, AiPositionEvaluation second)
    {
        return ToComparableScore(first) > ToComparableScore(second);
    }

    private static int ToComparableScore(AiPositionEvaluation evaluation)
    {
        if (evaluation.MateIn.HasValue)
        {
            int mateIn = evaluation.MateIn.Value;
            return mateIn > 0
                ? 100000 - mateIn
                : -100000 - mateIn;
        }

        return evaluation.ScoreCentipawns ?? 0;
    }
}
