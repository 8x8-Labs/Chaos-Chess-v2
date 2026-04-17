using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public GameResult FinishType { get; set; } = GameResult.None;

    public static GameManager Instance;

    public PieceColor PlayerColor = PieceColor.White;
    public PieceColor EnemyColor = PieceColor.Black;

    public List<Sprite> BlackSprites = new List<Sprite>();
    public List<Sprite> WhiteSprites = new List<Sprite>();

    private int curTurn = 1;
    public bool IsPlayerTurn => (curTurn % 2 == 1);

    public bool IsGameInput = true;
    public bool IsEndGame { get; private set; } = false;
    public bool IsArenaMode { get; set; } = false;
    private List<(int turn, Action action)> recievedActions = new List<(int, Action)>();

    /// <summary>플레이어 턴이 시작되고 CanMovePos가 유효해진 직후 발행됩니다.</summary>
    public event Action OnPlayerTurnStarted;
    /// <summary>매 턴(플레이어·AI 모두) 종료 직후 발행됩니다. Effector 지속 턴 카운트다운에 사용됩니다.</summary>
    public event Action OnTurnChanged;
    /// <summary>반 턴 종료 직후 발행됩니다.</summary>
    public event Action OnHalfTurnChanged;
    /// <summary>시간역행 카드 전용 이벤트 입니다.</summary>
    public event Action<Action, Action> OnTimeReversalRequired;
    /// <summary>아버지의 원수 카드 전용 이벤트 입니다.</summary>
    public event Action<Piece> OnAwakenedPieceSelected;

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
    public UIManager UI => uiManager;

    private BoardUI boardUI;

    private Piece selectedPiece;
    private int extraPlayerActions = 0;
    private Piece lockedPiece = null;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainGameScene")
            return;
        IsEndGame = false;
        boardUI = FindFirstObjectByType<BoardUI>();
        uiManager = FindFirstObjectByType<UIManager>();

        FinishType = GameResult.None;

        curTurn = 1;

        BoardManager.Instance.OnPromotionRequired -= HandlePromotion;
        BoardManager.Instance.OnPromotionRequired += HandlePromotion;

        OnTimeReversalRequired -= HandleTimeReversal;
        OnTimeReversalRequired += HandleTimeReversal;

        LoadMapManager();

        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        EvaluateGameState(moves);
        BoardManager.Instance.UpdatePiecesCanMovePos(moves);
    }

    /// <summary>
    /// MapManager에서 FEN과 ELO(맵의 전체적인 상태)를 받아와서 스톡피쉬에 적용한다
    /// </summary>
    private void LoadMapManager()
    {
        FairyStockfishBridge.Instance.InitEngine("chess");
        if (MapManager.Instance != null && MapManager.Instance.curMap != null)
        {
            int elo = MapManager.Instance.curMap.ELO;
            FairyStockfishBridge.Instance.SetElo(elo);

            string fen = MapManager.Instance.curMap.FEN;
            BoardManager.Instance.LoadFEN(fen);
        }
        else
        {
            FairyStockfishBridge.Instance.SetElo(1000);

            BoardManager.Instance.LoadFEN();
        }
    }

    public void SelectGrid(Vector3Int pos)
    {
        if (!IsPlayerTurn) return;
        if (!BoardManager.Instance.IsInside(pos)) return;

        // 파괴된 기물이 잠겨있으면 잠금 해제
        if (lockedPiece != null && !lockedPiece)
        {
            extraPlayerActions = 0;
            lockedPiece = null;
        }

        UI.HideAwakenButton();

        Piece piece = BoardManager.Instance.GetPiece(pos);

        if (piece != null && piece.Color == turnColor && piece.Type != PieceType.Wall)
        {
            if (lockedPiece != null && piece != lockedPiece) return;
            SelectPiece(piece);
            boardUI.DrawSelectTile(pos);
            boardUI.DrawValidMoveTiles(piece);
        }
        else
        {
            MoveSelected(pos);
            boardUI.DeleteSelectTile();
            boardUI.DeleteValidMoveTiles();
        }
    }

    /// <summary>lockedPiece를 설정합니다. 투기장 등 외부에서 기물 잠금이 필요할 때 사용합니다.</summary>
    public void SetLockedPiece(Piece piece) => lockedPiece = piece;

    /// <summary>현재 플레이어에게 추가 행동권을 부여합니다. piece가 지정되면 해당 기물만 움직일 수 있습니다.</summary>
    public void GrantExtraPlayerAction(Piece piece = null)
    {
        extraPlayerActions++;
        lockedPiece = piece;
    }

    private void RefreshPlayerTurn()
    {
        BoardManager.Instance.UpdateFEN();
        string fen = BoardManager.Instance.GetFEN();
        FairyStockfishBridge.Instance.SetPosition(fen);
        FairyStockfishBridge.Instance.GetLegalMovesAsync(moves =>
        {
            EvaluateGameState(moves);
            BoardManager.Instance.UpdatePiecesCanMovePos(moves);
            OnPlayerTurnStarted?.Invoke();
        });
    }

    private void HandlePromotion(Piece pawn, Vector3Int pos)
    {
        IsGameInput = false;

        uiManager.ShowPromotion((type) =>
        {
            BoardManager.Instance.ChangePiece(pos, pawn.Color, type);

            IsGameInput = true;

            NextTurn(() => RequestAIMove());
        });
    }

    private void HandleTimeReversal(Action onYes, Action onNo)
    {
        IsGameInput = false;

        uiManager.ShowTimeReversal(
            () =>
            {
                onYes?.Invoke();

                IsGameInput = true;
            },
            () =>
            {
                onNo?.Invoke();

                IsGameInput = true;
            }
        );
    }

    public void RequestTimeReversal(Action onYes, Action onNo)
    {
        OnTimeReversalRequired?.Invoke(onYes, onNo);
    }

    private void SelectPiece(Piece piece)
    {
        selectedPiece = piece;
        selectedPiece.PieceSelect();
        OnAwakenedPieceSelected?.Invoke(piece);
    }

    public void NextTurn(Action onComplete = null)
    {
        curTurn += 1;
        BoardManager.Instance.UpdateFEN(); // 디버깅
        string fen = BoardManager.Instance.GetFEN();
        FairyStockfishBridge.Instance.SetPosition(fen);

        ReturnAction();

        FairyStockfishBridge.Instance.GetLegalMovesAsync(moves =>
        {
            EvaluateGameState(moves);
            BoardManager.Instance.UpdatePiecesCanMovePos(moves);

            if (IsPlayerTurn)
            {
                OnTurnChanged?.Invoke();
                OnPlayerTurnStarted?.Invoke();
            }

            OnHalfTurnChanged?.Invoke();

            ApplyGameResult();

            BoardManager.Instance.CheckKingExistence();

            BoardManager.Instance.RefreshMoves();

            FairyStockfishBridge.Instance.GetLegalMovesAsync(moves2 =>
            {
                EvaluateGameState(moves2);
                onComplete?.Invoke();
            });
        });
    }

    /// <summary>
    /// 일정 턴 이후 작동하는 행동을 삽입합니다.
    /// </summary>
    /// <param name="x">대기 턴</param>
    /// <param name="act">작동 액션</param>
    public void AppendAction(int x, Action act)
    {
        recievedActions.Add((curTurn + x * 2, act));
    }

    /// <summary>
    /// 액션 발동 후 리스트에서 제거하는 행동을 수행합니다.
    /// </summary>
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

        Piece piece = selectedPiece;
        selectedPiece = null;

        if (BoardManager.Instance.MovePiece(piece, target))
        {
            // 프로모션이면 여기서 멈춤
            if (!IsGameInput)
                return;

            DOVirtual.DelayedCall(Piece.MoveDuration, () =>
            {
                if (extraPlayerActions > 0)
                {
                    extraPlayerActions--;
                    RefreshPlayerTurn();
                }
                else
                {
                    if (!IsArenaMode) lockedPiece = null;
                    // RequestAIMove는 NextTurn 콜백 완료 후 호출해 GetLegalMoves와의 충돌을 방지.
                    NextTurn(() => RequestAIMove());
                }
            });
        }
    }

    /// <summary>현재 보드 상태를 Stockfish에 동기화합니다. 투기장 종료 후 기물 복원 시 사용합니다.</summary>
    public void SyncPositionToStockfish()
    {
        BoardManager.Instance.UpdateFEN();
        string fen = BoardManager.Instance.GetFEN();
        FairyStockfishBridge.Instance.SetPosition(fen);
        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        BoardManager.Instance.UpdatePiecesCanMovePos(moves);
    }

    public void RequestAIMove()
    {
        if (IsEndGame)
            return;

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
        if (IsArenaMode)
        {
            // 투기장 중 체크메이트는 아레나 정리 후 처리 (OnCheckmate 직접 호출 시 RequestAIMove와 경합)
            if (moves.Length == 0 && FairyStockfishBridge.Instance.IsInCheck())
                ArenaManager.Instance.EndArena(ArenaResult.OpponentCheckmated);
            return;
        }

        if (FinishType != GameResult.None) return;
        if (BoardManager.Instance.GetHalfmoveClock() >= 150)
        {
            FinishType = GameResult.Draw;
        }
        bool isCheck = FairyStockfishBridge.Instance.IsInCheck();


        if (moves.Length == 0)
        {
            if (isCheck)
                OnCheckmate();
            else
                FinishType = GameResult.Draw;
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

    public void OnSurrender(PieceColor color)
    {
        if (color == PlayerColor)
        {
            FinishType = GameResult.BlackWin;
            Debug.Log("플레이어 항복");
        }
        else
        {
            FinishType = GameResult.WhiteWin;
            Debug.Log("AI 항복");
        }
        ApplyGameResult();
    }

    private void OnCheckmate()
    {
        if (NowTurn == 'w')
        {
            FinishType = GameResult.BlackWin;
            Debug.Log("체크메이트");
            Debug.Log("흑 승");
        }
        else
        {
            FinishType = GameResult.WhiteWin;
            Debug.Log("체크메이트");
            Debug.Log("백 승");
        }
    }

    private void ApplyGameResult()
    {
        if (IsEndGame)
            return;
        if (FinishType == GameResult.None) return;

        switch (FinishType)
        {
            case GameResult.WhiteWin:
                Debug.Log("플레이어 승리");
                break;
            case GameResult.BlackWin:
                Debug.Log("AI 승리");
                break;
            case GameResult.Draw:
                Debug.Log("무승부");
                break;
        }

        MapManager.Instance.OnCombatCleared();
        EndGame();
    }

    private void EndGame()
    {
        IsEndGame = true;
        UI.ShowEndGame(FinishType);
    }
}
