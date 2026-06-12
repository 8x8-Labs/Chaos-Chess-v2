using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 차원 불안 - 기물 전용 (레어)
/// 나이트에 적용 가능, 이동 시 가상의 나이트가 이동합니다.
/// 나이트가 잡힐 시 가상의 나이트의 위치로 이동합니다.
/// </summary>
public class DimensionInstabilityCard : CardData, IPieceCard
{
    [SerializeField] private Material ghostMaterial;

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
        if (args == null || args.Targets == null || args.Targets.Count == 0) return;
        Piece piece = args.Targets[0];
        if (piece == null) return;

        var effector = CreatePieceEffector<DimensionInstabilityEffector>(piece);
        effector.GhostMaterial = ghostMaterial;
        effector.Apply();
    }
}

public class DimensionInstabilityEffector : PieceEffector
{
    /// <summary>나이트 스프라이트의 인덱스 (Piece.CharToIndex의 'n').</summary>
    private const int KnightSpriteIndex = 3;
    /// <summary>가상 나이트 비주얼의 반투명도.</summary>
    private const float GhostAlpha = 0.45f;

    /// <summary>가상 나이트 스프라이트에 적용할 머티리얼 (카드에서 주입, null이면 기본 머티리얼).</summary>
    public Material GhostMaterial { get; set; }

    private Vector3Int ghostPos;
    private bool hasGhost;

    private GameObject ghostObject;
    private SpriteRenderer ghostRenderer;

    protected override void OnApply()
    {
        hasGhost = false;
    }

    public override void OnPieceMove(Vector3Int dest)
    {
        hasGhost = TryMoveGhost(target.PrevPos, dest);
        if (hasGhost) ShowGhost(ghostPos);
    }

    public override void OnPieceCaptured()
    {
        if (hasGhost && BoardManager.Instance.IsInside(ghostPos) && BoardManager.Instance.IsEmpty(ghostPos))
        {
            BoardManager.Instance.ChangePiece(ghostPos, target.Color, 'n');
        }

        Revert();
    }

    protected override void OnRevert()
    {
        if (ghostObject != null) Destroy(ghostObject);
        ghostObject = null;
        ghostRenderer = null;
        Destroy(this);
    }

    /// <summary>OnRevert를 거치지 않고 파괴될 경우(타겟 기물 파괴 등)에도 가상 나이트가 씬에 남지 않도록 정리합니다.</summary>
    private void OnDestroy()
    {
        if (ghostObject != null) Destroy(ghostObject);
    }

    /// <summary>가상 나이트 비주얼을 생성(최초)하거나 이동시킵니다.</summary>
    private void ShowGhost(Vector3Int pos)
    {
        Vector3 worldPos = BoardManager.Instance.GridPosToWorldPos(pos);

        if (ghostObject == null)
        {
            ghostObject = new GameObject("DimensionGhostKnight");
            ghostRenderer = ghostObject.AddComponent<SpriteRenderer>();

            if (GhostMaterial != null) ghostRenderer.sharedMaterial = GhostMaterial;

            // 실제 기물과 같은 정렬 레이어에 두되, 상대 기물 위에 있어도 잘 보이도록
            // sortingOrder를 한 단계 올려 항상 기물 위에 그린다.
            SpriteRenderer src = target.GetComponent<SpriteRenderer>();
            if (src != null)
            {
                ghostRenderer.sortingLayerID = src.sortingLayerID;
                ghostRenderer.sortingOrder = src.sortingOrder + 1;
            }
            else
            {
                ghostRenderer.sortingOrder = 1;
            }
            ghostObject.transform.localScale = target.transform.localScale;
        }

        GameManager gm = GameManager.Instance;
        ghostRenderer.sprite = target.Color == PieceColor.White
            ? gm.WhiteSprites[KnightSpriteIndex]
            : gm.BlackSprites[KnightSpriteIndex];
        ghostRenderer.color = new Color(1f, 1f, 1f, GhostAlpha);

        ghostObject.transform.position = worldPos;
    }

    private bool TryMoveGhost(Vector3Int origin, Vector3Int realDestination)
    {
        List<Vector3Int> cells = GetMovableCells(origin);
        cells.Remove(realDestination);
        if (cells.Count == 0)
        {
            ghostPos = realDestination;
            return true;
        }

        ghostPos = cells[Random.Range(0, cells.Count)];
        return true;
    }

    private List<Vector3Int> GetMovableCells(Vector3Int origin)
    {
        List<Vector3Int> result = new List<Vector3Int>();
        int[] dx = { -2, -2, -1, 1, 2, 2, 1, -1 };
        int[] dy = { -1, 1, 2, 2, 1, -1, -2, -2 };
        for (int i = 0; i < 8; i++)
        {
            int x = dx[i];
            int y = dy[i];

            Vector3Int candidate = new Vector3Int(origin.x + x, origin.y + y, origin.z);

            if (!BoardManager.Instance.IsInside(candidate)) continue;

            Piece piece = BoardManager.Instance.GetPiece(candidate);
            if (piece == null || piece.Color != target.Color)
            {
                result.Add(candidate);
            }
        }

        return result;
    }
}
