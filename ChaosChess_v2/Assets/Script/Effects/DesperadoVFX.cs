using DG.Tweening;
using UnityEngine;

/// <summary>
/// 데스페라도(Desperado) 효과의 루프 VFX 연출 컴포넌트입니다. 효과의 LoopVFXPrefab에 올려두면 effector가 자동으로 호출합니다.
///
/// - 적용 시: 하나의 SpriteRenderer에 "돌아가는 총" 스프라이트를 띄워 빠르게 회전시키다가, 점점 감속해 지정한 최종 각도에서 멈춥니다.
///   회전이 멈추는 순간 같은 렌더러의 스프라이트를 정지한 "총"으로 교체해 최종 각도로 고정합니다(리볼버 실린더가 돌다 멈추는 듯한 연출).
/// - 소멸 시(효과가 끝나는 순간): 총 스프라이트가 페이드아웃되어 사라집니다.
///   루프 VFX 루트는 Revert 직후 Destroy되므로, 렌더러를 루트에서 떼어내 퇴장 연출을 끝까지 돌린 뒤 스스로 파괴합니다.
/// </summary>
public class DesperadoVFX : MonoBehaviour, IEffectApplyListener, IEffectRevertListener
{
    [Header("렌더러 / 스프라이트")]
    [Tooltip("스프라이트를 표시·회전시킬 대상 SpriteRenderer")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [Tooltip("회전 중에 보이는 '돌아가는 총' 스프라이트")]
    [SerializeField] private Sprite spinningSprite;
    [Tooltip("회전이 멈춘 뒤 보이는 정지한 '총' 스프라이트")]
    [SerializeField] private Sprite gunSprite;

    [Header("회전 연출")]
    [Tooltip("멈추기 전까지 도는 시간(초)")]
    [SerializeField] private float spinDuration = 1.0f;
    [Tooltip("멈추기 전까지 도는 총 바퀴 수")]
    [SerializeField] private float spinRevolutions = 4f;
    [Tooltip("최종적으로 멈출 Z 각도(도)")]
    [SerializeField] private float finalAngle = 0f;
    [Tooltip("회전 방향 (true = 시계 반대 방향)")]
    [SerializeField] private bool counterClockwise = false;
    [Tooltip("감속 이징 (빠르게 시작해 천천히 멈춤)")]
    [SerializeField] private Ease spinEase = Ease.OutCubic;
    [Tooltip("돌아가는 총 → 정지 총으로 교체되는 시점 (회전 시간 대비 비율, 0~1). 1보다 작으면 회전이 거의 멈춰갈 때 미리 교체됩니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float swapAtNormalizedTime = 0.85f;

    [Header("소멸 연출 (페이드아웃)")]
    [Tooltip("사라지는 시간(초)")]
    [SerializeField] private float exitDuration = 0.4f;
    [Tooltip("페이드아웃 이징")]
    [SerializeField] private Ease exitFadeEase = Ease.InQuad;

    private Sequence spinSeq;

    public void OnEffectApply(in EffectVFXContext ctx)
    {
        KillSequence(ref spinSeq);

        if (targetRenderer == null) return;

        // 시작 상태: 돌아가는 총 스프라이트로 교체하고, 각도를 0도로 초기화.
        targetRenderer.sprite = spinningSprite;
        SetAlpha(targetRenderer, 1f);

        Transform t = targetRenderer.transform;
        t.localRotation = Quaternion.identity;

        // 0도에서 (바퀴 수 × 360 + 최종 각도)까지 한 번에 돌리면, OutCubic 이징으로 빠르게 시작해 최종 각도에서 부드럽게 멈춥니다.
        float dir = counterClockwise ? 1f : -1f;
        float targetZ = dir * (360f * spinRevolutions) + finalAngle;

        spinSeq = DOTween.Sequence();
        spinSeq.Append(t.DOLocalRotate(new Vector3(0f, 0f, targetZ), spinDuration, RotateMode.FastBeyond360)
            .SetEase(spinEase));

        // 회전이 거의 멈춰갈 무렵(감속 구간) 같은 렌더러의 스프라이트를 정지 총으로 미리 교체해 더 자연스럽게 멈추도록 합니다.
        spinSeq.InsertCallback(spinDuration * swapAtNormalizedTime,
            () => { if (targetRenderer != null) targetRenderer.sprite = gunSprite; });
    }

    public void OnEffectRevert(in EffectVFXContext ctx)
    {
        KillSequence(ref spinSeq);
    }

    private static void SetAlpha(SpriteRenderer sprite, float a)
    {
        if (sprite == null) return;
        Color c = sprite.color;
        c.a = a;
        sprite.color = c;
    }

    private static void KillSequence(ref Sequence seq)
    {
        seq?.Kill();
        seq = null;
    }

    private void OnDestroy()
    {
        // exit 트윈은 떼어낸 렌더러에서 독립적으로 끝까지 재생돼야 하므로 여기서 Kill하지 않습니다.
        KillSequence(ref spinSeq);
    }
}
