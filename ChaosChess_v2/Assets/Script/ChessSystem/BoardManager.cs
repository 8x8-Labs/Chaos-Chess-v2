using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BoardManager : MonoBehaviour
{
    [SerializeField] private string FEN;
    [SerializeField] private List<Piece> Pieces;
    private Piece[,] board = new Piece[8, 8];

    void Start()
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject obj in objects)
        {
            if (obj.layer == LayerMask.NameToLayer("Piece"))
            {
                Piece piece = obj.GetComponent<Piece>();
                Pieces.Add(piece);
            }
        }

        foreach (Piece piece in Pieces)
        {
            AddPiece(piece, piece.Pos);
            piece.Move(piece.Pos);
        }
        UpdatePiecesCanMovePos();
    }

    public void UpdatePiecesCanMovePos()
    {
        foreach (Piece piece in Pieces)
        {
            piece.UpdateCanMovePos(this);
        }
    }

    private void AddPiece(Piece piece, Vector3Int pos) // board에 기물 추가하기
    {
        board[pos.x, pos.y] = piece;
    }

    public bool IsInside(Vector3Int pos) // 선택한 좌표가 체스판 안에 있는가
    {
        return pos.x >= 0 && pos.x < 8 && pos.y >= 0 && pos.y < 8;
    }

    public bool IsEmpty(Vector3Int pos) // 선택한 좌표에 기물이 있는가
    {
        if (!IsInside(pos)) return false;

        return board[pos.x, pos.y] == null;
    }

    public Piece GetPiece(Vector3Int pos) // 선택한 좌표에 기물을 가져온다
    {
        if (!IsInside(pos)) return null;

        return board[pos.x, pos.y];
    }

    public bool MovePiece(Piece piece, Vector3Int target) // 기물의 위치를 target으로 이동시킨다
    {
        if (!piece.CanMoveTo(this, target)) // target으로 이동 가능한가
            return false;

        board[piece.Pos.x, piece.Pos.y] = null;

        if (!IsEmpty(target))
        {
            DestroyPiece(target);
        }

        board[target.x, target.y] = piece;

        piece.Move(target);

        return true;
    }

    public void DestroyPiece(Vector3Int target)
    {
        Piece targetPiece = GetPiece(target);

        Pieces.Remove(targetPiece);
        Destroy(targetPiece.gameObject);
    }

    public void UpdateFEN()
    {
        FEN = "";
        for (int i = 7; i > -1; i--)
        {
            string line = "";
            int emptyCnt = 0;
            Vector3Int target;
            for (int j = 0; j < 8; j++)
            {
                target = new Vector3Int(j, i, 0);
                if (!IsEmpty(target))
                {
                    if (emptyCnt > 0)
                    {
                        line += emptyCnt;
                        emptyCnt = 0;
                    }

                    Piece piece = GetPiece(target);
                    line += piece.GetFen();
                }
                else
                    emptyCnt++;
            }
            if (emptyCnt != 0)
                line += emptyCnt;

            FEN += line;

            if (i != 0)
                FEN += "/";
        }
    }

    public string GetFEN()
    {
        return FEN;
    }

}
