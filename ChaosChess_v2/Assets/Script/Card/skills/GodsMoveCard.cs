using UnityEngine;
/// <summary>
/// 신의 수 - 기물 전용 (레어, 승격)
/// 선택 기물을 한 단계 위로 승격시킨다. 
/// </summary>
public class GodsMoveCard : CardData, IPieceCard
{
    private PieceSelector selector;
    public void LoadPieceSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<PieceSelector>();
        selector.EnableSelector(this);
    }
    public void Execute(CardEffectArgs args = null)
    {
        Piece piece = args.Targets[0];
        char change;
        switch (piece.Type)
        {
            case PieceType.Pawn:
                change = 'n';
                break;
            case PieceType.Knight:
                change = 'r';
                break;
            case PieceType.Bishop:
                change = 'r';
                break;
            case PieceType.Rook:
                change = 'q';
                break;
            default:
                change = 'p';
                break;
        }
        // ChangePiece가 기존 기물을 파괴하므로 좌표/색을 먼저 캡처한다.
        Vector3Int pos = piece.Pos;
        PieceColor color = piece.Color;

        BoardManager.Instance.ChangePiece(pos, color, change);

        // 이 카드는 Effector를 만들지 않으므로 SO에 지정된 적용 VFX/애니메이션을 직접 재생한다.
        if (DataSO != null)
        {
            Piece promoted = BoardManager.Instance.GetPiece(pos);
            Vector3 vfxPos = promoted != null
                ? promoted.transform.position
                : BoardManager.Instance.GridPosToWorldPos(pos);
            VFXSpawner.SpawnOneShot(DataSO.VFX.ApplyVFXPrefab, vfxPos,
                promoted != null ? promoted.transform : null);
            if (DataSO.VFX.PlayApplyAnim && promoted != null)
                VFXSpawner.PlayPunch(promoted.transform, DataSO.VFX.AnimStrength, DataSO.VFX.AnimDuration);
        }
    }
}
