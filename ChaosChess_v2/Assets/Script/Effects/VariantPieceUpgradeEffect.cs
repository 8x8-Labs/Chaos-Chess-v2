using UnityEngine;

/// <summary>
/// 엘리트 노드 진입 시 적 기물이 변형 기물로 업그레이드되는 것을 알리는 1회성 연출 컴포넌트입니다.
/// 대상 기물 위치에 프리팹을 스폰하면 자체 파티클이 재생되고, 재생이 끝나면 스스로 파괴됩니다.
/// (변형 기물 종류와 무관하게 공통으로 사용하는 단일 프리팹입니다.)
/// </summary>
public class VariantPieceUpgradeEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem particle;
    [Tooltip("재생 후 파괴까지의 수명(초). 0 이하이면 파티클 길이로 자동 계산합니다.")]
    [SerializeField] private float lifetimeOverride = 0f;

    private void Start()
    {
        if (particle == null) particle = GetComponentInChildren<ParticleSystem>();
        if (particle == null)
        {
            Destroy(gameObject);
            return;
        }

        particle.Play();

        float life = lifetimeOverride > 0f ? lifetimeOverride : GetTotalDuration(particle);
        Destroy(gameObject, life);
    }

    /// <summary>파티클의 재생 시간 + 최대 입자 수명을 더해 전체 연출 길이를 추정합니다.</summary>
    private static float GetTotalDuration(ParticleSystem ps)
    {
        ParticleSystem.MainModule main = ps.main;
        return main.duration + main.startLifetime.constantMax;
    }
}
