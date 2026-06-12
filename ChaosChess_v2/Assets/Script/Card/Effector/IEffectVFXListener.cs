using UnityEngine;

/// <summary>
/// effector 생애주기 시점에 복제된 VFX 오브젝트(LoopVFXPrefab 인스턴스)의 컴포넌트로 전달되는 컨텍스트입니다.
/// </summary>
public readonly struct EffectVFXContext
{
    /// <summary>이 연출을 띄운 효과입니다. 대상 기물/타일 등 추가 정보 접근에 사용합니다.</summary>
    public readonly Effector Effector;

    /// <summary>호출 시점의 앵커 월드 좌표입니다.</summary>
    public readonly Vector3 WorldPos;

    /// <summary>남은 지속 턴 수입니다. (-1 = 영구)</summary>
    public readonly int RemainingTurns;

    public EffectVFXContext(Effector effector, Vector3 worldPos, int remainingTurns)
    {
        Effector = effector;
        WorldPos = worldPos;
        RemainingTurns = remainingTurns;
    }
}

/// <summary>효과가 적용되는 순간(루프 VFX 스폰 직후) 호출됩니다.</summary>
public interface IEffectApplyListener
{
    void OnEffectApply(in EffectVFXContext ctx);
}

/// <summary>이동/잡기/타일 진입 등 게임 훅이 발동할 때 호출됩니다.</summary>
public interface IEffectHookListener
{
    void OnEffectHook(in EffectVFXContext ctx);
}

/// <summary>지속 효과가 한 턴 경과할 때(만료 직전 제외) 호출됩니다.</summary>
public interface IEffectTickListener
{
    void OnEffectTurnTick(in EffectVFXContext ctx);
}

/// <summary>효과가 만료/해제되어 루프 VFX가 정리되기 직전 호출됩니다.</summary>
public interface IEffectRevertListener
{
    void OnEffectRevert(in EffectVFXContext ctx);
}
