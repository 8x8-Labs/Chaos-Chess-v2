using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public enum ArenaResult { PlayerWon, Timeout, OpponentCheckmated }

/// <summary>
/// 투기장 카드 효과를 관리하는 싱글턴 매니저.
///
/// [동작 원리]
/// 1. StartArena() 호출 시 비투기장 기물을 보드에서 숨기고 (HidePiece)
///    양쪽 King만 남겨 Stockfish가 정상적으로 수를 계산할 수 있도록 유지합니다.
/// 2. King은 IsInvincible 상태로 설정해 포획되지 않도록 하며,
///    매 반턴(OnHalfTurnChanged)마다 무적을 갱신합니다.
/// 3. 플레이어 기물 전체가 이동 가능하며, 상대 기물은 Stockfish가 조종합니다.
/// 4. 승패 조건이 충족되거나 8턴이 초과되면 EndArena()로 결과를 처리합니다.
///
/// [승패 조건]
/// - PlayerWon        : 상대 투기장 기물 3개 모두 포획 → 해당 기물만 영구 제거
/// - Timeout          : 8 플레이어 턴 초과 → 무효, 기물 복원 후 메인 게임 재개
/// - OpponentCheckmated : 투기장 내 체크메이트 → 플레이어 게임 승리
/// </summary>
public class ArenaManager : MonoBehaviour
{
    public static ArenaManager Instance;

    // 투기장 참가 기물
    private List<Piece> opponentArenaPieces = new();  // 상대 투기장 기물 (최대 3개)

    // 숨겨진 기물 목록 (투기장 종료 시 복원 대상)
    private List<Piece> hiddenPieces = new();

    // Stockfish 호환을 위해 보드에 남기는 King 참조
    private Piece playerKing;
    private Piece opponentKing;

    private int playerMoveCount;  // 플레이어가 투기장에서 이동한 횟수 (최대 8)
    private bool isArenaActive;   // 중복 StartArena / EndArena 호출 방지용 플래그

    // Timeout 복원용: 아레나 시작 시점의 모든 기물 위치 스냅샷
    private Dictionary<Piece, Vector3Int> savedPositions = new();
    private string savedCastlingFen;  // 아레나 시작 시점의 캐슬링 권한 (Timeout 시 복원)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 투기장을 시작합니다.
    /// </summary>
    /// <param name="opponents">투기장에 참여할 상대 기물 목록 (최대 3개)</param>
    public void StartArena(List<Piece> opponents)
    {
        if (isArenaActive) return;
        isArenaActive = true;

        opponentArenaPieces = opponents;
        playerMoveCount = 0;
        hiddenPieces.Clear();

        BoardManager bm = BoardManager.Instance;
        GameManager gm = GameManager.Instance;
        var allPiece = bm.GetAllPieces();

        // 아레나 시작 시 모든 기물 위치 저장 (Timeout 시 완전 복원에 사용)
        savedPositions.Clear();
        foreach (Piece p in bm.GetAllPieces())
            savedPositions[p] = p.Pos;

        // 캐슬링 권한 저장 — BatchReassign이 King/Rook 복원 시 플래그를 손상시키므로 별도 보존
        bm.UpdateFEN();
        savedCastlingFen = bm.GetFEN().Split(' ')[2];

        // 양쪽 King 참조 저장 — Stockfish 연산에 필요
        playerKing = allPiece.Find(p => p.Color == gm.PlayerColor && p.Type == PieceType.King);
        opponentKing = allPiece.Find(p => p.Color == gm.EnemyColor && p.Type == PieceType.King);

        // 플레이어 전체 기물 + 상대 arena 3개 + 상대 King 외 모든 기물을 보드에서 숨김
        // — 숨겨진 기물은 SetActive(false) + 보드 배열 제거로 Stockfish FEN에 포함되지 않음
        HashSet<Piece> keepPieces = new HashSet<Piece>(
            bm.GetAllPieces().FindAll(p => p.Color == gm.PlayerColor));
        keepPieces.UnionWith(opponents);
        keepPieces.Add(opponentKing); // playerKing은 플레이어 기물로 이미 포함됨
        foreach (Piece p in allPiece)
        {
            if (!keepPieces.Contains(p))
            {
                hiddenPieces.Add(p);
                bm.HidePiece(p);
            }
        }

        // King을 무적으로 설정 — Stockfish가 King 포획 시도 시 ConsumeInvincibility()로
        // 실제 포획 없이 NextTurn()만 호출됨. 무적은 매 반턴 OnHalfTurnChanged에서 갱신됨
        playerKing?.SetInvincible();
        opponentKing?.SetInvincible();

        // 투기장 모드 활성화 — 플레이어 기물 전체 이동 가능 (lockedPiece 없음)
        // — IsArenaMode: EvaluateGameState()의 체크메이트/스테일메이트 판정 건너뜀
        gm.IsArenaMode = true;
        gm.SetLockedPiece(null);

        // 반턴 이벤트 구독으로 포획 감지 및 턴 카운트
        gm.OnHalfTurnChanged += OnHalfTurnChanged;
    }

