using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 윤회 - 기물 전용 (고급)
/// 2턴 동안 룩은 비숍의 행마법대로, 비숍은 룩의 행마법대로 움직입니다.
/// </summary>
public class TransmigrationCard : CardData, IPieceCard
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
        Piece piece = args.Targets[0];
        if (piece.IsPromotioned)
        {
            Vector3Int sp = piece.StartPos;
            Piece cur = BoardManager.Instance.GetPiece(sp);
            if (cur != null)
            {
                int[] dx = { -1, -1, -1, 0, 1, 1, 1, 0 };
                int[] dy = { -1, 0, 1, 1, 1, 0, -1, -1 };
                bool flag = true;
                for(int i = 0; i < 8; i++)
                {
                    int x = dx[i];
                    int y = dy[i];
                    Vector3Int can = new Vector3Int(sp.x + x, sp.y + y, sp.z);
                    Piece p = BoardManager.Instance.GetPiece(can);
                    if (p == null && flag)
                    {
                        Debug.Log("moved");
                        BoardManager.Instance.ForceTeleport(cur,can);
                        BoardManager.Instance.RefreshMoves();
                        flag = false;
                    }
                }
            }
            BoardManager.Instance.ForceTeleport(piece, sp);
            BoardManager.Instance.ChangePiece(sp, GameManager.Instance.turnColor == PieceColor.White ? PieceColor.Black : PieceColor.White, 'p',piece);
            BoardManager.Instance.RefreshMoves();
        }

    }
}
