using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private int curTurn = 1;
    public bool IsPlayerTurn => (curTurn % 2 == 1);

    public bool IsGameInput = true;
    private List<(int turn, Action action)> recievedActions = new List<(int, Action)>();
    public PieceColor turnColor
    {
        get
        {
            if (curTurn % 2 == 1)
                return PieceColor.White;
            else
                return PieceColor.Black;
        }
    }
    public char NowTurn
    {
        get
        {
            if (curTurn % 2 == 1)
                return 'w';
            else
                return 'b';
        }
    }

    private UIManager uiManager;
    private BoardUI boardUI;

    private Piece selectedPiece;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        FairyStockfishBridge.Instance.InitEngine("chess");

        curTurn = 1;

        boardUI = GetComponent<BoardUI>();
        uiManager = FindFirstObjectByType<UIManager>();

        BoardManager.Instance.OnPromotionRequired += HandlePromotion;

        BoardManager.Instance.LoadFEN();

        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        EvaluateGameState(moves);
        BoardManager.Instance.UpdatePiecesCanMovePos(moves);
    }

    public void SelectGrid(Vector3Int pos)
    {
        if (!IsPlayerTurn) return;
        if (!BoardManager.Instance.IsInside(pos)) return;

        Piece piece = BoardManager.Instance.GetPiece(pos);

        if (piece != null && piece.Color == turnColor && !(piece is Wall))
        {
            SelectPiece(piece);
            boardUI.DrawSelectTile(pos);
        }
        else
        {
            MoveSelected(pos);
            boardUI.DeleteSelectTile();
        }
    }

    private void HandlePromotion(Piece pawn, Vector3Int pos)
    {
        IsGameInput = false;

        uiManager.Show((type) =>
        {
            BoardManager.Instance.ChangePiece(pos, pawn.Color, type);

            IsGameInput = true;

            NextTurn();

            RequestAIMove();
        });
    }

    private void SelectPiece(Piece piece)
    {
        selectedPiece = piece;
    }

    public void NextTurn()
    {
        curTurn += 1;
        BoardManager.Instance.UpdateFEN(); // 디버깅
        string fen = BoardManager.Instance.GetFEN();
        FairyStockfishBridge.Instance.SetPosition(fen);

        ReturnAction();

        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        EvaluateGameState(moves);
        BoardManager.Instance.UpdatePiecesCanMovePos(moves);
    }

    public void AppendAction(int x, Action act)
    {
        recievedActions.Add((curTurn + x * 2, act));
    }

    public void ReturnAction()
    {
        for (int i = recievedActions.Count - 1; i >= 0; i--)
        {
            var item = recievedActions[i];

            if (item.turn == curTurn)
            {
                item.action.Invoke();
                recievedActions.RemoveAt(i);
            }
        }
    }
    // MoveSelected 안에서 플레이어 수 적용 후:
    private void MoveSelected(Vector3Int target)
    {
        if (selectedPiece == null) return;

        if (BoardManager.Instance.MovePiece(selectedPiece, target))
        {
            selectedPiece = null;

            // 프로모션이면 여기서 멈춤
            if (!IsGameInput)
                return;

            NextTurn();

            RequestAIMove();
        }
        selectedPiece = null;
    }

    public void RequestAIMove()
    {
        FairyStockfishBridge.Instance.GetBestMoveAsync(
            depth: 12,
            moveTimeMs: 2000,
            callback: (uciMove) =>
            {
                // UCI 수 (예: "e2e4") → Vector3Int 변환 후 BoardManager에 적용
                BoardManager.Instance.ApplyUCIMove(uciMove);
            }
        );
    }
    private void EvaluateGameState(string[] moves)
    {
        if (BoardManager.Instance.GetHalfmoveClock() >= 150)
        {
            OnDraw();
        }
        bool isCheck = FairyStockfishBridge.Instance.IsInCheck();


        if (moves.Length == 0)
        {
            if (isCheck)
                OnCheckmate();
            else
                OnDraw();
        }
        else if (isCheck)
        {
            OnCheck();
        }
    }
    private void OnCheck()
    {
        Debug.Log("체크");
    }

    private void OnCheckmate()
    {
        if (NowTurn == 'w')
        {
            Debug.Log("체크메이트");
            Debug.Log("흑 승");
        }
        else
        {
            Debug.Log("체크메이트");
            Debug.Log("백 승");
        }
        ExitGame();
    }

    private void OnDraw()
    {
        Debug.Log("무승부");
        ExitGame();
    }

    // (임시) 게임 종료 메서드
    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
