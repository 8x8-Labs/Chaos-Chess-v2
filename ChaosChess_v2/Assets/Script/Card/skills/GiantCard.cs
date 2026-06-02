using UnityEngine;

/// <summary>
/// 거대화 - 기물 전용 (무력화)
/// 기물 이동 시 1칸 반경의 기물들을 1턴 동안 무력화시킵니다.
/// 무력화 시 카드 적용은 되지만 이동할 수는 없습니다.
/// </summary>
public class GiantCard : CardData, IPieceCard
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
        var effector = CreatePieceEffector<GiantEffector>(piece);
        effector.Apply();
    }
}
public class GiantEffector : PieceEffector
{
    protected override void OnApply()
    {
    }
    int[] dx = { -1, -1, -1, 0, 1, 1, 1, 0 };
    int[] dy = { -1, 0, 1, 1, 1, 0, -1, -1 };

    public override void OnPieceMove(Vector3Int dest)
    {
        for(int i = 0; i < 8; i++)
        {
            int x = dx[i];
            int y = dy[i];
            Vector3Int pos = new Vector3Int(dest.x + x, dest.y + y, dest.z);
            if (BoardManager.Instance.IsInside(pos))
            {
                Piece target = BoardManager.Instance.GetPiece(pos);
                if (target != null)
                {
                    if (PieceEffector.HasActiveMovementOverride(target))
                        continue;

                    GiantStunEffector stun = target.gameObject.AddComponent<GiantStunEffector>();
                    stun.CardSO = null;
                    stun.Init(target, 1);
                    stun.Apply();
                }
            }
        }

        Revert();
    }
    protected override void OnRevert()
    {
        Destroy(this);
    }

}

public class GiantStunEffector : PieceEffector, IMovementOverrideEffect
{
    protected override void OnApply()
    {
        target.MoveFenOverride = "a";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        if (target.MoveFenOverride?.ToLower() == "a")
            target.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }
}
