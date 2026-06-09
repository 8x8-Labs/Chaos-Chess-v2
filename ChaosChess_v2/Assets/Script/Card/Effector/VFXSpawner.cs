using UnityEngine;
using DG.Tweening;

/// <summary>
/// 카드 효과용 파티클/트윈 연출을 스폰하고 정리하는 경량 정적 헬퍼입니다.
/// 풀링은 적용하지 않으며(추후 확장 여지), 모든 메서드는 prefab/대상이 null이면 조용히 무시합니다.
/// </summary>
public static class VFXSpawner
{
    /// <summary>파티클 시스템이 없을 때 원샷 인스턴스를 파괴하기까지의 기본 수명(초)입니다.</summary>
    private const float DefaultOneShotLifetime = 2f;

    private const float DefaultPunchDuration = 0.3f;
    private const int PunchVibrato = 6;
    private const float PunchElasticity = 0.5f;

    /// <summary>1회성 연출 프리팹을 스폰하고, 파티클 수명이 끝나면 자동으로 파괴합니다.</summary>
    /// <param name="prefab">스폰할 프리팹 (null이면 무시)</param>
    /// <param name="worldPos">스폰 위치(월드)</param>
    /// <param name="parent">부모로 붙일 Transform (null이면 부모 없음)</param>
    /// <returns>생성된 인스턴스 (prefab이 null이면 null)</returns>
    public static GameObject SpawnOneShot(GameObject prefab, Vector3 worldPos, Transform parent = null)
    {
        if (prefab == null) return null;

        GameObject instance = Object.Instantiate(prefab, worldPos, Quaternion.identity, parent);
        float lifetime = CalculateLifetime(instance);
        Object.Destroy(instance, lifetime);
        return instance;
    }

    /// <summary>지속(루프) 연출 프리팹을 스폰합니다. 호출측이 반환된 인스턴스를 보관하고 직접 파괴해야 합니다.</summary>
    /// <param name="prefab">스폰할 프리팹 (null이면 무시)</param>
    /// <param name="worldPos">스폰 위치(월드)</param>
    /// <param name="parent">부모로 붙일 Transform. 부모가 파괴되면 함께 정리됩니다.</param>
    /// <returns>생성된 인스턴스 (prefab이 null이면 null)</returns>
    public static GameObject SpawnLoop(GameObject prefab, Vector3 worldPos, Transform parent = null)
    {
        if (prefab == null) return null;
        return Object.Instantiate(prefab, worldPos, Quaternion.identity, parent);
    }

    /// <summary>대상 Transform에 공통 펀치 스케일 트윈을 재생합니다. 위치(DOMove)와 독립적입니다.</summary>
    /// <param name="target">펀치할 Transform (null이거나 strength가 0 이하면 무시)</param>
    /// <param name="strength">펀치 세기(스케일 변화량)</param>
    /// <param name="duration">트윈 진행 시간(초). 0 이하면 기본값 사용</param>
    public static void PlayPunch(Transform target, float strength, float duration = DefaultPunchDuration)
    {
        if (target == null || strength <= 0f) return;
        float dur = duration > 0f ? duration : DefaultPunchDuration;
        target.DOPunchScale(Vector3.one * strength, dur, PunchVibrato, PunchElasticity);
    }

    /// <summary>인스턴스의 파티클 시스템들로부터 자동 파괴까지의 수명을 계산합니다.</summary>
    private static float CalculateLifetime(GameObject instance)
    {
        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems == null || systems.Length == 0)
            return DefaultOneShotLifetime;

        float maxLifetime = 0f;
        foreach (ParticleSystem ps in systems)
        {
            ParticleSystem.MainModule main = ps.main;
            float duration = main.duration + main.startLifetime.constantMax;
            if (duration > maxLifetime)
                maxLifetime = duration;
        }

        return maxLifetime > 0f ? maxLifetime : DefaultOneShotLifetime;
    }
}
