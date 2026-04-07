using UnityEngine;

/// <summary>
/// 데스페라도 - 기물 전용 (고급)
/// 선택한 기물이 이번 턴 총 2번 행동할 수 있지만, 두 번째 행동 후 기물이 파괴됩니다.
/// </summary>
public class DesperadoCard : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    public void LoadPieceSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<PieceSelector>();
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        Piece piece = args.Targets[0];
        var effector = CreatePieceEffector<DesperadoEffect>(piece);
        effector.Apply();
    }
}

/// <summary>
/// 데스페라도 효과 - 기물이 처음 이동하면 추가 행동권을 부여하고,
/// 두 번째 이동 후 기물을 파괴합니다.
/// </summary>
public class DesperadoEffect : PieceEffector
{
    private int moveCount = 0;

    protected override void OnApply() { }

    protected override void OnRevert()
    {
        Destroy(this);
    }

    public override void OnPieceMove(Vector3Int dest)
    {
        moveCount++;

        if (moveCount == 1)
        {
            // 첫 번째 행동: 같은 기물로 한 번 더 움직일 수 있도록 추가 행동권 부여
            GameManager.Instance.GrantExtraPlayerAction(target);
        }
        else if (moveCount >= 2)
        {
            // 두 번째 행동: 기물 파괴
            Piece pieceToDestroy = target;
            Revert();
            BoardManager.Instance.DestroyPiece(pieceToDestroy);
        }
    }
}
