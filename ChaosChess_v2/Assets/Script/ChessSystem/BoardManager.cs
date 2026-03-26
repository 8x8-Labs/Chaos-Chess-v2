using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
class FENPrefabPair
{
    public char FENChar;
    public Piece prefab;
}

public class BoardManager : MonoBehaviour
{
    private class Castling
    {
        private bool WhiteKingSide = true;
        private bool WhiteQueenSide = true;
        private bool BlackKingSide = true;
        private bool BlackQueenSide = true;

        public string GetFEN()
        {
            string result = "";

            if (WhiteKingSide) result += "K";
            if (WhiteQueenSide) result += "Q";
            if (BlackKingSide) result += "k";
            if (BlackQueenSide) result += "q";

            return result == "" ? "-" : result;
        }

        public void SetFen(string fenCastling)
        {
            WhiteKingSide = false;
            WhiteQueenSide = false;
            BlackKingSide = false;
            BlackQueenSide = false;

            if (fenCastling == "-") return;

            foreach (char c in fenCastling)
            {
                switch (c)
                {
                    case 'K': WhiteKingSide = true; break;
                    case 'Q': WhiteQueenSide = true; break;
                    case 'k': BlackKingSide = true; break;
                    case 'q': BlackQueenSide = true; break;
                }
            }
        }

        public void OnKingMove(PieceColor color)
        {
            if (color == PieceColor.White)
            {
                WhiteKingSide = false;
                WhiteQueenSide = false;
            }
            else
            {
                BlackKingSide = false;
                BlackQueenSide = false;
            }
        }

        public void OnRookMove(PieceColor color, Vector3Int from)
        {
            if (color == PieceColor.White)
            {
                if (from.x == 0) WhiteQueenSide = false; // a1
                if (from.x == 7) WhiteKingSide = false;  // h1
            }
            else
            {
                if (from.x == 0) BlackQueenSide = false; // a8
                if (from.x == 7) BlackKingSide = false;  // h8
            }
        }

        public void OnRookDie(PieceColor color, Vector3Int pos)
        {
            // 잡힌 쪽 기준
            if (color == PieceColor.White)
            {
                if (pos.x == 0) WhiteQueenSide = false;
                if (pos.x == 7) WhiteKingSide = false;
            }
            else
            {
                if (pos.x == 0) BlackQueenSide = false;
                if (pos.x == 7) BlackKingSide = false;
            }
        }
    }


    public System.Action<Piece, Vector3Int> OnPromotionRequired;

    private Castling castling = new Castling();
    private Vector3Int enPassantPos = new Vector3Int(-1, -1, -1);

    [SerializeField] private string FEN;
    private List<Piece> Pieces = new List<Piece>();
    private Piece[,] board = new Piece[8, 8];

    [SerializeField] private List<FENPrefabPair> FENMapList;
    private Dictionary<char, Piece> FENMap;

    private GamaManager gamaManager;

    void Awake()
    {
        enPassantPos = new Vector3Int(-1, -1, -1);

        gamaManager = GetComponent<GamaManager>();

        FENMap = new Dictionary<char, Piece>();

        foreach (var pair in FENMapList)
        {
            FENMap[pair.FENChar] = pair.prefab;
        }
    }

    void Start()
    {
        LoadFEN();
        UpdatePiecesCanMovePos();
    }

    private void LoadFEN()
    {
        Pieces.Clear();
        board = new Piece[8, 8];

        int x = 0;
        int y = 7;

        string[] SliceFEN = FEN.Split(' ');

        foreach (char c in SliceFEN[0])
        {
            // 줄 바꿈
            if (c == '/')
            {
                y--;
                x = 0;
                continue;
            }

            // 숫자 = 빈칸
            if (char.IsDigit(c))
            {
                x += c - '0';
                continue;
            }

            // 색 판단
            bool isWhite = char.IsUpper(c);

            // 종류 판단
            char lower = char.ToLower(c);

            // 프리팹 찾기
            if (FENMap.TryGetValue(lower, out Piece prefab))
            {
                Vector3Int pos = new Vector3Int(x, y, 0);

                // 생성
                Piece piece = Instantiate(prefab, transform);

                // 상태 설정
                piece.Init(pos, isWhite ? PieceColor.White : PieceColor.Black);

                // 보드 등록
                AddPiece(piece, pos);

                // 위치 반영 (Transform 이동 등)
                piece.Move(pos);

                Pieces.Add(piece);
            }

            x++;
        }

        if (SliceFEN[1][0] != gamaManager.NowTurn)
        {
            gamaManager.NextTurn();
        }

        castling.SetFen(SliceFEN[2]);

        if (SliceFEN[3] != "-")
            enPassantPos = UCIToGrid(SliceFEN[3]);

        FairyStockfishBridge.Instance.SetPosition(FEN);
    }

