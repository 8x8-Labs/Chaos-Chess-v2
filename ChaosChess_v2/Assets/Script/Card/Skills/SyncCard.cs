using UnityEngine;

/// <summary>
/// 동기화 - 일반
/// 동기화 칸에 들어가면 반대 행의 기물이 동기화된 기물과 함께 움직이며,
/// 이 효과는 1턴 동안만 발동하고 반대 행에 기물이 없으면 사라집니다.
/// 동기화된 기물은 다른 쪽으로 이동할 경우 효과가 해제됩니다.
/// </summary>
public class SyncCard : CardData, ITileCard
{
    [SerializeField] private SyncLinkLineSettings linkLineSettings = SyncLinkLineSettings.Default;
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
        child.CardSO = null;

        child.DataSO = DataSO;
        parent.LinkLineSettings = linkLineSettings;
        parent.DataSO = DataSO;

        parent.child = child;
        parent.Apply();
    }
}

public class SyncEffect : TileEffector
{
    public CardDataSO DataSO;
    public SyncLinkLineSettings LinkLineSettings;

    public SyncChild child;
    private SyncLinkLine syncLine;

    protected override void OnApply()
    {
        // 타일 이펙트 추가
        if (DataSO.NeedEffectTileBase)
        {
            BoardManager.Instance.TileEffectDrawer.SetTileEffect(tilePos, DataSO.EffectTileBase);
            if (child != null)
                BoardManager.Instance.TileEffectDrawer.SetTileEffect(child.TilePos, DataSO.EffectTileBase);
        }

        if (LinkLineSettings.Enabled && child != null)
            syncLine = SyncLinkLine.Create(transform, tilePos, child.TilePos, LinkLineSettings);

        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        // 타일 이펙트 제거
        if (DataSO.NeedEffectTileBase)
        {
            BoardManager.Instance?.TileEffectDrawer?.ClearTileEffect(tilePos);
            if (child != null)
                BoardManager.Instance?.TileEffectDrawer?.ClearTileEffect(child.TilePos);
        }

        DestroySyncLine();
        BoardManager.Instance?.UnregisterTileEffector(tilePos, this);
        if (child != null)
        {
            child.Revert();
            Destroy(child.gameObject);
        }
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
        follower.syncTilePos = tilePos;
        follower.Init(piece, -1);

        Revert();
        follower.Apply();
    }

    private void DestroySyncLine()
    {
        if (syncLine != null)
            Destroy(syncLine.gameObject);

        syncLine = null;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        DestroySyncLine();
    }
}

public class SyncChild : TileEffector
{
    public CardDataSO DataSO;

    public Piece SynchronizedPiece;
    private bool isMirroring;

    protected override void OnApply()
    {
        Piece.OnPieceDestroyed += HandlePieceDestroyed;

        // 타일 이펙트 추가
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.SetTileEffect(tilePos, DataSO.EffectTileBase);

        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        Piece.OnPieceDestroyed -= HandlePieceDestroyed;

        // 타일 이펙트 제거
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance?.TileEffectDrawer?.ClearTileEffect(tilePos);

        SynchronizedPiece = null;
        BoardManager.Instance?.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    private void HandlePieceDestroyed(Piece piece)
    {
        if (piece == SynchronizedPiece) Revert();
    }

    // Revert()를 거치지 않고 오브젝트가 파괴되는 예외적 경로 대비
    protected override void OnDestroy()
    {
        base.OnDestroy();
        Piece.OnPieceDestroyed -= HandlePieceDestroyed;
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
            BoardManager.Instance?.TileEffectDrawer?.ClearTileEffect(syncTilePos);

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
        Revert();
    }

    public override void OnPieceCaptured()
    {
        if (syncChild != null) syncChild.Revert();
        Revert();
    }
}

public class SyncLinkLine : MonoBehaviour
{
    private Material lineMaterial;

    public static SyncLinkLine Create(Transform parent, Vector3Int from, Vector3Int to, SyncLinkLineSettings settings)
    {
        GameObject lineObject = new GameObject("SyncLinkLine");
        lineObject.transform.SetParent(parent, false);

        SyncLinkLine line = lineObject.AddComponent<SyncLinkLine>();
        line.Init(from, to, settings);
        return line;
    }

    private void Init(Vector3Int from, Vector3Int to, SyncLinkLineSettings settings)
    {
        LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = settings.Width;
        lineRenderer.endWidth = settings.Width;
        lineRenderer.numCapVertices = 4;
        lineRenderer.sortingOrder = settings.SortingOrder;

        Shader shader = settings.LineShader != null ? settings.LineShader : Shader.Find("Sprites/Default");
        if (shader != null)
        {
            lineMaterial = new Material(shader);
            lineRenderer.material = lineMaterial;
        }
        else
        {
            Debug.LogError("SyncLinkLine: line shader is missing.");
        }

        lineRenderer.startColor = settings.Color;
        lineRenderer.endColor = settings.Color;

        Vector3 start = BoardManager.Instance.GridPosToWorldPos(from);
        Vector3 end = BoardManager.Instance.GridPosToWorldPos(to);
        Vector3 direction = end - start;
        float distance = direction.magnitude;

        if (distance > 0f)
        {
            Vector3 cellOffset = BoardManager.Instance.GridPosToWorldPos(from + Vector3Int.right) - start;
            float halfCell = cellOffset.magnitude * 0.5f;
            Vector3 edgeOffset = direction.normalized * Mathf.Min(halfCell, distance * 0.5f);

            start += edgeOffset;
            end -= edgeOffset;
        }

        start.z = settings.Z;
        end.z = settings.Z;

        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
            Destroy(lineMaterial);
    }
}

[System.Serializable]
public struct SyncLinkLineSettings
{
    public bool Enabled;
    public Color Color;
    [Range(0.01f, 0.2f)]
    public float Width;
    public int SortingOrder;
    public float Z;
    public Shader LineShader;

    public static SyncLinkLineSettings Default => new SyncLinkLineSettings
    {
        Enabled = true,
        Color = new Color(0.35f, 0.85f, 1f, 0.85f),
        Width = 0.045f,
        SortingOrder = -1,
        Z = -0.1f,
        LineShader = null
    };
}
