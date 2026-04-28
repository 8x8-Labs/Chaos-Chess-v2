using UnityEngine;

/// <summary>
/// 점프대 - 타일전용
/// 선택한 칸을 점프대로 지정합니다. 
/// 점프대에 올라가면 이동한 방향으로 두칸 점프하고 그 위치에 있는 기물을 자신의 기물이라도 처치합니다. 
/// 이후 칸 효과가 해제됩니다.
/// 만약 두칸 점프가 불가능하다면 점프를 하지 않습니다.
/// </summary>
public class JumpingPlatformCard : CardData, ITileCard
{
    private TileSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<TileSelector>();
    }

    public void LoadTileSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<TileSelector>();
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        JumpingPlatformEffect effect = CreateTileEffector<JumpingPlatformEffect>(args.TargetPos[0]);

        effect.Apply();
    }
}

public class JumpingPlatformEffect : TileEffector
{
    protected override void OnApply()
    {
        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    public override void OnPieceEnter(Piece piece)
    {
        Vector3Int current = piece.Pos;
        Vector3Int from = piece.PrevPos;

        int dx = current.x - from.x;
        int dy = current.y - from.y;

        int adx = Mathf.Abs(dx);
        int ady = Mathf.Abs(dy);

        Vector3Int dir;

        if (adx > ady)
            dir = new Vector3Int(dx > 0 ? 1 : -1, 0, 0);
        else if (ady > adx)
            dir = new Vector3Int(0, dy > 0 ? 1 : -1, 0);
        else
            dir = new Vector3Int(dx > 0 ? 1 : -1, dy > 0 ? 1 : -1, 0);

        Vector3Int jumpTarget = current + dir * 2;

        if (!BoardManager.Instance.IsInside(jumpTarget))
        {
            Revert();
            return;
        }

        BoardManager.Instance.ForceTeleport(piece, jumpTarget);

        Revert();
    }
}