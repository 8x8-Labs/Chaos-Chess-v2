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
        if (tileSelector == null) tileSelector = FindFirstObjectByType<TileSelector>();
        tileSelector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        // 원격 적용에는 선택 UI가 없습니다. 기물과 목표 칸이 한 메시지로 함께 오므로
        // 단계를 나누지 않고 곧바로 처리합니다.
        // (여기서 LoadTileSelector를 부르면 상대 화면에 선택 UI가 열립니다.)
        if (args != null && args.TargetsPreselected)
        {
            ExecutePreselected(args);
            return;
        }

        if (!pieceSelected)
        {
            pawn = args.Targets[0];
            LoadTileSelector();
            pieceSelected = true;
            return;
        }
        Vector3Int target = args.TargetPos[0];

        pieceSelected = false;
        BoardManager.Instance.ForceTeleport(pawn, target, '\0', true);
    }

    /// <summary>대상이 이미 정해진 경우(원격 적용) 한 번에 실행합니다.</summary>
    private void ExecutePreselected(CardEffectArgs args)
    {
        if (args.Targets == null || args.Targets.Count == 0 ||
            args.TargetPos == null || args.TargetPos.Count == 0)
        {
            Debug.LogError("[Teleport] 기물과 목표 칸이 모두 필요한데 일부가 비어 있습니다.");
            return;
        }

        BoardManager.Instance.ForceTeleport(args.Targets[0], args.TargetPos[0], '\0', true);
    }
}
