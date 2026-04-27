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
    private string DefaultFEN;

    protected override void OnApply()
    {
        DefaultFEN = BoardManager.Instance.GetFEN();
    }

    protected override void OnRevert()
    {
        string NewFEN = BoardManager.Instance.GetFEN();

        BoardManager.Instance.DestroyPieces(BoardManager.Instance.GetAllPieces(), false);
        BoardManager.Instance.LoadFEN(DefaultFEN);

        BoardManager.Instance.RefreshMoves();

        GameManager.Instance.RequestTimeReversal(
            () =>
            {
                Destroy(gameObject);
            },
            () =>
            {
                Destroy(gameObject);
                BoardManager.Instance.DestroyPieces(BoardManager.Instance.GetAllPieces());
                BoardManager.Instance.LoadFEN(NewFEN);

                BoardManager.Instance.RefreshMoves();

            }
        );
    }
}