    /// <summary>
    /// 매 반턴 종료 시 호출됩니다.
    /// King 무적 갱신, 포획 감지, 8턴 초과 판정을 수행합니다.
    /// </summary>
    private void OnHalfTurnChanged()
    {
        if (!isArenaActive) return;

        // King 무적 갱신 — ApplyUCIMove에서 ConsumeInvincibility() 호출 후 재설정
        playerKing?.SetInvincible();
        opponentKing?.SetInvincible();

        // 상대 투기장 기물 전멸 여부 확인
        if (opponentArenaPieces.Count > 0 && opponentArenaPieces.All(p => p == null))
        {
            EndArena(ArenaResult.PlayerWon);
            return;
        }

        // AI 턴이 시작됐다는 것은 플레이어가 한 수를 뒀다는 의미
        // — IsPlayerTurn이 false이면 방금 플레이어 반턴이 끝난 것
        if (!GameManager.Instance.IsPlayerTurn)
        {
            playerMoveCount++;
            if (playerMoveCount >= 8)
                EndArena(ArenaResult.Timeout);
        }
    }

    /// <summary>
    /// 투기장을 종료하고 결과를 처리합니다.
    /// </summary>
    /// <param name="result">투기장 종료 원인</param>
    public void EndArena(ArenaResult result)
    {
        if (!isArenaActive) return;
        isArenaActive = false;

        GameManager gm = GameManager.Instance;

        // 이벤트 구독 해제 및 투기장 모드 비활성화
        gm.OnHalfTurnChanged -= OnHalfTurnChanged;
        gm.IsArenaMode = false;
        gm.SetLockedPiece(null);

        // King 무적 해제 — 메인 게임 복귀 후 King이 정상적으로 포획될 수 있어야 함
        playerKing?.ConsumeInvincibility();
        opponentKing?.ConsumeInvincibility();

        // Timeout: 아레나 중 이동한 기물을 아레나 전 위치로 먼저 복원
        // — BatchReassign은 2페이즈로 동작해 겹침 없이 다수 기물을 재배치함
        // — 활성 기물을 원위치로 돌린 뒤 숨긴 기물을 복원해야 위치 충돌이 없음
        if (result == ArenaResult.Timeout)
        {
            var piecesToRestore = new List<Piece>();
            var originalPositions = new List<Vector3Int>();
            foreach (var kv in savedPositions)
            {
                Piece p = kv.Key;
                if (p != null && p.gameObject.activeSelf)
                {
                    piecesToRestore.Add(p);
                    originalPositions.Add(kv.Value);
                }
            }
            BoardManager.Instance.BatchReassign(piecesToRestore, originalPositions);
            // BatchReassign이 King/Rook 이동 시 캐슬링 플래그를 false로 설정하므로 원래 권한으로 복원
            BoardManager.Instance.SetCastlingFromFEN(savedCastlingFen);
        }

        // 숨겨두었던 비투기장 기물을 원래 위치에 복원
        foreach (Piece p in hiddenPieces)
            if (p != null) BoardManager.Instance.RestorePiece(p);
        hiddenPieces.Clear();

        switch (result)
        {
            case ArenaResult.PlayerWon:
                // 상대 투기장 기물은 이미 DestroyPiece()로 제거된 상태이므로 추가 처리 불필요
                Debug.Log("[Arena] 플레이어 승리 — 상대 투기장 기물 제거됨");
                gm.SyncPositionToStockfish();
                // RequestAIMove는 MoveSelected()가 NextTurn() 완료 후 호출 — 여기서 호출 시 GetLegalMoves와 충돌
                break;

            case ArenaResult.Timeout:
                Debug.Log("[Arena] 8턴 초과 — 무효, 메인 게임 재개");
                gm.SyncPositionToStockfish();
                // RequestAIMove는 MoveSelected()가 NextTurn() 완료 후 호출 — 여기서 호출 시 GetLegalMoves와 충돌
                break;

            case ArenaResult.OpponentCheckmated:
                Debug.Log("[Arena] 투기장 내 체크메이트 — 플레이어 게임 승리");
                gm.OnSurrender(gm.EnemyColor);
                break;
        }
    }
}
