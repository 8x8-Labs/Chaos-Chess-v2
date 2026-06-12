using DG.Tweening;
using UnityEngine;

/// <summary>
/// 노을빛 검 효과의 루프 VFX 연출 컴포넌트입니다. 효과의 LoopVFXPrefab에 올려두면 effector가 자동으로 호출합니다.
///
/// - 적용 시: 아버지의 원수 연출처럼 검이 위에서 내려오며 등장합니다.
/// - 소멸 시(잡기로 효과가 끝나는 순간): 스프라이트 각도가 확 돌아가며 칼을 휘두른 뒤 페이드아웃으로 사라집니다.
///   노을빛 검은 잡는 순간 Revert되는 1회성 효과이므로 휘두르기·퇴장 연출을 Revert 한 곳에서 처리합니다.
/// </summary>
public class SunsetEffect : MonoBehaviour, IEffectApplyListener, IEffectRevertListener
{
    [SerializeField] private SpriteRenderer sword;

    [Header("등장 연출 (검 강하)")]
    [Tooltip("검이 내려오기 시작하는 로컬 오프셋")]
    [SerializeField] private Vector2 appearStartOffset = new Vector2(0f, 2f);
    [SerializeField] private float startRotation = -150f;
    [Tooltip("내려오는 시간(초)")]
    [SerializeField] private float appearDuration = 0.35f;
    [Tooltip("내려오는 이징")]
    [SerializeField] private Ease appearEase = Ease.OutQuad;

    [Header("휘두르기 연출 (소멸 시)")]
    [Tooltip("휘두르기 직전 들어올리는(와인드업) 각도")]
    [SerializeField] private float swingStartAngle = 75f;
    [Tooltip("휘둘러 도달하는 각도")]
    [SerializeField] private float swingEndAngle = -75f;
    [Tooltip("휘두르는 시간(초) — 짧을수록 날카롭게 보입니다")]
    [SerializeField] private float swingDuration = 0.12f;
    [Tooltip("휘두르는 이징(날카로운 느낌은 OutExpo/OutCubic)")]
    [SerializeField] private Ease swingEase = Ease.OutExpo;

    [Header("소멸 연출 (페이드아웃)")]
    [Tooltip("사라지는 시간(초)")]
    [SerializeField] private float fadeOutDuration = 0.4f;
    [Tooltip("페이드 이징")]
    [SerializeField] private Ease fadeOutEase = Ease.InQuad;

    private Sequence appearSeq;

    public void OnEffectApply(in EffectVFXContext ctx)
    {
        if (sword == null) return;

        KillSequence(ref appearSeq);

        Transform t = sword.transform;
        t.localPosition = appearStartOffset;
        t.localRotation = Quaternion.Euler(0f, 0f, startRotation);
        SetSwordAlpha(0f);

        appearSeq = DOTween.Sequence();
        appearSeq.Join(t.DOLocalMove(Vector3.zero, appearDuration).SetEase(appearEase));
        appearSeq.Join(sword.DOFade(1f, appearDuration).SetEase(Ease.OutQuad));
    }

    public void OnEffectRevert(in EffectVFXContext ctx)
    {
        KillSequence(ref appearSeq);

        if (sword == null) return;

        // 루프 VFX 루트는 이 호출 직후 Destroy됩니다. 검을 루트에서 떼어내되,
        // 잡기 이동 중 기물을 계속 따라가도록 루트의 부모(=기물 transform)에 그대로 붙입니다.
        // (null로 떼면 옛 칸에 고정돼 기물만 새 칸으로 미끄러져 가버립니다.)
        // 또한 이 시퀀스를 필드에 담으면 루트의 OnDestroy가 Kill해버리므로 로컬 변수로 두어
        // 분리된 검에서 휘두르기 → 페이드아웃을 끝까지 돌린 뒤 스스로 파괴합니다.
        Transform t = sword.transform;
        SpriteRenderer detachedSword = sword;
        t.SetParent(transform.parent, worldPositionStays: true);

        // 와인드업 각도로 즉시 세팅한 뒤, 빠르게 휘둘러 슬래시처럼 보이게 합니다.
        t.localRotation = Quaternion.Euler(0f, 0f, swingStartAngle);

        Sequence exit = DOTween.Sequence();
        // 1) 칼을 휘두름
        exit.Append(
            t.DOLocalRotate(new Vector3(0f, 0f, swingEndAngle), swingDuration, RotateMode.Fast)
             .SetEase(swingEase));
        // 2) 휘두른 자세 그대로 페이드아웃
        exit.Append(detachedSword.DOFade(0f, fadeOutDuration).SetEase(fadeOutEase));
        exit.OnComplete(() =>
        {
            if (detachedSword != null)
                Destroy(detachedSword.gameObject);
        });
    }

    private void SetSwordAlpha(float a)
    {
        Color c = sword.color;
        c.a = a;
        sword.color = c;
    }

    private static void KillSequence(ref Sequence seq)
    {
        seq?.Kill();
        seq = null;
    }

    private void OnDestroy()
    {
        // exit 시퀀스는 분리된 검에서 독립적으로 끝까지 재생돼야 하므로 여기서 Kill하지 않습니다.
        KillSequence(ref appearSeq);
    }
}
