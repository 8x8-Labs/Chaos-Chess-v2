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

    private string DefaultFEN;
    private PieceColor casterColor = PieceColor.White;
    private bool hasCasterColor;

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
            ResolveAiDecision(NewFEN);
            return;
        }

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
        AiPieceColor perspective = UnityAiColorMapper.ToAiColor(casterColor);

        AnalyzeFen(
            DefaultFEN,
            perspective,
            savedEvaluation =>
            {
                AnalyzeFen(
                    currentFen,
                    perspective,
                    currentEvaluation =>
                    {
                        if (IsFirstEvaluationBetter(savedEvaluation, currentEvaluation))
                            BoardManager.Instance.ReplacePositionFromFen(DefaultFEN);

                        Destroy(gameObject);
                    },
                    KeepCurrent);
            },
            KeepCurrent);
    }

    private static void AnalyzeFen(
        string fen,
        AiPieceColor perspective,
        System.Action<AiPositionEvaluation> onComplete,
        System.Action onError)
    {
        if (FairyStockfishBridge.Instance == null)
        {
            onError?.Invoke();
            return;
        }

        FairyStockfishBridge.Instance.AnalyzePositionAsync(
            fen,
            AnalysisDepth,
            VariationCount,
            perspective,
            (_, snapshot) =>
            {
                try
                {
                    onComplete?.Invoke(snapshot.ToPositionEvaluation());
                }
                catch
                {
                    onError?.Invoke();
                }
            },
            (_, __) => onError?.Invoke());
    }

    private void KeepCurrent()
    {
        Destroy(gameObject);
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
