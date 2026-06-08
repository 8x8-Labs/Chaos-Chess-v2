using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>투기장 진입 시에도 일시정지하지 않고 유지할 효과 마커입니다.</summary>
public interface IArenaPersistentEffect { }

/// <summary>기물의 행마/표현 override를 점유하는 효과 마커입니다.</summary>
public interface IMovementOverrideEffect { }

/// <summary>기물 또는 타일에 효과를 적용하고 관리하는 추상 컴포넌트</summary>
public abstract class Effector : MonoBehaviour, IEffect
{
    public static event System.Action<Effector> OnAnyEffectApplied;
    public static event System.Action<Effector> OnAnyEffectReverted;
    public static event System.Action<Effector> OnAnyEffectTurnTicked;

    private int remainingTurns; // -1 = 영구 효과
    private bool useHalfTurn; // 반턴 사용 여부
    private bool isApplied;
    private bool hasNotifiedApplied;
    private CardRandomizerManager.ActiveCardToken activeCardToken;
    // 투기장 등 특수 모드에서 효과 동작을 잠시 멈출 때 사용합니다.
    private bool isSuspended;

    public CardDataSO CardSO { get; set; }
    public bool IsExpired => remainingTurns == 0;
    public bool IsPermanent => remainingTurns < 0;
    public int RemainingTurns => remainingTurns;
    public bool IsSuspended => isSuspended;
    protected bool IsApplied => isApplied;

    protected void SetDuration(int turns)
    {
        remainingTurns = turns;
    }

    /// <summary>효과를 대상에 등록합니다. 턴 이벤트를 구독하고 OnApply()를 호출합니다.</summary>
    public void Apply(bool halfTurn = false)
    {
        if (isApplied) return;

        activeCardToken = CardRandomizerManager.Instance?.RetainActiveCard(CardSO);

        isApplied = true;
        GameManager.Instance.OnTurnChanged += OnTurnChanged;

        useHalfTurn = halfTurn;
        if (useHalfTurn)
            GameManager.Instance.OnHalfTurnChanged += OnHalfTurnChanged;

        OnApply();

        if (!isApplied) return;

        if (IsExpired)
        {
            Revert();
            return;
        }

        hasNotifiedApplied = true;
        OnAnyEffectApplied?.Invoke(this);
        OnEffectApplied();
    }

    /// <summary>효과를 대상에서 제거합니다. 턴 이벤트를 해제하고 OnRevert()를 호출합니다.</summary>
    public void Revert()
    {
        if (!isApplied) return;

        isApplied = false;
        GameManager.Instance.OnTurnChanged -= OnTurnChanged;
        if (useHalfTurn)
            GameManager.Instance.OnHalfTurnChanged -= OnHalfTurnChanged;

        bool shouldNotifyReverted = hasNotifiedApplied;
        hasNotifiedApplied = false;

        OnRevert();
        if (shouldNotifyReverted)
            OnAnyEffectReverted?.Invoke(this);

        OnEffectReverted();
        activeCardToken?.Complete();
        activeCardToken = null;
    }

    protected virtual void OnDestroy()
    {
        if (!isApplied) return;

        isApplied = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTurnChanged -= OnTurnChanged;
            if (useHalfTurn)
                GameManager.Instance.OnHalfTurnChanged -= OnHalfTurnChanged;
        }

        if (hasNotifiedApplied)
            OnAnyEffectReverted?.Invoke(this);

