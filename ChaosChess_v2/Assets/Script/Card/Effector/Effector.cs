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
        PlayApplyVFX();
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

        StopLoopVFX();
        if (shouldNotifyReverted)
            PlayRevertVFX();

        OnRevert();
        if (shouldNotifyReverted)
            OnAnyEffectReverted?.Invoke(this);

        OnEffectReverted();
        activeCardToken?.Complete();
        activeCardToken = null;
    }

    protected virtual void OnDestroy()
    {
        StopLoopVFX();
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
        {
            Revert();
        }
        else
        {
            OnAnyEffectTurnTicked?.Invoke(this);
            NotifyTurnTickListeners();
        }
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

    // ───────── VFX 연출 (CardSO.VFX 기반 자동 재생) ─────────

    private GameObject loopVFXInstance;

    // 루프 VFX 인스턴스에서 캐싱한 생애주기 리스너들 (훅별로 분리)
    private IEffectApplyListener[] applyListeners;
    private IEffectHookListener[] hookListeners;
    private IEffectTickListener[] tickListeners;
    private IEffectRevertListener[] revertListeners;

    private CardVFXConfig vfxConfigOverride;

    /// <summary>이 effector가 사용할 VFX 설정입니다. 기본은 CardSO.VFX이며, SetVFXConfig로 교체할 수 있습니다.</summary>
    protected CardVFXConfig VFXConfig => vfxConfigOverride ?? CardSO?.VFX;

    /// <summary>CardSO.VFX 대신 사용할 VFX 설정을 지정합니다. (예: 타일이 기물에 효과를 부여할 때의 연출)</summary>
    public void SetVFXConfig(CardVFXConfig config) => vfxConfigOverride = config;

    /// <summary>VFX 연출 기준 월드 좌표입니다. 서브클래스가 제공하지 않으면 위치 기반 연출(적용/유지/소멸)은 생략됩니다.</summary>
    protected virtual bool TryGetVFXWorldPosition(out Vector3 pos) { pos = default; return false; }

    /// <summary>루프 VFX를 부착하고 펀치를 적용할 대상입니다. 부착 대상이 파괴되면 루프도 함께 정리됩니다.</summary>
    protected virtual Transform VFXFollowTarget => null;

    /// <summary>효과음을 재생합니다. 클립이 없거나 SoundManager가 없으면 조용히 무시합니다.</summary>
    private void PlaySFX(AudioClip clip, float volume)
    {
        if (clip == null || SoundManager.Instance == null) return;
        SoundManager.Instance.SFXPlay(CardSO != null ? CardSO.CardName : "CardEffect", clip, volume);
    }

    /// <summary>효과 적용 시 1회 버스트 + 지속 루프 + 기본 펀치 트윈을 재생합니다.</summary>
    private void PlayApplyVFX()
    {
        CardVFXConfig vfx = VFXConfig;
        if (vfx == null) return;

        PlaySFX(vfx.ApplySFX, vfx.SFXVolume);

        if (!TryGetVFXWorldPosition(out Vector3 pos)) return;

        Transform follow = VFXFollowTarget;
        VFXSpawner.SpawnOneShot(vfx.ApplyVFXPrefab, pos, follow);

        if (vfx.LoopVFXPrefab != null)
        {
            loopVFXInstance = VFXSpawner.SpawnLoop(vfx.LoopVFXPrefab, pos, follow);
            CacheVFXListeners(loopVFXInstance);

            if (applyListeners != null)
            {
                EffectVFXContext ctx = MakeContext(pos);
                foreach (IEffectApplyListener listener in applyListeners)
                    listener?.OnEffectApply(in ctx);
            }
        }

        if (vfx.PlayApplyAnim)
            VFXSpawner.PlayPunch(follow, vfx.AnimStrength, vfx.AnimDuration);
    }

    /// <summary>효과 소멸 시 1회 버스트를 재생합니다. 호스트가 곧 파괴돼도 살아남도록 부모 없이 스폰합니다.</summary>
    private void PlayRevertVFX()
    {
        CardVFXConfig vfx = VFXConfig;
        if (vfx == null) return;

        PlaySFX(vfx.RevertSFX, vfx.SFXVolume);

        if (vfx.RevertVFXPrefab == null) return;
        if (!TryGetVFXWorldPosition(out Vector3 pos)) return;

        VFXSpawner.SpawnOneShot(vfx.RevertVFXPrefab, pos, null);
    }

    /// <summary>지속 루프 VFX를 정리합니다.</summary>
    private void StopLoopVFX()
    {
        if (loopVFXInstance == null) return;

        // Revert()와 OnDestroy()가 모두 거치는 단일 지점이므로 여기서만 소멸을 통지합니다.
        if (revertListeners != null)
        {
            EffectVFXContext ctx = MakeContext(loopVFXInstance.transform.position);
            foreach (IEffectRevertListener listener in revertListeners)
                listener?.OnEffectRevert(in ctx);
        }
        ClearVFXListeners();

        Destroy(loopVFXInstance);
        loopVFXInstance = null;
    }

    /// <summary>앵커 위치에 게임 훅 VFX를 재생합니다. 서브클래스 훅(이동/잡기/진입 등)에서 호출하세요.</summary>
    protected void PlayHookVFX()
    {
        if (!TryGetVFXWorldPosition(out Vector3 pos)) return;
        PlayHookVFX(pos, VFXFollowTarget);
    }

    /// <summary>지정 월드 좌표에 게임 훅 VFX를 재생합니다. 버스트는 부모 없이 스폰되어 대상 파괴(기물 잡힘 등)와 무관하게 유지됩니다.</summary>
    protected void PlayHookVFX(Vector3 worldPos, Transform punchTarget = null)
    {
        CardVFXConfig vfx = VFXConfig;
        if (vfx == null) return;

        PlaySFX(vfx.HookSFX, vfx.SFXVolume);

        VFXSpawner.SpawnOneShot(vfx.HookVFXPrefab, worldPos, null);
        if (vfx.PlayHookAnim)
            VFXSpawner.PlayPunch(punchTarget, vfx.AnimStrength, vfx.AnimDuration);

        if (hookListeners != null)
        {
            EffectVFXContext ctx = MakeContext(worldPos);
            foreach (IEffectHookListener listener in hookListeners)
                listener?.OnEffectHook(in ctx);
        }
    }

    // ───────── VFX 리스너 (복제본 컴포넌트 호출) ─────────

    /// <summary>루프 VFX 인스턴스에서 훅별 리스너를 캐싱합니다.</summary>
    private void CacheVFXListeners(GameObject instance)
    {
        applyListeners = instance.GetComponentsInChildren<IEffectApplyListener>(true);
        hookListeners = instance.GetComponentsInChildren<IEffectHookListener>(true);
        tickListeners = instance.GetComponentsInChildren<IEffectTickListener>(true);
        revertListeners = instance.GetComponentsInChildren<IEffectRevertListener>(true);
    }

    private void ClearVFXListeners()
    {
        applyListeners = null;
        hookListeners = null;
        tickListeners = null;
        revertListeners = null;
    }

    private EffectVFXContext MakeContext(Vector3 worldPos) => new(this, worldPos, remainingTurns);

    /// <summary>턴 경과를 리스너에 전달합니다.</summary>
    private void NotifyTurnTickListeners()
    {
        if (tickListeners == null) return;

        Vector3 pos = TryGetVFXWorldPosition(out Vector3 p)
            ? p
            : (loopVFXInstance != null ? loopVFXInstance.transform.position : Vector3.zero);

        EffectVFXContext ctx = MakeContext(pos);
        foreach (IEffectTickListener listener in tickListeners)
            listener?.OnEffectTurnTick(in ctx);
    }
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

    protected override bool TryGetVFXWorldPosition(out Vector3 pos)
    {
        if (target == null) { pos = default; return false; }
        pos = target.transform.position;
        return true;
    }

    protected override Transform VFXFollowTarget => target != null ? target.transform : null;

    public virtual void OnPieceCaptured()
    {
        // 잡혀서 곧 파괴될 기물이므로 펀치 트윈 없이 파티클 버스트만 재생합니다.
        if (target != null)
            PlayHookVFX(target.transform.position, null);
    }
    public virtual void OnPieceCapture() { PlayHookVFX(); }
    public virtual void OnPieceMove(Vector3Int dest) { PlayHookVFX(); }
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

    public virtual void OnPieceEnter(Piece piece) { PlayHookVFX(); }
    public virtual void OnPieceExit(Piece piece) { PlayHookVFX(); }
    public virtual bool CanPieceEnter(Piece piece, Vector3Int from, Vector3Int to) { return true; }

    protected override bool TryGetVFXWorldPosition(out Vector3 pos)
    {
        if (BoardManager.Instance == null) { pos = default; return false; }
        pos = BoardManager.Instance.GridPosToWorldPos(tilePos);
        return true;
    }

    protected override Transform VFXFollowTarget => transform;

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
            RemainingTurns,
            playAppear: true);
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

    // 전역 효과는 단일 대상이 없으므로 적용/소멸 VFX의 앵커로 보드 중앙을 사용합니다.
    // (훅 VFX는 OnPieceAct에서 행동한 기물 위치를 직접 넘기므로 영향받지 않습니다.)
    protected override bool TryGetVFXWorldPosition(out Vector3 pos)
    {
        if (BoardManager.Instance == null) { pos = default; return false; }
        pos = (BoardManager.Instance.GridPosToWorldPos(new Vector3Int(3, 3, 0))
             + BoardManager.Instance.GridPosToWorldPos(new Vector3Int(4, 4, 0))) * 0.5f;
        return true;
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
    public virtual void OnPieceAct(Piece piece, Vector3Int dest)
    {
        if (piece != null)
            PlayHookVFX(piece.transform.position, piece.transform);
    }

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
