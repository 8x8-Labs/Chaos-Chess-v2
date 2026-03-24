using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FENPrefabPair
{
    public char FENChar;
    public Piece prefab;
}

public class BoardManager : MonoBehaviour
{
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

        // SliceFEN[2][0] 캐슬링 가능 여부

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

        Vector3Int from = new Vector3Int(-1, -1, -1);
        Vector3Int to = new Vector3Int(-1, -1, -1);
        Piece fromPiece = null;

        foreach (string move in moves)
        {
            Debug.Log(from + " " + UCIToGrid(move.Substring(0, 2)) + " " + fromPiece);
            if (UCIToGrid(move.Substring(0, 2)) != from)
            {
                from = UCIToGrid(move.Substring(0, 2));
                Debug.Log(GetPiece(from));
                fromPiece = GetPiece(from);

            }
            to = UCIToGrid(move.Substring(2));
            Debug.Log(fromPiece);

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

        Piece piece = GetPiece(from);
        if (piece != null)
            MovePiece(piece, to);

        gamaManager.NextTurn();
    }

    public bool MovePiece(Piece piece, Vector3Int target)
    {
        if (!piece.CanMoveTo(this, target))
            return false;

        Vector3Int from = piece.Pos;

        Vector3Int prevEP = enPassantPos;
        enPassantPos = new Vector3Int(-1, -1, -1);

        if (piece is Pawn && target == prevEP)
        {
            int dir = (piece.Color == PieceColor.White) ? -1 : 1;
            Vector3Int capturedPos = new Vector3Int(target.x, target.y + dir, 0);
            Debug.Log(capturedPos);
            DestroyPiece(capturedPos);
        }

        board[from.x, from.y] = null;

        if (!IsEmpty(target))
        {
            DestroyPiece(target);
        }

        board[target.x, target.y] = piece;
        piece.Move(target);

        if (piece is Pawn && Mathf.Abs(from.y - target.y) == 2)
        {
            int middleY = (from.y + target.y) / 2;
            enPassantPos = new Vector3Int(from.x, middleY, 0);
        }

        return true;
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
        FEN += "KQkq ";
        FEN += ep + " 0 1";
    }

    public string GetFEN()
    {
        return FEN;
    }
}