    public void UpdatePiecesCanMovePos()
    {
        foreach (Piece piece in Pieces)
        {
            piece.ResetCanMovePos();
        }

        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        if (FairyStockfishBridge.Instance.IsInCheck())
        {
            Debug.Log("체크");
        }

        Vector3Int from = new Vector3Int(-1, -1, -1);
        Vector3Int to = new Vector3Int(-1, -1, -1);
        Piece fromPiece = null;

        foreach (string move in moves)
        {
            if (UCIToGrid(move.Substring(0, 2)) != from)
            {
                from = UCIToGrid(move.Substring(0, 2));
                fromPiece = GetPiece(from);
            }
            to = UCIToGrid(move.Substring(2));

            fromPiece.AddCanMovePos(to);
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

    // uci로 받은 이동을 적용한다
    public void ApplyUCIMove(string uciMove)
    {
        // "e2e4" → from(4,1), to(4,3) 변환
        Vector3Int from = UCIToGrid(uciMove.Substring(0, 2));
        Vector3Int to = UCIToGrid(uciMove.Substring(2, 2));

        char promotion = '\0';

        if (uciMove.Length == 5)
            promotion = uciMove[4];

        Piece piece = GetPiece(from);
        if (piece != null)
            MovePiece(piece, to, promotion);

        gamaManager.NextTurn();
    }

    public bool MovePiece(Piece piece, Vector3Int target, char promotion = '\0')
    {
        if (!piece.CanMoveTo(this, target))
            return false;

        Vector3Int from = piece.Pos;

        Vector3Int prevEP = enPassantPos;
        enPassantPos = new Vector3Int(-1, -1, -1);

        // 앙파상
        if (piece is Pawn && target == prevEP)
        {
            int dir = (piece.Color == PieceColor.White) ? -1 : 1;
            Vector3Int diePos = new Vector3Int(target.x, target.y + dir, 0);
            DestroyPiece(diePos);
        }

        // 캐슬링
        if (piece is King)
        {
            castling.OnKingMove(piece.Color);
            if (Mathf.Abs(piece.Pos.x - target.x) == 2)
            {
                int dir = (piece.Pos.x < target.x) ? 1 : -1;
                Vector3Int rookFrom = Vector3Int.zero;

                if (piece.Pos.x < target.x)
                    rookFrom = new Vector3Int(target.x + dir, target.y, 0);
                else
                    rookFrom = new Vector3Int(target.x + dir * 2, target.y, 0);

                Vector3Int rookTo = new Vector3Int(target.x - dir, target.y, 0);
                Piece rook = GetPiece(rookFrom);
                MovePiece(rook, rookTo);
            }
        }
        if (piece is Rook)
        {
            castling.OnRookMove(piece.Color, from);
        }

        board[from.x, from.y] = null;

        if (!IsEmpty(target))
        {
            Piece targetPiece = GetPiece(target);
            if (targetPiece is Rook)
            {
                castling.OnRookDie(targetPiece.Color, target);
            }
            DestroyPiece(target);
        }

        board[target.x, target.y] = piece;
        piece.Move(target);

        if (piece is Pawn && Mathf.Abs(from.y - target.y) == 2)
        {
            int middleY = (from.y + target.y) / 2;
            enPassantPos = new Vector3Int(from.x, middleY, 0);
        }

        if (piece is Pawn)
        {
            if (piece.Pos.y == (piece.Color == PieceColor.White ? 7 : 0))
            {
                HandlePromotion(piece, target, promotion);
            }
        }

        return true;
    }
    private void HandlePromotion(Piece pawn, Vector3Int pos, char promotion)
    {
        PieceColor color = pawn.Color;

        // AI 프로모션 (UCI)
        if (promotion != '\0')
        {
            CreatePromotionPiece(pos, color, promotion);
        }
        else
        {
            // 플레이어 → 이벤트 던짐
            OnPromotionRequired?.Invoke(pawn, pos);
        }
    }

    public void CreatePromotionPiece(Vector3Int pos, PieceColor color, char type)
    {
        // 폰 제거
        DestroyPiece(pos);

        char key = char.ToLower(type);

        if (FENMap.TryGetValue(key, out Piece prefab))
        {
            Piece newPiece = Instantiate(prefab, transform);

            newPiece.Init(pos, color);
            AddPiece(newPiece, pos);
            newPiece.Move(pos);

            Pieces.Add(newPiece);
        }
    }

    public void DestroyPiece(Vector3Int target)
    {
        Piece targetPiece = GetPiece(target);

        Pieces.Remove(targetPiece);
        Destroy(targetPiece.gameObject);
    }

    public Vector3Int UCIToGrid(string sq)
    {
        int x = sq[0] - 'a'; // 'a'~'h' → 0~7
        int y = sq[1] - '1'; // '1'~'8' → 0~7
        return new Vector3Int(x, y, 0);
    }

    public string GridTOUCI(Vector3Int pos)
    {
        string sq = ((char)(pos.x + 'a')).ToString() + ((char)(pos.y + '1')).ToString();
        return sq;
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
        FEN += " ";
        if (gamaManager.NowTurn == 'w')
            FEN += 'w';
        else
            FEN += 'b';

        string ep = enPassantPos == new Vector3Int(-1, -1, -1) ? "-" : GridTOUCI(enPassantPos);

        FEN += " ";

        FEN += castling.GetFEN();

        FEN += " ";

        FEN += ep + " 0 1";
    }

    public string GetFEN()
    {
        return FEN;
    }
}