        hasNotifiedApplied = false;
        activeCardToken?.Complete();
        activeCardToken = null;
    }

    /// <summary>투기장 동안 효과를 일시 정지합니다.</summary>
    public void SuspendForArena()
    {
        if (isSuspended) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTurnChanged -= OnTurnChanged;
            if (useHalfTurn)
                GameManager.Instance.OnHalfTurnChanged -= OnHalfTurnChanged;
        }

        isSuspended = true;
        ToggleTileVisual(false);
        OnSuspendForArena();
        OnAnyEffectTurnTicked?.Invoke(this);
    }

    /// <summary>투기장 종료 후 효과를 재개합니다.</summary>
    public void ResumeFromArena()
    {
        if (!isSuspended) return;

        isSuspended = false;
        ToggleTileVisual(true);
        OnResumeFromArena();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTurnChanged += OnTurnChanged;
            if (useHalfTurn)
                GameManager.Instance.OnHalfTurnChanged += OnHalfTurnChanged;
        }

        OnAnyEffectTurnTicked?.Invoke(this);
    }

    /// <summary>서브클래스에서 훅/버프를 부착합니다.</summary>
    protected abstract void OnApply();

    /// <summary>서브클래스에서 훅/버프를 해제합니다.</summary>
    protected abstract void OnRevert();

    protected virtual void OnEffectApplied() { }
    protected virtual void OnEffectReverted() { }

    protected virtual void OnSuspendForArena() { }
    protected virtual void OnResumeFromArena() { }

    public virtual void OnTurnChanged()
    {
        if (!isApplied) return;
        if (IsPermanent) return;

        remainingTurns--;
        if (IsExpired)
            Revert();
        else
            OnAnyEffectTurnTicked?.Invoke(this);
    }

    protected virtual void OnHalfTurnChanged() { }

    /// <summary>효과 타입별(DataSO) 타일 연출을 끄거나 켭니다.</summary>
    private void ToggleTileVisual(bool enabled)
    {
        CardDataSO dataSO = VisualDataSO;

        bool hasAnimationFrames = dataSO != null
            && dataSO.EffectTileAnimationFrames != null
            && dataSO.EffectTileAnimationFrames.Length > 0;

        TileBase tileBase = GetVisualTileBase();
        if (dataSO == null || !dataSO.NeedEffectTileBase || (tileBase == null && !hasAnimationFrames))
            return;

        foreach (Vector3Int pos in GetVisualPositions())
        {
            if (enabled)
                BoardManager.Instance?.TileEffectDrawer?.SetTileEffect(pos, dataSO, GetVisualEffectTileIndex(), RemainingTurns);
            else
                BoardManager.Instance?.TileEffectDrawer?.ClearTileEffect(pos);
        }
    }

    /// <summary>타일 연출에 사용할 카드 데이터입니다. 필요 시 서브 클래스에서 재정의합니다.</summary>
    protected virtual CardDataSO VisualDataSO => null;

    /// <summary>타일 연출에 사용할 좌표 목록입니다. 필요 시 서브 클래스에서 재정의합니다.</summary>
    protected virtual IEnumerable<Vector3Int> GetVisualPositions() { yield break; }

    /// <summary>타일 연출에 사용할 타일베이스입니다. 필요 시 서브 클래스에서 재정의합니다.</summary>
    protected virtual TileBase GetVisualTileBase() => VisualDataSO?.EffectTileBase;

    /// <summary>선택 순서별 타일 연출 인덱스입니다. 필요 시 서브 클래스에서 재정의합니다.</summary>
    protected virtual int GetVisualEffectTileIndex() => 0;
}

/// <summary>기물에 부착되는 효과의 기반 추상 클래스</summary>
public abstract class PieceEffector : Effector, IPieceEffect
{
    private static readonly List<PieceEffector> effectorBuffer = new();

    protected Piece target;

    public static bool HasActiveMovementOverride(Piece piece)
    {
        if (piece == null) return false;
        if (piece.MoveFenOverride != null || piece.FenOverride != null) return true;

        effectorBuffer.Clear();
        try
        {
            piece.GetComponents(effectorBuffer);
            foreach (PieceEffector effector in effectorBuffer)
            {
                if (effector is IMovementOverrideEffect && !effector.IsSuspended)
                    return true;
            }
        }
        finally
        {
            effectorBuffer.Clear();
        }
        return false;
    }

    public void Init(Piece piece, int duration = -1)
    {
        target = piece;
        SetDuration(duration);
    }

    public virtual void OnPieceCaptured() { }
    public virtual void OnPieceCapture() { }
    public virtual void OnPieceMove(Vector3Int dest) { }
}

/// <summary>타일에 부착되는 효과의 기반 추상 클래스</summary>
public abstract class TileEffector : Effector, ITileEffect
{
    public Vector3Int TilePos
    {
        get
        {
            return tilePos;
        }
    }
    protected Vector3Int tilePos;
    protected int effectTileIndex;

    public void Init(Vector3Int pos, int duration = -1, int effectTileIndex = 0)
    {
        tilePos = pos;
        this.effectTileIndex = effectTileIndex;
        SetDuration(duration);
    }

    public virtual void OnPieceEnter(Piece piece) { }
    public virtual void OnPieceExit(Piece piece) { }
    public virtual bool CanPieceEnter(Piece piece, Vector3Int from, Vector3Int to) { return true; }

    public override void OnTurnChanged()
    {
        base.OnTurnChanged();

        if (IsApplied && !IsExpired)
            RefreshTileEffectTurnAnimation();
    }

