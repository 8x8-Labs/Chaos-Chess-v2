using UnityEngine;

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

        effect.Apply();
    }
}

public class TimeReversalEffecter : GlobalEffector
{
    private string fen;

    protected override void OnApply()
    {
        fen = BoardManager.Instance.GetFEN();
    }

    protected override void OnRevert()
    {
        GameManager.Instance.RequestTimeReversal(
            () =>
            {
                BoardManager.Instance.DestroyPieces(BoardManager.Instance.GetAllPieces());
                BoardManager.Instance.LoadFEN(fen);

                BoardManager.Instance.RefreshMoves();

                Destroy(gameObject);
            },
            () =>
            {
                Destroy(gameObject);
            }
        );
    }
}