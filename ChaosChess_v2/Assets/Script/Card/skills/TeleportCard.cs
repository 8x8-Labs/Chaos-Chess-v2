using UnityEngine;

/// <summary>
/// 텔레포트 - 기물 전용
/// 폰 기물이 선택한 비어있는 칸으로 이동합니다.
/// 카드 사용 시 턴 사용, 프로모션 칸으로 이동 불가.
/// </summary>
public class TeleportCard : CardData, IPieceCard, ITileCard
{
    private PieceSelector pieceSelector;
    private TileSelector tileSelector;
    private bool pieceSelected = false;
    private Piece pawn;

    private void Awake()
    {
        pieceSelector = FindFirstObjectByType<PieceSelector>();
        tileSelector = FindFirstObjectByType<TileSelector>();
    }

    public void LoadPieceSelector()
    {
        pieceSelected = false;
        if (pieceSelector == null) pieceSelector = FindFirstObjectByType<PieceSelector>();
        pieceSelector.EnableSelector(this);
    }
    public void LoadTileSelector()
    {
        if(tileSelector == null) tileSelector = FindFirstObjectByType<TileSelector>();
        tileSelector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        if (!pieceSelected)
        {
            pawn = args.Targets[0];
            LoadTileSelector();
            pieceSelected = true;
            return;
        }
        Vector3Int target = args.TargetPos[0];

        BoardManager.Instance.ForceTeleport(pawn, target, '\0', true);
    }

}