    protected override void OnSuspendForArena()
    {
        BoardManager.Instance?.UnregisterTileEffector(tilePos, this);
    }

    protected override void OnResumeFromArena()
    {
        BoardManager.Instance?.RegisterTileEffector(tilePos, this);
    }

    protected override CardDataSO VisualDataSO => CardSO;

    protected override IEnumerable<Vector3Int> GetVisualPositions()
    {
        yield return tilePos;
    }

    protected TileBase EffectTileBase => CardSO?.GetEffectTileBase(effectTileIndex);

    protected override TileBase GetVisualTileBase() => EffectTileBase;
    protected override int GetVisualEffectTileIndex() => effectTileIndex;

    protected void ShowTileEffect(CardDataSO dataSO = null)
    {
        CardDataSO visualData = dataSO != null ? dataSO : CardSO;
        if (visualData == null || !visualData.NeedEffectTileBase)
            return;

        BoardManager.Instance?.TileEffectDrawer?.SetTileEffect(
            tilePos,
            visualData,
            effectTileIndex,
            RemainingTurns);
    }

    protected void ClearTileEffect()
    {
        BoardManager.Instance?.TileEffectDrawer?.ClearTileEffect(tilePos);
    }

    protected void RefreshTileEffectTurnAnimation(CardDataSO dataSO = null, int customRemainingTurns = -1)
    {
        CardDataSO visualData = dataSO != null ? dataSO : CardSO;
        if (visualData == null || visualData.EffectTileAnimationMode != TileEffectAnimationMode.Turn)
            return;

        int turns = customRemainingTurns >= 0 ? customRemainingTurns : RemainingTurns;
        BoardManager.Instance?.TileEffectDrawer?.TickTurnAnimation(tilePos, turns);
    }
}

/// <summary>특정 타입의 기물이 행동(이동/잡기)했을 때 반응하는 전역 효과의 기반 추상 클래스</summary>
public abstract class GlobalEffector : Effector
{
    public static event System.Action<GlobalEffector> OnActivated;
    public static event System.Action<GlobalEffector> OnDeactivated;
    public static event System.Action<GlobalEffector> OnTurnTicked;

    protected override void OnEffectApplied() => OnActivated?.Invoke(this);
    protected override void OnEffectReverted() => OnDeactivated?.Invoke(this);

    public override void OnTurnChanged()
    {
        base.OnTurnChanged();
        if (IsApplied && !IsExpired) OnTurnTicked?.Invoke(this);
    }

    protected PieceType watchType;   // 감시할 기물 타입 (Flags 조합 가능, None = 모든 타입)
    protected ApplyType watchColor;  // 감시할 기물 색상

    public void Init(PieceType type, ApplyType color = ApplyType.All, int duration = -1)
    {
        watchType = type;
        watchColor = color;
        SetDuration(duration);
    }

    /// <summary>조건에 맞는 기물이 이동하거나 잡을 때 호출됩니다.</summary>
    /// <param name="piece">행동한 기물</param>
    /// <param name="dest">이동한 목적지</param>
    public virtual void OnPieceAct(Piece piece, Vector3Int dest) { }

    /// <summary>조건에 맞는 기물이 다른 기물을 잡을 때 호출됩니다.</summary>
    /// <param name="piece">행동한 기물</param>
    /// <param name="dest">이동한 목적지</param>
    public virtual void OnPieceCapture(Piece piece, Vector3Int dest) { }

    /// <summary>행동한 기물이 감시 조건(타입, 색상)에 해당하는지 확인합니다.</summary>
    protected bool IsWatching(Piece piece)
    {
        bool typeMatch = watchType == PieceType.None || (watchType & piece.Type) != 0;
        bool colorMatch = watchColor == ApplyType.All
            || (watchColor == ApplyType.White && piece.Color == PieceColor.White)
            || (watchColor == ApplyType.Black && piece.Color == PieceColor.Black);
        return typeMatch && colorMatch;
    }

    public virtual bool CanPieceAct(Piece piece, Vector3Int from, Vector3Int to) { return true; }

    protected override void OnSuspendForArena()
    {
        BoardManager.Instance?.UnregisterGlobalEffector(this);
    }

    protected override void OnResumeFromArena()
    {
        BoardManager.Instance?.RegisterGlobalEffector(this);
    }

    protected override CardDataSO VisualDataSO => CardSO;
}
