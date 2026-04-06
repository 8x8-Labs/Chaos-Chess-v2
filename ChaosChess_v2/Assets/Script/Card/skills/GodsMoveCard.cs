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
        BoardManager.Instance.ChangePiece(piece.Pos, piece.Color, change);
    }
}
