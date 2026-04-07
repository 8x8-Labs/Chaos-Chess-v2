using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무하한 카드
/// 반경 1칸 내로 어떠한 기물도 접근/이동할 수 없습니다. 
/// (이 기믹이 적용된 기물은 움직일 수 없습니다.) ← 무제한, 상대/아군이 들어오려고 시도하면 수를 취소하고 해제.
/// </summary>
public class LimitlessCard : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    public void LoadPieceSelector()
    {
        if (selector == null)
            selector = FindFirstObjectByType<PieceSelector>();

        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        Piece piece = args.Targets[0];

        // 기물 효과 적용 (움직임 제한 등)
        var pieceEffector = CreatePieceEffector<LimitlessPieceEffector>(piece);
        pieceEffector.Apply();

        // 3x3 필드 생성
        CreateLimitlessField(piece, pieceEffector);
    }

    private void CreateLimitlessField(Piece center, LimitlessPieceEffector pieceEff)
    {
        Vector3Int pos = center.Pos;

        var controller = new LimitlessFieldController(pieceEff);

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                Vector3Int tile = new Vector3Int(pos.x + dx, pos.y + dy, 0);

                if (!BoardManager.Instance.IsInside(tile))
                    continue;

                GameObject obj = new GameObject("LimitlessTile");
                var eff = obj.AddComponent<LimitlessTileEffector>();

                eff.Init(tile, -1);
                eff.SetController(controller);
                eff.Apply();

                controller.tiles.Add(eff);
            }
        }
    }
}

public class LimitlessPieceEffector : PieceEffector
{
    protected override void OnApply()
    {
        target.FenOverride = "a";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        target.FenOverride = null;
        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }
}

/// <summary>
/// 무하한이 적용된 3x3타일들을 관리합니다
/// </summary>
public class LimitlessFieldController
{
    public List<LimitlessTileEffector> tiles = new();
    private LimitlessPieceEffector pieceEff;

    private bool broken = false;

    public LimitlessFieldController(LimitlessPieceEffector pieceEff)
    {
        this.pieceEff = pieceEff;
    }

    public void Break()
    {
        if (broken) return;
        broken = true;

        // 1. 타일 제거
        foreach (var t in tiles)
        {
            if (t != null)
                t.Revert();
        }
        tiles.Clear();

        // 2. 기물 효과 제거
        if (pieceEff != null)
            pieceEff.Revert();
    }
}

public class LimitlessTileEffector : TileEffector
{
    private LimitlessFieldController controller;

    public void SetController(LimitlessFieldController controller)
    {
        this.controller = controller;
    }

    protected override void OnApply()
    {
        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    public override bool CanPieceEnter(Piece piece, Vector3Int from, Vector3Int to)
    {
        // 진입 시도 → 필드 전체 제거
        controller?.Break();

        return false; // 이동 차단
    }
}