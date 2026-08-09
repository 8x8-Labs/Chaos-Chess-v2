using System;
using System.Collections;
using System.Collections.Generic;
using ChaosChess.Unity.AIIntegration.Runtime;
using UnityEngine;
using UnityEngine.Serialization;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    private const int AiMoveTimeMs = 5000;

    // 모바일에서는 엔진이 즉시 수를 반환해 AI가 너무 빠르게 두는 느낌을 주므로,
    // 수를 적용하기 전 최소한의 연출 딜레이를 보장한다 (초 단위).
#if UNITY_ANDROID || UNITY_IOS
    private const float MinAiMoveDelaySeconds = 0.8f;
#else
    private const float MinAiMoveDelaySeconds = 0f;
#endif

    public GameResult FinishType { get; set; } = GameResult.None;

    public static GameManager Instance;

    [FormerlySerializedAs("PlayerColor")]
    [SerializeField] private PieceColor playerColor = PieceColor.White;

    /// <summary>
    /// 플레이어(로컬)가 맡은 진영입니다. 매치 시작 시 GameCycleManager가 주입합니다.
    /// </summary>
    public PieceColor PlayerColor => playerColor;

    /// <summary>
    /// 상대 진영입니다. PlayerColor에서 파생되므로 따로 설정하지 않습니다.
    /// (예전에는 독립 필드라 둘이 어긋날 수 있었습니다.)
    /// </summary>
    public PieceColor EnemyColor => CardTargetRelationExtensions.Opposite(playerColor);

    /// <summary>매치 시작 전에 플레이어 진영을 지정합니다.</summary>
    public void SetPlayerColor(PieceColor color)
    {
        playerColor = color;
    }

    public List<Sprite> BlackSprites = new List<Sprite>();
    public List<Sprite> WhiteSprites = new List<Sprite>();

    [Header("Elite Transformation")]
    [Tooltip("엘리트 노드 진입 시 변형 기물 위치에 스폰할 공통 업그레이드 연출 프리팹")]
    [SerializeField] private GameObject variantUpgradeVfxPrefab;

    [SerializeField] private int curTurn;
    /// <summary>현재 턴 번호입니다. 매치 메시지의 순서를 강제하는 데 씁니다.</summary>
    public int CurrentTurn => curTurn;

    /// <summary>
    /// 지금이 플레이어 차례인지 여부입니다. PlayerColor가 백이면 기존의 "홀수 턴" 판정과 동일합니다.
    /// </summary>
    public bool IsPlayerTurn => turnColor == PlayerColor;
    public bool IsPlayerInCheck { get; private set; }

    public bool IsGameInput = true;
    /// <summary>false이면 RequestAIMove가 무시됩니다. 카드 이펙트 랩에서 양쪽을 수동으로 두기 위해 사용합니다.</summary>
    public bool AiAutoMoveEnabled = true;
    /// <summary>이번 턴을 대신 둘 주체입니다. 지금은 AI 카드 컨트롤러이며, 멀티에서는 원격 프로바이더가 들어갑니다.</summary>
    [SerializeField] private TurnProvider turnProvider;
    public bool IsEndGame { get; private set; } = false;
    public bool IsArenaMode { get; set; } = false;
    public bool IsCardIntervalPaused => cardIntervalPauseCount > 0;
    private List<(int turn, Action action, CardRandomizerManager.ActiveCardToken token)> recievedActions = new();
    // 투기장 진행 중 기존 예약 액션(폭탄, 지속효과 해제 등) 소모를 잠시 멈춥니다.
    private bool areQueuedActionsPaused = false;
    private int queuedActionPauseTurn = -1;
    private int cardIntervalPauseCount = 0;

    /// <summary>플레이어 턴이 시작되고 CanMovePos가 유효해진 직후 발행됩니다.</summary>
    public event Action OnPlayerTurnStarted;
    /// <summary>카드 지급 주기 카운트의 일시정지 상태가 바뀔 때 발행됩니다.</summary>
    public event Action OnCardIntervalPauseChanged;
    /// <summary>매 턴(플레이어·AI 모두) 종료 직후 발행됩니다. Effector 지속 턴 카운트다운에 사용됩니다.</summary>
    public event Action OnTurnChanged;
    /// <summary>반 턴 종료 직후 발행됩니다.</summary>
    public event Action OnHalfTurnChanged;
    /// <summary>시간역행 카드 전용 이벤트 입니다.</summary>
    public event Action<Action, Action> OnTimeReversalRequired;
    /// <summary>아버지의 원수 카드 전용 이벤트 입니다.</summary>
    public event Action<Piece> OnAwakenedPieceSelected;
    /// <summary>플레이어 체크 상태가 바뀔 때 카드 UI 입력 차단 갱신에 사용됩니다.</summary>
    public event Action<bool> OnPlayerCheckStateChanged;
    private bool cancelCurrentGameStateEvaluation;

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

    // 승격 선택이 끝날 때까지 보류해 둔 로컬 착수입니다.
    private Vector3Int pendingLocalMoveFrom;
    private Vector3Int pendingLocalMoveTo;
    private bool hasPendingLocalMove;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // 이번 매치의 플레이어 진영을 런 관리자에서 받아옵니다.
            // GameCycleManager가 없는 환경(카드 이펙트 랩 등)에서는 인스펙터 값을 그대로 씁니다.
            if (GameCycleManager.Instance != null)
                playerColor = GameCycleManager.Instance.PlayerColor;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        IsEndGame = false;
        CardRandomizerManager.Instance?.ClearActiveCards();
        CardSelectionState.Reset();
        boardUI = FindFirstObjectByType<BoardUI>();
        uiManager = FindFirstObjectByType<UIManager>();
        turnProvider = ResolveTurnProvider();

        FinishType = GameResult.None;

        curTurn = 1;

        BoardManager.Instance.OnPromotionRequired -= HandlePromotion;
        BoardManager.Instance.OnPromotionRequired += HandlePromotion;

        if (boardUI != null)
        {
            BoardManager.Instance.OnLastMoveChanged -= boardUI.DrawLastMove;
            BoardManager.Instance.OnLastMoveChanged += boardUI.DrawLastMove;

            BoardManager.Instance.OnMoveBlocked -= boardUI.DrawBlockedMove;
            BoardManager.Instance.OnMoveBlocked += boardUI.DrawBlockedMove;
        }

        OnTimeReversalRequired -= HandleTimeReversal;
        OnTimeReversalRequired += HandleTimeReversal;

        // 기물이 배치되기 전에 보드 시점을 먼저 확정합니다.
        BoardManager.Instance.ApplyBoardView(PlayerColor);

        LoadMapManager();

        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        EvaluateGameState(moves);
        BoardManager.Instance.UpdatePiecesCanMovePos(moves);

        // 플레이어가 흑이면 백(AI)이 선수입니다.
        // 이후 턴은 NextTurn 콜백이 이어받지만, 첫 수만은 여기서 요청해야 대국이 시작됩니다.
        if (!IsPlayerTurn)
            RequestAIMove();
    }

    /// <summary>
    /// 게임 모드에 맞는 턴 프로바이더를 고릅니다.
    /// 한 씬에 AI용과 원격용이 함께 있어도 모드가 결정하므로, 오브젝트를 켜고 끌 필요가 없습니다.
    /// </summary>
    private TurnProvider ResolveTurnProvider()
    {
        GameMode mode = GameCycleManager.Instance != null
            ? GameCycleManager.Instance.CurrentMode
            : GameMode.Run;
        bool needRemote = mode == GameMode.Multiplayer;

        TurnProvider resolved = null;

        // 인스펙터로 지정한 프로바이더가 모드와 맞으면 그대로 씁니다.
        if (turnProvider != null && turnProvider.IsRemote == needRemote)
        {
            resolved = turnProvider;
        }
        else
        {
            foreach (TurnProvider candidate in FindObjectsByType<TurnProvider>(FindObjectsSortMode.None))
            {
                if (candidate.IsRemote == needRemote)
                {
                    resolved = candidate;
                    break;
                }
            }
        }

        Debug.Log($"[TurnProvider] 모드 {mode} " +
                  $"(GameCycleManager {(GameCycleManager.Instance != null ? "있음" : "없음")}) " +
                  $"→ 선택: {(resolved != null ? resolved.GetType().Name : "없음")}");

        if (resolved == null && needRemote)
            Debug.LogError("[Network] 멀티플레이 모드인데 씬에서 RemoteTurnProvider를 찾지 못했습니다.");

        return resolved;
    }

    /// <summary>
    /// MapManager에서 FEN과 ELO(맵의 전체적인 상태)를 받아와서 스톡피쉬에 적용한다
    /// </summary>
    private void LoadMapManager()
    {
        FairyStockfishBridge.Instance.InitEngine("chaoschess");
        if (MapManager.Instance != null && MapManager.Instance.curMap != null)
        {
            int elo = MapManager.Instance.curMap.ELO;
            FairyStockfishBridge.Instance.SetElo(elo);

            string fen = MapManager.Instance.curMap.FEN;
            BoardManager.Instance.LoadFEN(fen);

            if (MapManager.Instance.curMap.nodeType == NodeType.Elite)
                ApplyEliteTransformation();
        }
        else
        {
            FairyStockfishBridge.Instance.SetElo(1000);

            BoardManager.Instance.LoadFEN();
        }
    }

    /// <summary>
    /// 엘리트 노드 진입 시 적 기물 일부를 변형 기물(Amazon/Chancellor/KnightRider)로 교체한다.
    /// </summary>
    private void ApplyEliteTransformation()
    {
        List<Piece> candidates = new List<Piece>();
        candidates.AddRange(BoardManager.Instance.GetPiece<Knight>(EnemyColor));
        candidates.AddRange(BoardManager.Instance.GetPiece<Bishop>(EnemyColor));
        candidates.AddRange(BoardManager.Instance.GetPiece<Rook>(EnemyColor));
        candidates.AddRange(BoardManager.Instance.GetPiece<Queen>(EnemyColor));
        if (candidates.Count == 0) return;

        char[] variantChars = { 's', 'y', 'z' }; // Amazon, Chancellor, KnightRider

        // 이번 입장에서 새로 등장한 변형 기물 종류 (중복 제거). 설명 UI 표시에 사용합니다.
        List<PieceType> introduced = new List<PieceType>();

        int count = Mathf.Min(UnityEngine.Random.Range(1, 3), candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
            Piece target = candidates[randomIndex];
            candidates.RemoveAt(randomIndex);

            char variantChar = variantChars[UnityEngine.Random.Range(0, variantChars.Length)];
            Vector3Int pos = target.Pos;
            BoardManager.Instance.ChangePiece(pos, EnemyColor, variantChar);

            // 업그레이드 연출을 변형된 기물 위치에 스폰합니다.
            if (variantUpgradeVfxPrefab != null)
            {
                Vector3 worldPos = BoardManager.Instance.GridPosToWorldPos(pos);
                Instantiate(variantUpgradeVfxPrefab, worldPos, Quaternion.identity);
            }

            PieceType type = VariantCharToPieceType(variantChar);
            if (type != PieceType.None && !introduced.Contains(type))
                introduced.Add(type);
        }

        // 패널 표시는 다음 프레임으로 미룹니다.
        // (씬 로드 중 Start()에서 호출되므로, 패널 자신의 Start()가 끝나기 전에
        //  EnablePanel을 호출하면 패널의 DisablePanel에 덮어써질 수 있습니다.)
        if (introduced.Count > 0)
            StartCoroutine(ShowVariantInfoNextFrame(introduced));
    }

    private System.Collections.IEnumerator ShowVariantInfoNextFrame(List<PieceType> types)
    {
        yield return null;
        UI?.ShowVariantPieceInfo(types);
    }

    private static PieceType VariantCharToPieceType(char variantChar) => variantChar switch
    {
        's' => PieceType.Amazon,
        'y' => PieceType.Chancellor,
        'z' => PieceType.KnightRider,
        _ => PieceType.None
    };

    /// <summary>AI ELO를 delta만큼 조정합니다.</summary>
    public void ModifyELO(int delta)
    {
        int elo = MapManager.Instance.curMap.ELO + delta;
        MapManager.Instance.curMap.ELO = elo;
        FairyStockfishBridge.Instance.SetElo(elo);
    }

    public void SelectGrid(Vector3Int pos)
    {
        // AI가 켜져 있으면 플레이어(화이트) 턴에만 입력을 허용합니다.
        // AI를 끄면(카드 랩) 양쪽을 수동으로 둘 수 있도록 턴 색 제한을 해제합니다.
        if (!IsPlayerTurn && AiAutoMoveEnabled) return;
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
            boardUI?.DeleteSelectTile();
            boardUI?.DeleteValidMoveTiles();
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
            ApplyGameResult();
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

            // 승격 문자까지 확정됐으므로 이제 착수를 보냅니다.
            if (hasPendingLocalMove)
            {
                hasPendingLocalMove = false;
                NotifyLocalMove(pendingLocalMoveFrom, pendingLocalMoveTo, type);
            }

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

            BoardManager.Instance.CheckKingExistence();

            ApplyGameResult();

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
        CardRandomizerManager.ActiveCardToken activeCardToken =
            CardRandomizerManager.Instance?.RetainActiveCard();

        recievedActions.Add((curTurn + x * 2, act, activeCardToken));
    }

    /// <summary>
    /// 액션 발동 후 리스트에서 제거하는 행동을 수행합니다.
    /// </summary>
    public void ReturnAction()
    {
        if (areQueuedActionsPaused)
            return;

        for (int i = recievedActions.Count - 1; i >= 0; i--)
        {
            var item = recievedActions[i];

            if (item.turn == curTurn)
            {
                try
                {
                    item.action?.Invoke();
                }
                finally
                {
                    item.token?.Complete();
                    recievedActions.RemoveAt(i);
                }
            }
        }

    }

    /// <summary>예약 액션 소모를 일시 정지합니다.</summary>
    public void PauseQueuedActions()
    {
        if (areQueuedActionsPaused)
            return;

        areQueuedActionsPaused = true;
        queuedActionPauseTurn = curTurn;
    }

    /// <summary>
    /// 예약 액션 소모를 재개합니다.
    /// 일시정지 중 흘러간 턴 수만큼 예약 트리거 턴을 뒤로 밀어, 남은 지속 턴을 보존합니다.
    /// </summary>
    public void ResumeQueuedActions()
    {
        if (!areQueuedActionsPaused)
            return;

        int delta = curTurn - queuedActionPauseTurn;
        if (delta != 0)
        {
            for (int i = 0; i < recievedActions.Count; i++)
            {
                var item = recievedActions[i];
                recievedActions[i] = (item.turn + delta, item.action, item.token);
            }
        }

        areQueuedActionsPaused = false;
        queuedActionPauseTurn = -1;
    }

    /// <summary>대기 중인 카드 예약 작업과 추가 행동 상태를 결과 발동 없이 취소합니다.</summary>
    public void ClearQueuedCardActions()
    {
        foreach (var item in recievedActions)
            item.token?.Complete();

        recievedActions.Clear();
        areQueuedActionsPaused = false;
        queuedActionPauseTurn = -1;
        extraPlayerActions = 0;
        lockedPiece = null;
        UI?.HideAwakenButton();
    }

    /// <summary>카드 지급 주기 카운트를 일시 정지합니다.</summary>
    public void PushCardIntervalPause()
    {
        cardIntervalPauseCount++;
        OnCardIntervalPauseChanged?.Invoke();
    }

    /// <summary>카드 지급 주기 카운트 일시 정지를 해제합니다.</summary>
    public void PopCardIntervalPause()
    {
        cardIntervalPauseCount = Mathf.Max(0, cardIntervalPauseCount - 1);
        OnCardIntervalPauseChanged?.Invoke();
    }

    /// <summary>
    /// 로컬 착수를 원격에 알립니다. 승격은 문자가 정해진 뒤에 불러야 UCI가 완성됩니다.
    /// 원격 대전이 아니면 프로바이더가 그대로 무시합니다.
    /// </summary>
    private void NotifyLocalMove(Vector3Int from, Vector3Int to, char promotion = '\0')
    {
        if (turnProvider == null || BoardManager.Instance == null) return;

        string uci = BoardManager.Instance.GridTOUCI(from) + BoardManager.Instance.GridTOUCI(to);
        if (promotion != '\0')
            uci += char.ToLower(promotion);

        turnProvider.SendLocalAction(MatchMessage.CreateMove(CurrentTurn, uci));
    }

    /// <summary>
    /// 로컬 플레이어가 사용한 카드를 원격에 알립니다.
    /// 카드 효과가 턴을 넘길 수도 있으므로 효과를 적용하기 전에 불러야 합니다.
    /// </summary>
    public void NotifyLocalCard(CardDataSO cardSO, IReadOnlyList<Vector3Int> targets)
    {
        if (turnProvider == null || cardSO == null || BoardManager.Instance == null) return;

        if (string.IsNullOrEmpty(cardSO.AiCardId))
        {
            Debug.LogWarning($"[Network] '{cardSO.CardName}'에 AiCardId가 없어 상대에게 전달할 수 없습니다.");
            return;
        }

        string[] squares = null;
        if (targets != null && targets.Count > 0)
        {
            squares = new string[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                squares[i] = BoardManager.Instance.GridTOUCI(targets[i]);
        }

        turnProvider.SendLocalAction(MatchMessage.CreateCard(CurrentTurn, cardSO.AiCardId, squares));
    }

    // MoveSelected 안에서 플레이어 수 적용 후:
    private void MoveSelected(Vector3Int target)
    {
        if (selectedPiece == null) return;

        Piece piece = selectedPiece;
        selectedPiece = null;

        // MovePiece가 위치를 바꾸기 전에 출발 칸을 기록해 둡니다.
        Vector3Int from = piece.Pos;

        if (BoardManager.Instance.MovePiece(piece, target))
        {
            BoardManager.Instance.RefreshMoves();
            boardUI?.DeleteSelectTile();
            boardUI?.DeleteValidMoveTiles();

            // 프로모션이면 여기서 멈춤
            if (!IsGameInput)
            {
                // 승격 문자가 정해질 때까지 송신을 미룹니다.
                pendingLocalMoveFrom = from;
                pendingLocalMoveTo = target;
                hasPendingLocalMove = true;
                return;
            }

            NotifyLocalMove(from, target);

            DOVirtual.DelayedCall(Piece.MoveDuration, () =>
            {
                if (extraPlayerActions > 0)
                {
                    // 데스페라도 추가 행동 중에도, 이미 상대가 체크메이트면 즉시 게임 종료합니다.
                    if (TryApplyImmediateOpponentCheckmate())
                        return;

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

    /// <summary>
    /// 현재 보드에서 "상대 턴" 기준으로 체크메이트를 판정해 즉시 승패를 적용합니다.
    /// 데스페라도처럼 턴을 넘기지 않는 추가 행동 분기에서 사용합니다.
    /// </summary>
    private bool TryApplyImmediateOpponentCheckmate()
    {
        BoardManager.Instance.UpdateFEN();
        string currentFen = BoardManager.Instance.GetFEN();

        string[] fenParts = currentFen.Split(' ');
        if (fenParts.Length < 2)
            return false;

        string originalTurn = fenParts[1];
        fenParts[1] = (originalTurn == "w") ? "b" : "w";
        string opponentTurnFen = string.Join(" ", fenParts);

        FairyStockfishBridge.Instance.SetPosition(opponentTurnFen);
        string[] opponentMoves = FairyStockfishBridge.Instance.GetLegalMoves();
        bool opponentInCheck = FairyStockfishBridge.Instance.IsInCheck();

        // 이후 흐름을 위해 엔진 포지션을 원래 턴 상태로 복원합니다.
        FairyStockfishBridge.Instance.SetPosition(currentFen);

        if (opponentMoves.Length == 0 && opponentInCheck)
        {
            OnSurrender(EnemyColor);
            return true;
        }

        return false;
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

    /// <summary>
    /// 보드 강제 재배치 직전 선택/하이라이트/애니메이션 상태를 정리합니다.
    /// </summary>
    public void CancelCurrentSelectionForBoardTransition()
    {
        selectedPiece = null;
        boardUI?.DeleteSelectTile();
        boardUI?.DeleteValidMoveTiles();
    }

    public void RequestAIMove()
    {
        if (IsEndGame)
            return;

        if (!AiAutoMoveEnabled)
            return;

        if (turnProvider != null &&
            turnProvider.TryRequestTurn(this, BoardManager.Instance, RequestStockfishAIMove))
        {
            return;
        }

        RequestStockfishAIMove();
    }

    private void RequestStockfishAIMove()
    {
        if (IsEndGame || !AiAutoMoveEnabled || BoardManager.Instance == null)
            return;

        float requestTime = Time.realtimeSinceStartup;

        FairyStockfishBridge.Instance.GetBestMoveAsync(
            depth: 12,
            moveTimeMs: AiMoveTimeMs,
            callback: (uciMove) =>
            {
                // 콜백 도착 시점에 인스턴스가 파괴되었으면 지연 스케줄링조차 하지 않는다.
                if (this == null)
                    return;

                RunAfterMinAiDelay(requestTime, () =>
                {
                    // 지연 도중 씬 전환으로 인스턴스가 파괴되었을 수 있어 유효성 재확인
                    if (this == null || BoardManager.Instance == null)
                        return;

                    if (IsEndGame)
                        return;

                    if (BoardManager.Instance.IsValidUciMove(uciMove))
                    {
                        BoardManager.Instance.ApplyUCIMove(uciMove);
                        return;
                    }

                    Debug.LogWarning($"[AI] Stockfish returned invalid move '{uciMove}'. Using random legal fallback.");
                    ApplyFallbackLegalAIMove();
                });
            }
        );
    }

    // 엔진 응답이 최소 연출 시간보다 빨리 도착하면 남은 시간만큼 지연 후 실행한다.
    private void RunAfterMinAiDelay(float requestTime, Action action)
    {
        float elapsed = Time.realtimeSinceStartup - requestTime;
        float remaining = MinAiMoveDelaySeconds - elapsed;

        if (remaining <= 0f)
        {
            action();
            return;
        }

        DOVirtual.DelayedCall(remaining, () => action(), ignoreTimeScale: true);
    }

    private void ApplyFallbackLegalAIMove()
    {
        FairyStockfishBridge.Instance.GetLegalMovesAsync(moves =>
        {
            if (moves == null || moves.Length == 0)
            {
                EvaluateGameState(Array.Empty<string>());
                ApplyGameResult();
                return;
            }

            string randomMove = ChooseRandomLegalMove(moves);
            if (randomMove == "none")
            {
                Debug.LogWarning("[AI] No valid legal moves found after filtering. Ending game.");
                EvaluateGameState(Array.Empty<string>());
                ApplyGameResult();
                return;
            }

            BoardManager.Instance.ApplyUCIMove(randomMove);
        });
    }

    private string ChooseRandomLegalMove(string[] moves)
    {
        string selectedMove = "none";
        int count = 0;

        foreach (string move in moves)
        {
            if (!BoardManager.Instance.IsValidUciMove(move))
                continue;

            count++;
            if (UnityEngine.Random.Range(0, count) == 0)
                selectedMove = move;
        }

        return selectedMove;
    }

    private void EvaluateGameState(string[] moves)
    {
        if (IsArenaMode)
        {
            // 투기장 중 체크메이트는 아레나 정리 후 처리 (OnCheckmate 직접 호출 시 RequestAIMove와 경합)
            if (moves.Length == 0 && FairyStockfishBridge.Instance.IsInCheck())
            {
                ArenaManager.Instance.EndArena(ArenaResult.OpponentCheckmated);
                ResetActions();
            }
            UpdatePlayerCheckState(false);
            return;
        }

        if (FinishType != GameResult.None)
        {
            UpdatePlayerCheckState(false);
            return;
        }
        if (BoardManager.Instance.GetHalfmoveClock() >= 150)
        {
            FinishType = GameResult.Draw;
        }
        else if (FairyStockfishBridge.Instance.IsInsufficientMaterial())
        {
            FinishType = GameResult.Draw;
        }
        bool isCheck = FairyStockfishBridge.Instance.IsInCheck();
        UpdatePlayerCheckState(IsPlayerTurn && isCheck);

        if (cancelCurrentGameStateEvaluation)
        {
            cancelCurrentGameStateEvaluation = false;
            return;
        }

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

    public void SetGameInput(bool enabled)
    {
        IsGameInput = enabled;
    }

    private void UpdatePlayerCheckState(bool isPlayerInCheck)
    {
        if (IsPlayerInCheck == isPlayerInCheck)
            return;

        IsPlayerInCheck = isPlayerInCheck;
        OnPlayerCheckStateChanged?.Invoke(IsPlayerInCheck);
    }

    public void ReevaluateGameState()
    {
        cancelCurrentGameStateEvaluation = true;
        StartCoroutine(ReevaluateGameStateNextFrame());
    }

    private IEnumerator ReevaluateGameStateNextFrame()
    {
        yield return null;

        BoardManager.Instance.RefreshMoves();
        string[] moves = FairyStockfishBridge.Instance.GetLegalMoves();
        EvaluateGameState(moves);
        ApplyGameResult();
    }

    private void OnCheck()
    {
        Debug.Log("체크");
    }

    public void OnSurrender(PieceColor color)
    {
        // 항복한 진영의 반대편이 승리합니다. 승패는 플레이어가 어느 색이든 색 자체로 결정됩니다.
        FinishType = color == PieceColor.White ? GameResult.BlackWin : GameResult.WhiteWin;
        Debug.Log(color == PlayerColor ? "플레이어 항복" : "AI 항복");
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

        if (GameCycleManager.Instance != null && !GameCycleManager.Instance.IsPracticeMode)
            MapManager.Instance?.OnCombatCleared();
        EndGame();
    }

    private void EndGame()
    {
        IsEndGame = true;

        UI.ShowEndGame(FinishType);
        if (GameCycleManager.Instance != null && !GameCycleManager.Instance.IsPracticeMode)
            PlayerState.Instance?.EndGame(FinishType);
    }

    /// <summary>
    /// 모든 액션을 지웁니다
    /// </summary>
    private void ResetActions()
    {
        OnPlayerTurnStarted = null;
        OnCardIntervalPauseChanged = null;
        OnTurnChanged = null;
        OnHalfTurnChanged = null;
        OnTimeReversalRequired = null;
        OnAwakenedPieceSelected = null;
        OnPlayerCheckStateChanged = null;
    }
}
