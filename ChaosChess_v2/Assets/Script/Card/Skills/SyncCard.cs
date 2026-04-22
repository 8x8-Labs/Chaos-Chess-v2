using UnityEngine;

/// <summary>
/// 동기화 - 일반
/// 동기화 칸에 들어가면 반대 행의 기물이 동기화된 기물과 함께 움직이며,
/// 이 효과는 1턴 동안만 발동하고 반대 행에 기물이 없으면 사라집니다.
/// 동기화된 기물은 다른 쪽으로 이동할 경우 효과가 해제됩니다.
/// </summary>
public class SyncCard : CardData, ITileCard
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
        Vector3Int pos = args.TargetPos[0];
        Vector3Int oppo = new Vector3Int(7 - pos.x, pos.y, 0);

        SyncChild child = CreateTileEffector<SyncChild>(oppo);
        SyncEffect parent = CreateTileEffector<SyncEffect>(pos);

        child.DataSO = DataSO;
        parent.DataSO = DataSO;

        parent.child = child;
        parent.Apply();
    }
}

public class SyncEffect : TileEffector
{
    public CardDataSO DataSO;

    public SyncChild child;

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
        if (child != null) child.Revert(); // 미활성 상태에서 소멸 시 child도 정리
        Destroy(gameObject);
    }

    public override void OnPieceEnter(Piece piece)
    {
        // child가 이미 활성화 중이거나 소멸된 경우 무시
        if (child == null || child.SynchronizedPiece != null) return;

        // 반대 칸에 기물이 없으면 효과 소멸
        Piece oppoPiece = BoardManager.Instance.GetPiece(child.TilePos);
        if (oppoPiece == null)
        {
            Revert();
            return;
        }

        // child 참조를 끊어 OnRevert에서 이중 정리 방지
        SyncChild activatedChild = child;
        child = null;

        activatedChild.Activate(oppoPiece);

        // 입장 기물에 SyncFollower 부착 — 다음 이동 시 반대 기물 동기 이동
        SyncFollower follower = piece.gameObject.AddComponent<SyncFollower>();

        follower.DataSO = DataSO;

        follower.syncChild = activatedChild;
        follower.syncEffect = this;
        follower.syncTilePos = tilePos;
        follower.Init(piece, -1);
        follower.Apply();
    }
}

public class SyncChild : TileEffector
{
    public CardDataSO DataSO;

    public Piece SynchronizedPiece;
    private bool isMirroring;

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

        SynchronizedPiece = null;
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    /// <summary>동기화 기물을 설정하고 타일 감시를 시작합니다.</summary>
    public void Activate(Piece synced)
    {
        SynchronizedPiece = synced;
        Apply();
    }

    /// <summary>입장 기물의 이동 방향과 동일하게 동기화 기물을 이동합니다.</summary>
    public void MirrorMove(Vector3Int from, Vector3Int to)
    {
        if (SynchronizedPiece == null)
        {
            Revert();
            return;
        }

        Vector3Int delta = to - from;
        Vector3Int target = SynchronizedPiece.Pos + delta;

        if (!BoardManager.Instance.IsInside(target))
        {
            Revert();
            return;
        }

        isMirroring = true;
        BoardManager.Instance.ForceTeleport(SynchronizedPiece, target);
        isMirroring = false;

        Revert();
    }

    /// <summary>동기화 기물이 독립적으로 이동하면 동기화를 해제합니다.</summary>
    public override void OnPieceExit(Piece piece)
    {
        if (!isMirroring && piece == SynchronizedPiece)
            Revert();
    }
}

public class SyncFollower : PieceEffector
{
    public CardDataSO DataSO;

    public SyncChild syncChild;
    public SyncEffect syncEffect;
    public Vector3Int syncTilePos;
    private bool isReady; // 동기화 칸 입장 이동은 무시, 다음 이동부터 적용

    protected override void OnApply()
    {
        // 타일 이펙트 추가
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.SetTileEffect(syncTilePos, DataSO.EffectTileBase);

        isReady = false;
    }

    protected override void OnRevert()
    {
        // 타일 이펙트 제거
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.ClearTileEffect(syncTilePos);

        Destroy(this);
    }

    public override void OnPieceMove(Vector3Int dest)
    {
        if (!isReady)
        {
            isReady = true;
            return;
        }

        if (syncChild != null)
            syncChild.MirrorMove(syncTilePos, dest);
        if (syncEffect != null)
            syncEffect.Revert(); // 동기화 발동 후 SyncEffect 타일 제거
        Revert();
    }

    public override void OnPieceCaptured()
    {
        if (syncChild != null) syncChild.Revert();
        if (syncEffect != null) syncEffect.Revert();
        Revert();
    }
}
