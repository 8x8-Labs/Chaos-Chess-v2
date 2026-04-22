using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 명령 복종 - 타일 전용 (고급)
/// 특정 칸을 명령 칸으로 지정합니다.
/// 이 칸에 들어간 기물은 일정 턴동안 지정된 칸으로 이동해야 합니다.
/// 명령을 3번 수행할 경우 기물의 죽음을 1회 무효화합니다.
/// </summary>
public class ObeyOrderCard : CardData, ITileCard
{
    private TileSelector selector;
    private void Awake()
    {
        selector = FindFirstObjectByType<TileSelector>();
    }

    public void LoadTileSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<TileSelector>();
        selector.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        ObeyOrderEffect effect = CreateTileEffector<ObeyOrderEffect>(args.TargetPos[0]);
        effect.Apply();

        effect.DataSO = DataSO;
    }
}

public class ObeyOrderEffect : TileEffector
{
    public CardDataSO DataSO;

    private enum ObeyState { Idle, WaitingForDest, WatchingObedience }

    public int ObeyCount = 0;
    private Piece enterPiece = null;
    public Piece EnterPiece => enterPiece;

    private ObeyState _state = ObeyState.Idle;
    private readonly List<TileEffector> _childEffects = new List<TileEffector>();

    protected override void OnApply()
    {
        // 타일 이펙트 추가
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.SetTileEffect(tilePos, DataSO.EffectTileBase);

        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        // 타일 이펙트 제거
        if (DataSO.NeedEffectTileBase)
            BoardManager.Instance.TileEffectDrawer.ClearTileEffect(tilePos);

        UnsubscribeFromTurnEvent();
        CleanupChildEffects();
        _state = ObeyState.Idle;
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    // Revert()를 거치지 않고 오브젝트가 파괴되는 예외적 경로 대비
    private void OnDestroy()
    {
        UnsubscribeFromTurnEvent();
    }

    public override void OnPieceEnter(Piece piece)
    {
        Debug.Log("입장");

        if (_state == ObeyState.Idle)
        {
            enterPiece = piece;
            Debug.Log("enterPiece 삽입 완료");
            TransitionTo(ObeyState.WaitingForDest);
        }
        else if (piece != enterPiece)
        {
            Revert();
        }
    }

    // ── 이벤트 구독/해제 ────────────────────────────────────────
    private void SubscribeToTurnEvent()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerTurnStarted += OnPlayerTurnStarted;
    }

    private void UnsubscribeFromTurnEvent()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerTurnStarted -= OnPlayerTurnStarted;
    }

    // ── 상태 전환 ───────────────────────────────────────────────
    private void TransitionTo(ObeyState newState)
    {
        UnsubscribeFromTurnEvent();
        _state = newState;

        if (newState == ObeyState.WaitingForDest || newState == ObeyState.WatchingObedience)
            SubscribeToTurnEvent();
    }

    // ── 플레이어 턴 시작 핸들러 ─────────────────────────────────
    private void OnPlayerTurnStarted()
    {
        if (enterPiece == null)
        {
            Debug.Log("[ObeyOrder] 감시 기물이 없어졌습니다. 효과 제거!");
            Revert();
            return;
        }

        switch (_state)
        {
            case ObeyState.WaitingForDest:
                HandleWaitingForDest();
                break;
            case ObeyState.WatchingObedience:
                HandleDisobedience();
                break;
        }
    }

    private void HandleWaitingForDest()
    {
        List<Vector3Int> moves = enterPiece.CanMovePos;

        if (moves == null || moves.Count == 0)
        {
            Debug.LogWarning("[ObeyOrder] CanMovePos가 비어있습니다. 다음 턴 재시도.");
            return; // 구독 유지, 다음 플레이어 턴에 재시도
        }

        Vector3Int destPos = moves[Random.Range(0, moves.Count)];
        Debug.Log("[ObeyOrder] 다음 위치 : " + destPos);

        GameObject destHost = new GameObject($"ObeyDestEffect_{destPos}");
        ObeyDestEffect destEffect = destHost.AddComponent<ObeyDestEffect>();
        destEffect.Init(destPos, -1);
        destEffect.Parent = this;
        destEffect.Apply();
        _childEffects.Add(destEffect);

        TransitionTo(ObeyState.WatchingObedience);
    }

    private void HandleDisobedience()
    {
        Debug.Log($"[ObeyOrder] {enterPiece.name} 이(가) 명령을 불복종했습니다. 효과 제거!");
        Revert();
    }

    // ── 명령 이행 처리 ──────────────────────────────────────────
    public void FulfillCommand(Piece piece)
    {
        if (piece != enterPiece) return;

        ObeyCount++;
        CleanupChildEffects();
        Debug.Log($"[ObeyOrder] {piece.name} 명령 이행 #{ObeyCount}");

        if (ObeyCount >= 3)
        {
            piece.SetInvincible();
            Debug.Log($"[ObeyOrder] {piece.name} 이(가) 명령을 3회 수행했습니다. 죽음 무효화 효과 발동!");
            enterPiece = null;
            Revert();
            return;
        }

        // 다음 목적지 대기 상태로 전환 (구독은 TransitionTo가 관리)
        TransitionTo(ObeyState.WaitingForDest);
    }

    // ── 헬퍼 ────────────────────────────────────────────────────
    private void CleanupChildEffects()
    {
        foreach (TileEffector e in _childEffects)
        {
            if (e != null) e.Revert();
        }
        _childEffects.Clear();
    }
}

public class ObeyDestEffect : TileEffector
{
    public ObeyOrderEffect Parent;

    protected override void OnApply()
    {
        BoardManager.Instance.RegisterTileEffector(tilePos, this);
    }

    protected override void OnRevert()
    {
        BoardManager.Instance.UnregisterTileEffector(tilePos, this);
        Destroy(gameObject);
    }

    public override void OnPieceEnter(Piece piece)
    {
        Parent?.FulfillCommand(piece);
    }
}
