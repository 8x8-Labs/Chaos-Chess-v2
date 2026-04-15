using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

[System.Serializable]
class FENPrefabPair
{
    public char FENChar;
    public Piece prefab;
}

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance;
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

    private int halfmoveClock = 0;
    private int fullmoveNumber = 1;

    public System.Action<Piece, Vector3Int> OnPromotionRequired;

    private List<GlobalEffector> globalEffectors = new();

    private Castling castling = new Castling();
    private Vector3Int enPassantPos = new Vector3Int(-1, -1, -1);

    [SerializeField] private string FEN;
    private List<Piece> Pieces = new List<Piece>();
    private Piece[,] board = new Piece[8, 8];

    [SerializeField] private List<FENPrefabPair> FENMapList;
    private Dictionary<char, Piece> FENMap;

    private Vector2 BoardCenterOffset = new Vector2(3.5f, 3.5f);
    private Vector2 CellSize = new Vector2(0.65f, 0.65f);

    private Dictionary<Vector3Int, List<TileEffector>> tileEffectors
        = new Dictionary<Vector3Int, List<TileEffector>>();

    [SerializeField] private Transform pieceSpawnTransform;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        enPassantPos = new Vector3Int(-1, -1, -1);

        FENMap = new Dictionary<char, Piece>();

        foreach (var pair in FENMapList)
        {
            FENMap[pair.FENChar] = pair.prefab;
        }
    }

    /// <summary>문자열 FEN의 값을 이용해서 Board와 Pieces를 초기화합니다</summary> 
    public void LoadFEN(string fenOverride = null)
    {
        if (fenOverride != null)
            FEN = fenOverride;
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
                Piece piece = Instantiate(prefab, pieceSpawnTransform);

                // 상태 설정
                piece.Init(pos, isWhite ? PieceColor.White : PieceColor.Black);
                piece.StartPos = pos;

                // 보드 등록
                AddPiece(piece, pos);

                // 위치 반영 (Transform 이동 등)
                Vector3 WorldPos = GridPosToWorldPos(pos);
                piece.Move(pos, WorldPos, animate: false);

                Pieces.Add(piece);
            }

            CheckCastlingRights();
            x++;
        }

        if (SliceFEN[1][0] != GameManager.Instance.NowTurn)
        {
            GameManager.Instance.NextTurn();
        }

        castling.SetFen(SliceFEN[2]);

        if (SliceFEN[3] != "-")
            enPassantPos = UCIToGrid(SliceFEN[3]);

        halfmoveClock = int.Parse(SliceFEN[4]);
        fullmoveNumber = int.Parse(SliceFEN[5]);

        CheckCastlingRights();
        UpdateFEN();

        FairyStockfishBridge.Instance.SetPosition(FEN);

        CheckKingExistence();
    }

    /// <summary> 모든 기물들이 이동 가능한 위치를 업데이트 합니다 </summary>
    public void UpdatePiecesCanMovePos(string[] moves)
    {
        foreach (Piece piece in Pieces)
        {
            piece.ResetCanMovePos();
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
            to = UCIToGrid(move.Substring(2, 2));

            fromPiece.AddCanMovePos(to);
        }
    }

    /// <summary> board에 기물을 추가합니다 </summary>
    private void AddPiece(Piece piece, Vector3Int pos)
    {
        board[pos.x, pos.y] = piece;
    }

    /// <summary> 선택한 좌표가 체스판 안에 있는지 확인합니다 </summary>
    public bool IsInside(Vector3Int pos)
    {
        return pos.x >= 0 && pos.x < 8 && pos.y >= 0 && pos.y < 8;
    }

    /// <summary> 선택한 좌표에 기물이 없는지 확인합니다 </summary>
    public bool IsEmpty(Vector3Int pos)
    {
        if (!IsInside(pos)) return false;

        return board[pos.x, pos.y] == null;
    }

    // public bool IsOccupiedByAlly(Vector3Int pos, Piece referencePiece)
    // {
    //     if (!IsInside(pos)) return false;
    //     Piece occupant = GetPiece(pos);
    //     return occupant != null && occupant.Color == referencePiece.Color;
    // }

    /// <summary> 선택한 좌표에 기물을 가져옵니다 </summary>
    public Piece GetPiece(Vector3Int pos)
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

        // AI가 무적 기물을 잡으려 할 때: 이동 취소 후 플레이어 턴 반환
        Piece targetPiece = GetPiece(to);
        if (targetPiece != null && targetPiece.IsInvincible)
        {
            Debug.Log($"[Invincible] {targetPiece.name} 무적 발동! AI 이동 취소, 플레이어 턴으로 전환");
            targetPiece.ConsumeInvincibility();
            GameManager.Instance.NextTurn();
            return;
        }

        Piece piece = GetPiece(from);
        if (piece != null)
            MovePiece(piece, to, promotion);

        DOVirtual.DelayedCall(Piece.MoveDuration, () => GameManager.Instance.NextTurn());
    }

    /// <summary>
    /// 기물을 이동시킵니다.
    /// 만약 이동시키려는 위치가 이동이 불가능한 공간이라면 false를 반환합니다.
    /// uci로 이동을 할때 프로모션을 하려면 promotion의 값을 넣어야합니다.
    /// </summary>
    public bool MovePiece(Piece piece, Vector3Int target, char promotion = '\0')
    {
        if (!piece.CanMoveTo(this, target))
            return false;

        Vector3Int from = piece.Pos;

        if (!CanMoveToTile(piece, from, target))
            return true; // 기물 이동을 안하고 턴을 넘김

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

        TriggerTileExit(from, piece);
        board[from.x, from.y] = null;

        bool isCapture = false;
        if (!IsEmpty(target))
        {
            isCapture = true;

            Piece targetPiece = GetPiece(target);
            if (targetPiece is Rook)
            {
                castling.OnRookDie(targetPiece.Color, target);
            }
            // 잡히는 대상 파괴
            AppendDeadPiece(targetPiece.Type, targetPiece.Color);
            DestroyPiece(target);
        }

        board[target.x, target.y] = piece;
        Vector3 WorldPos = GridPosToWorldPos(target);
        piece.Move(target, WorldPos);

        if (isCapture)
            // 잡는 대상의 이벤트 호출
            piece.TriggerOnCapture();

        if (piece is Pawn || isCapture)
            halfmoveClock = 0;
        else
            halfmoveClock++;

        // fullmove number (흑이 두면 증가)
        if (piece.Color == PieceColor.Black)
            fullmoveNumber++;

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

        TriggerGlobalEffectors(piece, target, isCapture);
        TriggerTileEnter(target, piece);

        foreach (var eff in piece.GetComponents<IPieceEffect>())
            eff.OnPieceMove(target);

        return true;
    }


    private void HandlePromotion(Piece pawn, Vector3Int pos, char promotion)
    {
        PieceColor color = pawn.Color;

        // AI 프로모션 (UCI)
        if (promotion != '\0')
        {
            ChangePiece(pos, color, promotion, pawn);
        }
        else
        {
            // 플레이어 → 이벤트 던짐
            OnPromotionRequired?.Invoke(pawn, pos);
        }
    }

    ///<summary> pos에 새로운 기물을 추가합니다 </summary> 
    public void ChangePiece(Vector3Int pos, PieceColor color, char type, Piece prom = null)
    {
        Vector3Int sp = new();
        if (prom != null)
            sp = prom.StartPos;

        DestroyPiece(pos);

        char key = char.ToLower(type);

        if (FENMap.TryGetValue(key, out Piece prefab))
        {
            Piece newPiece = Instantiate(prefab, pieceSpawnTransform);

            newPiece.Init(pos, color);
            AddPiece(newPiece, pos);
            if (prom != null)
            {
                newPiece.IsPromotioned = true;
                newPiece.StartPos = sp;
            }

            Vector3 WorldPos = GridPosToWorldPos(pos);
            newPiece.Move(pos, WorldPos);

            Pieces.Add(newPiece);
        }

        RefreshMoves();
    }

    ///<summary> target에 기물을 지웁니다 </summary> 
    public void DestroyPiece(Vector3Int target, bool refresh = true)
    {
        Piece targetPiece = GetPiece(target);
        DestroyPiece(targetPiece, refresh);
    }

    ///<summary> 기물을 지웁니다 </summary> 
    public void DestroyPiece(Piece piece, bool refresh = true)
    {
        if (piece == null) return;

        board[piece.Pos.x, piece.Pos.y] = null;
        piece.GetComponent<IPieceEffect>()?.OnPieceCaptured();
        Pieces.Remove(piece);
        Destroy(piece.gameObject);

        if (refresh) RefreshMoves();
    }

    ///<summary> 기물들을 지웁니다 </summary> 
    public void DestroyPieces(List<Piece> pieces, bool Refresh = true)
    {
        if (pieces == null) return;

        foreach (Piece piece in pieces)
        {
            DestroyPiece(piece, false);
        }
        if (Refresh)
            RefreshMoves();
    }

    /// <summary>기물을 보드 배열과 리스트에서 제거합니다. GameObject는 유지됩니다.</summary>
    public void HidePiece(Piece piece)
    {
        if (piece == null) return;
        board[piece.Pos.x, piece.Pos.y] = null;
        Pieces.Remove(piece);
        piece.gameObject.SetActive(false);
    }

    /// <summary>HidePiece로 숨긴 기물을 원래 위치(piece.Pos)에 복원합니다.</summary>
    public void RestorePiece(Piece piece)
    {
        if (piece == null) return;
        board[piece.Pos.x, piece.Pos.y] = piece;
        if (!Pieces.Contains(piece)) Pieces.Add(piece);
        piece.gameObject.SetActive(true);
    }

    /// <summary>캐슬링 상태를 FEN 문자열로 복원합니다. 투기장 Timeout 복원에 사용됩니다.</summary>
    public void SetCastlingFromFEN(string castlingFen) => castling.SetFen(castlingFen);

    ///<summary> UCI 좌표를 Vector3Int로 바꿉니다 </summary>
    public Vector3Int UCIToGrid(string sq)
    {
        int x = sq[0] - 'a'; // 'a'~'h' → 0~7
        int y = sq[1] - '1'; // '1'~'8' → 0~7
        return new Vector3Int(x, y, 0);
    }

    ///<summary> Vector3Int 좌표를 UCI로 바꿉니다 </summary> 
    public string GridTOUCI(Vector3Int pos)
    {
        string sq = ((char)(pos.x + 'a')).ToString() + ((char)(pos.y + '1')).ToString();
        return sq;
    }

    ///<summary> Vector3Int 좌표를 월드좌표로 바꿉니다 </summary> 
    public Vector3 GridPosToWorldPos(Vector3Int GridPos)
    {
        return new Vector3
        (
            (GridPos.x - BoardCenterOffset.x) * CellSize.x,
            (GridPos.y - BoardCenterOffset.y) * CellSize.y,
            0
        );
    }

    ///<summary> 현재 Board상태를 받아서 문자열 FEN을 수정합니다 </summary> 
    public void UpdateFEN()
    {
        CheckCastlingRights();
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

        if (GameManager.Instance.NowTurn == 'w')
            FEN += 'w';
        else
            FEN += 'b';


        FEN += " ";

        FEN += castling.GetFEN() + " ";

        string ep = enPassantPos == new Vector3Int(-1, -1, -1) ? "-" : GridTOUCI(enPassantPos);
        FEN += ep + " ";

        FEN += halfmoveClock + " " + fullmoveNumber;
        FairyStockfishBridge.Instance.SetPosition(FEN);

        CheckKingExistence();
    }

    /// <summary>FEN을 업데이트하고 기물들이 이동 가능한 위치를 초기화합니다 </summary>
    public void RefreshMoves()
    {
        UpdateFEN();
        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        UpdatePiecesCanMovePos(moves);
    }

    /// <summary>원래 킹, 룩 위치에 기물이 없거나 다른 기물이 있으면 그 방향 캐슬링이 불가능하게 만듭니다 </summary>
    private void CheckCastlingRights()
    {
        // 백킹
        Piece whiteKing = GetPiece(UCIToGrid("e1"));
        if (!(whiteKing is King) || whiteKing.Color != PieceColor.White)
        {
            castling.OnKingMove(PieceColor.White);
        }

        // 흑킹
        Piece blackKing = GetPiece(UCIToGrid("e8")); // e8
        if (!(blackKing is King) || blackKing.Color != PieceColor.Black)
        {
            castling.OnKingMove(PieceColor.Black);
        }

        //백룩
        Piece rook1 = GetPiece(UCIToGrid("a1"));
        if (!(rook1 is Rook) || rook1.Color != PieceColor.White || rook1.MoveFenOverride != null)
            castling.OnRookMove(PieceColor.White, UCIToGrid("a1"));

        Piece rook2 = GetPiece(UCIToGrid("h1"));
        if (!(rook2 is Rook) || rook2.Color != PieceColor.White || rook2.MoveFenOverride != null)
            castling.OnRookMove(PieceColor.White, UCIToGrid("h1"));

        //흑룩
        Piece rook3 = GetPiece(UCIToGrid("a8"));
        if (!(rook3 is Rook) || rook3.Color != PieceColor.Black || rook3.MoveFenOverride != null)
            castling.OnRookMove(PieceColor.Black, UCIToGrid("a8"));

        Piece rook4 = GetPiece(UCIToGrid("h8"));
        if (!(rook4 is Rook) || rook4.Color != PieceColor.Black || rook4.MoveFenOverride != null)
            castling.OnRookMove(PieceColor.Black, UCIToGrid("h8"));
    }

    /// <summary>문자열 FEN을 받습니다. </summary>
    public string GetFEN()
    {
        return FEN;
    }

    public int GetHalfmoveClock()
    {
        return halfmoveClock;
    }

    private bool CanMoveToTile(Piece piece, Vector3Int from, Vector3Int to)
    {
        foreach (var effector in globalEffectors)
        {
            if (!effector.CanPieceAct(piece, from, to))
                return false;
        }

        if (!tileEffectors.TryGetValue(to, out var list)) return true;
        foreach (var effector in list)
        {
            if (!effector.CanPieceEnter(piece, from, to))
                return false;
        }

        return true;
    }

    public void RegisterGlobalEffector(GlobalEffector effector)
    {
        globalEffectors.Add(effector);
    }

    public void UnregisterGlobalEffector(GlobalEffector effector)
    {
        globalEffectors.Remove(effector);
    }

    private void TriggerGlobalEffectors(Piece piece, Vector3Int dest, bool isCapture)
    {
        foreach (var effector in new List<GlobalEffector>(globalEffectors))
        {
            effector.OnPieceAct(piece, dest);
            if (isCapture)
                effector.OnPieceCapture(piece, dest);

        }
    }

    public void ClearAllTileEffectors()
    {
        foreach (var pair in tileEffectors)
        {
            foreach (var effector in new List<TileEffector>(pair.Value))
            {
                effector.Revert();
            }
        }

        tileEffectors.Clear();
    }

    public void RegisterTileEffector(Vector3Int pos, TileEffector effector)
    {
        if (!tileEffectors.TryGetValue(pos, out var list))
        {
            list = new List<TileEffector>();
            tileEffectors[pos] = list;
        }
        list.Add(effector);
    }

    public void UnregisterTileEffector(Vector3Int pos, TileEffector effector)
    {
        if (tileEffectors.TryGetValue(pos, out var list))
            list.Remove(effector);
    }

    private void TriggerTileEnter(Vector3Int pos, Piece piece)
    {
        if (!tileEffectors.TryGetValue(pos, out var list)) return;
        foreach (var effector in new List<TileEffector>(list))
            effector.OnPieceEnter(piece);
    }

    private void TriggerTileExit(Vector3Int pos, Piece piece)
    {
        if (!tileEffectors.TryGetValue(pos, out var list)) return;
        foreach (var effector in new List<TileEffector>(list))
            effector.OnPieceExit(piece);
    }

    /// <summary>보드 위 모든 기물 목록을 반환합니다.</summary>
    public List<Piece> GetAllPieces()
    {
        return new List<Piece>(Pieces);
    }

    /// <summary>보드 위 특정 기물을 전부 반환합니다.</summary>
    public List<T> GetPiece<T>(PieceColor pieceColor) where T : Piece
    {
        List<T> targetPieces = new List<T>();
        for (int i = 0; i < Pieces.Count; i++)
        {
            if (Pieces[i] is T && Pieces[i].Color == pieceColor)
            {
                targetPieces.Add((T)Pieces[i]);
            }
        }
        return targetPieces;
    }

    private void CheckKingExistence()
    {
        bool whiteAlive = HasKing(PieceColor.White);
        bool blackAlive = HasKing(PieceColor.Black);

        if (!whiteAlive && !blackAlive)
        {
            GameManager.Instance.FinishType = GameResult.Draw;
        }
        else if (!whiteAlive)
        {
            GameManager.Instance.FinishType = GameResult.BlackWin;
        }
        else if (!blackAlive)
        {
            GameManager.Instance.FinishType = GameResult.WhiteWin;
        }
        else
        {
            GameManager.Instance.FinishType = GameResult.None;
        }
    }

    private bool HasKing(PieceColor color)
    {
        foreach (var piece in Pieces)
        {
            if (piece is King && piece.Color == color)
                return true;
        }
        return false;
    }

    /// <summary>체스 규칙 검사 없이 기물을 대상 칸으로 강제 이동합니다.</summary>
    public void ForceTeleport(Piece piece, Vector3Int target, char promotion = '\0', bool useTurn = false)
    {
        Vector3Int from = piece.Pos;

        if (!CanMoveToTile(piece, from, target))
        {
            if (useTurn)
            {
                GameManager.Instance.NextTurn();
            }
            else
            {
                RefreshMoves();
            }
        }

        TriggerTileExit(from, piece);
        board[from.x, from.y] = null;

        // 이동하는 위치에 기물이 있으면 먹음
        if (!IsEmpty(target))
        {
            Piece targetPiece = GetPiece(target);
            if (targetPiece is Rook)
            {
                castling.OnRookDie(targetPiece.Color, target);
            }
            AppendDeadPiece(targetPiece.Type, targetPiece.Color);
            DestroyPiece(target);
        }

        if (piece is King)
        {
            castling.OnKingMove(piece.Color);
        }
        if (piece is Rook)
        {
            castling.OnRookMove(piece.Color, piece.Pos);
        }

        board[target.x, target.y] = piece;
        Vector3 WorldPos = GridPosToWorldPos(target);
        piece.Move(target, WorldPos);

        // 프로모션
        if (piece is Pawn)
        {
            if (piece.Pos.y == (piece.Color == PieceColor.White ? 7 : 0))
            {
                HandlePromotion(piece, target, promotion);
            }
        }

        if (useTurn)
        {
            GameManager.Instance.NextTurn();
        }
        else
        {
            RefreshMoves();
        }
    }

    /// <summary>
    /// 여러 기물을 동시에 재배치합니다.
    /// Phase 1에서 기존 칸을 모두 비운 뒤 Phase 2에서 새 칸에 배치하므로
    /// 서로의 자리를 교환할 때 충돌이 발생하지 않습니다.
    /// </summary>
    public void BatchReassign(List<Piece> pieces, List<Vector3Int> newPositions)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i] is King)
                castling.OnKingMove(pieces[i].Color);
            if (pieces[i] is Rook)
                castling.OnRookMove(pieces[i].Color, pieces[i].Pos);

            TriggerTileExit(pieces[i].Pos, pieces[i]);
            board[pieces[i].Pos.x, pieces[i].Pos.y] = null;
        }
        for (int i = 0; i < pieces.Count; i++)
        {
            Vector3Int target = newPositions[i];
            board[target.x, target.y] = pieces[i];
            Vector3 worldPos = GridPosToWorldPos(target);
            pieces[i].Move(target, worldPos);
        }
    }

    private List<PieceType> whiteDeadPieces = new();
    private List<PieceType> blackDeadPieces = new();
    public void AppendDeadPiece(PieceType type, PieceColor color)
    {
        if (color == PieceColor.White)
            whiteDeadPieces.Add(type);
        else
            blackDeadPieces.Add(type);
    }
    public List<PieceType> WhiteDeadPieces
    {
        get
        {
            return whiteDeadPieces;
        }
    }
    public List<PieceType> BlackDeadPieces
    {
        get
        {
            return blackDeadPieces;
        }
    }
}
