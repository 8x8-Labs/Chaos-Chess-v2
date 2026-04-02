using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 민첩 - 기물 전용 (일반)
/// 폰은 앞이나 뒤로 이동하여 기물을 한 번 잡을 수 있습니다.
/// </summary>
public class AgileCard : CardData, IPieceCard
{
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
        Piece pawn = args.Targets[0];
        BoardManager bm = BoardManager.Instance;

        // 후방 대각선 방향 (백: y-1, 흑: y+1)
        int backDir = pawn.Color == PieceColor.White ? -1 : 1;
        Vector3Int pos = pawn.Pos;

        Vector3Int[] backCaptures =
        {
            new Vector3Int(pos.x - 1, pos.y + backDir, 0),
            new Vector3Int(pos.x + 1, pos.y + backDir, 0),
        };

        foreach (Vector3Int capturePos in backCaptures)
        {
            if (!bm.IsInside(capturePos)) continue;
            Piece target = bm.GetPiece(capturePos);
            if (target != null && target.Color != pawn.Color)
                pawn.AddCanMovePos(capturePos);
        }
    }
}
