using UnityEngine;

/// <summary>
/// 포탈 - 타일 전용 (고급)
/// 2칸을 선택해서 포탈로 연결합니다.
/// 포탈은 최대 2회 시전자만 이용할 수 있으며 한쪽에 들어가면 반대쪽에서 소환됩니다.
/// </summary>
public class PortalSkill : CardData, ITileCard
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
        PortalEffect portalA = CreateTileEffector<PortalEffect>(args.TargetPos[0]);
        PortalEffect portalB = CreateTileEffector<PortalEffect>(args.TargetPos[1]);

        portalA.Dest = portalB;
        portalB.Dest = portalA;

        // 사용 가능 횟수 : 2
        var sharedUses = new SharedCounter(2);
        portalA.SharedUses = sharedUses;
        portalB.SharedUses = sharedUses;

        PieceColor casterColor = GameManager.Instance.PlayerColor;
        portalA.CasterColor = casterColor;
        portalB.CasterColor = casterColor;

        portalA.DataSO = DataSO;
        portalB.DataSO = DataSO;

        portalA.Apply();
        portalB.Apply();
    }
}

public class PortalEffect : TileEffector
{
    public CardDataSO DataSO;

    public PortalEffect Dest;
    public SharedCounter SharedUses;
    public PieceColor CasterColor;

    protected override void OnApply()
    {
        // 타일 이펙트 추가
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.SetTileEffect(tilePos, DataSO.EffectTileBase);

        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        // 타일 이펙트 제거
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.ClearTileEffect(tilePos);

        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    public override void OnPieceEnter(Piece piece)
    {
        if (piece.Color != CasterColor) return;
        if (SharedUses.Uses <= 0) return;
        if (Dest == null) return;

        SharedUses.Uses--;

        BoardManager.Instance.ForceTeleport(piece, Dest.tilePos);

        if (SharedUses.Uses <= 0)
        {
            PortalEffect dest = Dest;
            Dest = null;
            dest.Dest = null;
            dest.Revert();
            Revert();
        }
    }
}

public struct SharedCounter
{
    public int Uses;
    public SharedCounter(int initalUses) { Uses = initalUses; }
}