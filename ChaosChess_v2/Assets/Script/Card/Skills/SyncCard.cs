using UnityEngine;

/// <summary>
/// 동기화 - 일반
/// 선택한 칸과 반대 행 같은 열의 칸을 동기화 칸으로 만듭니다.
/// 두 칸에 기물이 모두 있으면 어느 쪽이 먼저 이동하든 다른 기물이 동일하게 이동하며,
/// 이 효과는 한 번 발동한 후 사라집니다.
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
        child.parent = parent;
        child.Apply();
        parent.Apply();
    }
}

public class SyncEffect : TileEffector
{
    public CardDataSO DataSO;
    public SyncLinkLineSettings LinkLineSettings;

    public SyncChild child;
    private SyncLinkLine syncLine;
    private bool isMirroring;
    private bool isResolving;

    protected override void OnApply()
    {
        ShowTileEffect(DataSO);

        if (LinkLineSettings.Enabled && child != null)
            syncLine = SyncLinkLine.Create(transform, tilePos, child.TilePos, LinkLineSettings);

        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance?.TileEffectDrawer?.ClearTileEffect(tilePos);

        DestroySyncLine();
        BoardManager.Instance?.UnregisterTileEffector(tilePos, this);

        if (child != null)
        {
            child.Revert();
            child = null;
        }

        Destroy(gameObject);
    }

    public override void OnPieceExit(Piece piece)
    {
        if (child != null)
            TryBeginSynchronizedMove(piece, tilePos, child.TilePos);
    }

    public void OnLinkedTileExit(Piece piece)
    {
        if (child == null) return;
        TryBeginSynchronizedMove(piece, child.TilePos, tilePos);
    }

    private void TryBeginSynchronizedMove(Piece movingPiece, Vector3Int startPos, Vector3Int linkedTile)
    {
        if (isMirroring || isResolving || child == null) return;

        Piece linkedPiece = BoardManager.Instance.GetPiece(linkedTile);
        if (linkedPiece == null)
        {
            Revert();
            return;
        }

        isResolving = true;
        SyncMoveTrigger trigger = movingPiece.gameObject.AddComponent<SyncMoveTrigger>();
        trigger.Init(movingPiece, -1);
        trigger.parent = this;
        trigger.linkedPiece = linkedPiece;
        trigger.startPos = startPos;
        trigger.Apply();
    }

    public void CompleteSynchronizedMove(Piece linkedPiece, Vector3Int startPos, Vector3Int destination)
    {
        if (!isResolving) return;
        isResolving = false;

        if (linkedPiece == null)
        {
            return;
        }

        Vector3Int target = linkedPiece.Pos + (destination - startPos);
        if (BoardManager.Instance.IsInside(target))
        {
            isMirroring = true;
            BoardManager.Instance.ForceTeleport(linkedPiece, target);
            isMirroring = false;
        }

        Revert();
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
    public SyncEffect parent;

    protected override void OnApply()
    {
        ShowTileEffect(DataSO);
        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance?.TileEffectDrawer?.ClearTileEffect(tilePos);

        BoardManager.Instance?.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    public override void OnPieceExit(Piece piece)
    {
        if (parent != null)
            parent.OnLinkedTileExit(piece);
    }
}

public class SyncMoveTrigger : PieceEffector
{
    public SyncEffect parent;
    public Piece linkedPiece;
    public Vector3Int startPos;

    protected override void OnApply() { }

    protected override void OnRevert()
    {
        Destroy(this);
    }

    public override void OnPieceMove(Vector3Int dest)
    {
        if (parent != null)
            parent.CompleteSynchronizedMove(linkedPiece, startPos, dest);

        Revert();
    }

    public override void OnPieceCaptured()
    {
        if (parent != null)
            parent.Revert();

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
