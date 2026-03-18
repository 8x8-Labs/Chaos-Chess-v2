using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ChessTestUI : MonoBehaviour
{
    [Header("UI 연결")]
    public Text statusText;
    public Text moveHistoryText;
    public Text legalMovesText;
    public InputField moveInputField;
    public Button submitMoveBtn;
    public Button newGameBtn;
    public Button aiMoveBtn;
    public Button getLegalMovesBtn;
    public ScrollRect moveHistoryScroll;

    private ChessGameManager _gameManager;
    private List<string> _moveLog = new List<string>();

    void Start()
    {
        _gameManager = FindFirstObjectByType<ChessGameManager>();
        if (_gameManager == null)
        {
            var go = new GameObject("ChessGameManager");
            _gameManager = go.AddComponent<ChessGameManager>();
        }

        // 버튼 이벤트 연결
        submitMoveBtn?.onClick.AddListener(OnSubmitMove);
        newGameBtn?.onClick.AddListener(OnNewGame);
        aiMoveBtn?.onClick.AddListener(OnRequestAIMove);
        getLegalMovesBtn?.onClick.AddListener(OnGetLegalMoves);

        UpdateStatus("게임 시작! 흰색 차례");
    }

    // ── 플레이어 수 입력 ───────────────────────────────
    void OnSubmitMove()
    {
        if (moveInputField == null) return;
        string move = moveInputField.text.Trim().ToLower();
        if (string.IsNullOrEmpty(move)) return;

        if (!_gameManager.isPlayerTurn)
        {
            UpdateStatus("AI 생각 중입니다. 잠시 기다려주세요.");
            return;
        }

        AddMoveLog("플레이어: " + move);
        _gameManager.PlayerMove(move);
        moveInputField.text = "";
        UpdateStatus("AI 생각 중...");
    }

    // ── AI 수 요청 ────────────────────────────────────
    void OnRequestAIMove()
    {
        if (_gameManager.isPlayerTurn)
        {
            UpdateStatus("플레이어 차례입니다.");
            return;
        }

        UpdateStatus("AI 생각 중...");
        _gameManager.RequestAIMove();
    }

    // ── 새 게임 ───────────────────────────────────────
    void OnNewGame()
    {
        _moveLog.Clear();
        UpdateMoveHistory("");
        UpdateLegalMoves("");
        _gameManager.NewGame();
        UpdateStatus("새 게임 시작! 흰색 차례");
    }

    // ── 합법적인 수 목록 ──────────────────────────────
    void OnGetLegalMoves()
    {
        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        string result = string.Join(", ", moves);
        UpdateLegalMoves(result);
        UpdateStatus("합법적인 수: " + moves.Length + "개");
    }

    // ── UI 업데이트 헬퍼 ──────────────────────────────
    private void AddMoveLog(string entry)
    {
        _moveLog.Add(entry);
        if (_moveLog.Count > 20) _moveLog.RemoveAt(0);
        UpdateMoveHistory(string.Join("\n", _moveLog));
    }

    public void UpdateStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log("[UI] " + msg);
    }

    public void UpdateMoveHistory(string history)
    {
        if (moveHistoryText != null)
        {
            moveHistoryText.text = history;
            if (moveHistoryScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                moveHistoryScroll.verticalNormalizedPosition = 0f; // 맨 아래로
            }
        }
    }

    public void UpdateLegalMoves(string moves)
    {
        if (legalMovesText != null) legalMovesText.text = moves;
    }

    // AI가 수를 두면 UI 업데이트 (ChessGameManager에서 호출)
    public void OnAIMoveCompleted(string move)
    {
        AddMoveLog("AI: " + move);
        UpdateStatus("플레이어 차례 (흰색)");
    }
}
