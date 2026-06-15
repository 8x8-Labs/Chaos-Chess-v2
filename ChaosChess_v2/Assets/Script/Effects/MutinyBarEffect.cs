using DG.Tweening;
using UnityEngine;

/// <summary>
/// 하극상(Mutiny) 효과의 루프 VFX 연출 컴포넌트입니다. 효과의 LoopVFXPrefab에 올려두면 effector가 자동으로 호출합니다.
///
/// - 적용 시: 막대 스프라이트가 왼쪽 가장자리를 고정한 채 가로로 0→1 차오르며 등장합니다(왼쪽 → 오른쪽).
/// - 소멸 시(효과가 끝나는 순간): 그 반대로 1→0 줄어들며 사라집니다(오른쪽 → 왼쪽).
///   왼쪽 가장자리를 고정하기 위해 스케일에 맞춰 위치를 보정하므로 스프라이트 피벗이 가운데여도 동작합니다.
///   루프 VFX 루트는 Revert 직후 Destroy되므로, 막대를 루트에서 떼어내 퇴장 연출을 끝까지 돌린 뒤 스스로 파괴합니다.
/// </summary>
public class MutinyBarEffect : MonoBehaviour, IEffectApplyListener, IEffectRevertListener
{
    [Header("대상")]
    [Tooltip("차오를 막대 스프라이트. 비워두면 자식에서 자동으로 찾습니다.")]
    [SerializeField] private SpriteRenderer bar;

    [Header("등장 연출 (차오름)")]
    [Tooltip("왼쪽에서 오른쪽으로 차오르는 시간(초)")]
    [SerializeField] private float fillDuration = 0.5f;
    [Tooltip("차오르는 이징")]
    [SerializeField] private Ease fillEase = Ease.OutQuad;

    [Header("소멸 연출 (줄어듦)")]
    [Tooltip("오른쪽에서 왼쪽으로 줄어드는 시간(초)")]
    [SerializeField] private float drainDuration = 0.5f;
    [Tooltip("줄어드는 이징")]
    [SerializeField] private Ease drainEase = Ease.InQuad;

    // 막대의 원래(=가득 찬) 로컬 위치/스케일과 왼쪽 가장자리 고정 계산용 반너비를 기억합니다.
    private Vector3 restPos;
    private float fullScaleX;
    private float halfWidthFull;

    private Tween fillTween;

    public void OnEffectApply(in EffectVFXContext ctx)
    {
        if (bar == null)
            bar = GetComponentInChildren<SpriteRenderer>();
        if (bar == null) return;

        CaptureMetrics();

        fillTween?.Kill();
        // 0(빈 막대)에서 시작해 1(가득)로 차오릅니다.
        SetFill(0f);
        fillTween = DOTween.To(SetFill, 0f, 1f, fillDuration).SetEase(fillEase);
    }

    public void OnEffectRevert(in EffectVFXContext ctx)
    {
        fillTween?.Kill();
        fillTween = null;

        if (bar == null) return;

        // 루프 VFX 루트는 이 호출 직후 Destroy됩니다. 막대를 루트에서 떼어내(월드 좌표 유지)
        // 줄어드는 연출을 끝까지 재생한 뒤 스스로 파괴되게 합니다. 루트의 부모에 그대로 붙여 기물 이동을 따라갑니다.
        SpriteRenderer detached = bar;
        detached.transform.SetParent(transform.parent, worldPositionStays: true);

        // SetFill은 인스턴스 메서드라 트윈이 this(곧 파괴될 MutinyBarEffect)를 캡처합니다.
        // 파괴된 오브젝트 참조로 인한 MissingReferenceException을 피하려 필요한 값을 지역 변수로 캡처하고,
        // 떼어낸 막대(detached)만 참조하는 람다로 줄어듦 연출을 재생합니다.
        float capturedFullScaleX = fullScaleX;
        float capturedHalfWidthFull = halfWidthFull;
        Vector3 capturedRestPos = restPos;

        // 가득 찬 상태(1)에서 빈 상태(0)로 역재생 → 오른쪽에서 왼쪽으로 줄어듭니다.
        DOTween.To(f =>
        {
            if (detached == null) return;
            Transform t = detached.transform;

            Vector3 s = t.localScale;
            s.x = capturedFullScaleX * f;
            t.localScale = s;

            Vector3 p = t.localPosition;
            p.x = capturedRestPos.x - capturedHalfWidthFull * (1f - f);
            t.localPosition = p;
        }, 1f, 0f, drainDuration)
            .SetEase(drainEase)
            .OnComplete(() =>
            {
                if (detached != null)
                    Destroy(detached.gameObject);
            });
    }

    /// <summary>현재 막대의 가득 찬 기준 위치·스케일·반너비를 기록합니다.</summary>
    private void CaptureMetrics()
    {
        Transform t = bar.transform;
        restPos = t.localPosition;
        fullScaleX = t.localScale.x;
        // sprite.bounds.extents.x는 transform 스케일과 무관한 스프라이트 로컬 반너비입니다.
        float spriteHalf = bar.sprite != null ? bar.sprite.bounds.extents.x : 0.5f;
        halfWidthFull = spriteHalf * fullScaleX;
    }

    /// <summary>채움 비율 f(0~1)에 맞춰 가로 스케일을 조절하고, 왼쪽 가장자리가 고정되도록 위치를 보정합니다.</summary>
    private void SetFill(float f)
    {
        if (bar == null) return;

        Transform t = bar.transform;

        Vector3 s = t.localScale;
        s.x = fullScaleX * f;
        t.localScale = s;

        // 가득 찼을 때(f=1) restPos를 유지하고, 줄어들수록 왼쪽 가장자리를 기준으로 폭이 줄도록 보정합니다.
        Vector3 p = t.localPosition;
        p.x = restPos.x - halfWidthFull * (1f - f);
        t.localPosition = p;
    }

    private void OnDestroy()
    {
        // drain 트윈은 떼어낸 막대에서 독립적으로 끝까지 재생돼야 하므로 여기서 Kill하지 않습니다.
        fillTween?.Kill();
        fillTween = null;
    }
}
