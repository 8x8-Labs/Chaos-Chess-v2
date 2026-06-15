using DG.Tweening;
using UnityEngine;

/// <summary>
/// 효과의 LoopVFXPrefab에 올려두면 effector가 적용되는 순간 자동으로 호출되는 1회성 연출 컴포넌트입니다.
///
/// - 적용 시: 대상 스프라이트가 360도 한 바퀴 돈 뒤, 자연스럽게 페이드아웃되며 사라집니다.
/// - 페이드아웃이 끝나면 루프 VFX 루트(이 GameObject)를 스스로 파괴합니다.
///   (effector의 StopLoopVFX가 나중에 다시 Destroy를 호출해도 Unity가 null로 처리하므로 안전합니다.)
/// </summary>
public class SpinFadeOutEffect : MonoBehaviour, IEffectApplyListener
{
    [Header("대상")]
    [Tooltip("회전·페이드시킬 스프라이트. 비워두면 자식에서 자동으로 찾습니다.")]
    [SerializeField] private SpriteRenderer targetSprite;

    [Header("회전 연출")]
    [Tooltip("도는 각도(기본 360 = 한 바퀴). 음수면 반대 방향.")]
    [SerializeField] private float spinAngle = 360f;
    [Tooltip("도는 시간(초)")]
    [SerializeField] private float spinDuration = 0.5f;
    [Tooltip("회전 이징(가속/감속 느낌)")]
    [SerializeField] private Ease spinEase = Ease.InOutQuad;

    [Header("페이드아웃 연출")]
    [Tooltip("사라지는 시간(초)")]
    [SerializeField] private float fadeOutDuration = 0.4f;
    [Tooltip("페이드 이징")]
    [SerializeField] private Ease fadeOutEase = Ease.InQuad;
    [Tooltip("회전과 페이드를 겹치는 시간(초). 0이면 다 돈 뒤에 페이드 시작, 클수록 도는 도중에 미리 흐려집니다.")]
    [SerializeField] private float fadeOverlap = 0.1f;

    private Sequence seq;

    public void OnEffectApply(in EffectVFXContext ctx)
    {
        Play();
    }

    /// <summary>360도 회전 → 페이드아웃 → 자기 파괴 연출을 재생합니다.</summary>
    public void Play()
    {
        if (targetSprite == null)
            targetSprite = GetComponentInChildren<SpriteRenderer>();
        if (targetSprite == null)
        {
            // 회전·페이드시킬 스프라이트가 없으면 도는 의미가 없으므로 바로 정리합니다.
            Destroy(gameObject);
            return;
        }

        seq?.Kill();

        // 기물 자식으로 따라다니지 않고, 생성된 위치(타일 중앙)에 독립적으로 머무르도록
        // 월드 좌표를 유지한 채 부모(기물 transform)에서 떼어냅니다.
        // 이렇게 하면 기물의 거대화 스케일·이동에 영향받지 않고 그 자리에서 회전·페이드됩니다.
        transform.SetParent(null, worldPositionStays: true);

        Transform t = targetSprite.transform;
        // 항상 현재 각도 기준으로 한 바퀴 돌도록 FastBeyond360을 사용합니다.
        Vector3 targetEuler = t.localEulerAngles + new Vector3(0f, 0f, spinAngle);

        seq = DOTween.Sequence();
        // 1) 한 바퀴 회전
        seq.Append(t.DOLocalRotate(targetEuler, spinDuration, RotateMode.FastBeyond360).SetEase(spinEase));
        // 2) 회전 막바지에 살짝 겹쳐서 페이드아웃 시작 (fadeOverlap만큼 당겨줌)
        float fadeInsertTime = Mathf.Max(0f, spinDuration - fadeOverlap);
        seq.Insert(fadeInsertTime, targetSprite.DOFade(0f, fadeOutDuration).SetEase(fadeOutEase));
        seq.OnComplete(() => Destroy(gameObject));
    }

    private void OnDestroy()
    {
        seq?.Kill();
        seq = null;
    }
}
