using DG.Tweening;
using UnityEngine;

/// <summary>
/// 충전(Charge) 효과의 루프 VFX 연출 컴포넌트입니다. 효과의 LoopVFXPrefab에 올려두면 effector가 자동으로 호출합니다.
///
/// - 적용 시: 두 스프라이트가 각자의 제자리 위쪽에서 천천히 내려오며 페이드인됩니다.
/// - 소멸 시(효과가 끝나는 순간): 두 스프라이트가 천천히 더 아래로 내려가며 페이드아웃되어 사라집니다.
///   루프 VFX 루트는 Revert 직후 Destroy되므로, 스프라이트를 루트에서 떼어내 퇴장 연출을 끝까지 돌린 뒤 스스로 파괴합니다.
/// </summary>
public class ChargeEffect : MonoBehaviour, IEffectApplyListener, IEffectRevertListener
{
    [SerializeField] private SpriteRenderer sprite1;
    [SerializeField] private SpriteRenderer sprite2;

    [Header("등장 연출 (강하)")]
    [Tooltip("내려오기 시작하는 위쪽 로컬 오프셋(제자리 기준)")]
    [SerializeField] private float appearStartHeight = 1.5f;
    [Tooltip("천천히 내려오는 시간(초)")]
    [SerializeField] private float appearDuration = 0.6f;
    [Tooltip("내려오는 이징")]
    [SerializeField] private Ease appearEase = Ease.OutQuad;
    [Tooltip("두 번째 스프라이트가 첫 번째보다 늦게 내려오기 시작하는 간격(초)")]
    [SerializeField] private float appearInterval = 0.3f;

    [Header("소멸 연출 (강하 + 페이드아웃)")]
    [Tooltip("사라지며 추가로 내려가는 거리(로컬)")]
    [SerializeField] private float exitDropDistance = 1.5f;
    [Tooltip("천천히 내려가며 사라지는 시간(초)")]
    [SerializeField] private float exitDuration = 0.6f;
    [Tooltip("내려가는 이징")]
    [SerializeField] private Ease exitMoveEase = Ease.InQuad;
    [Tooltip("페이드아웃 이징")]
    [SerializeField] private Ease exitFadeEase = Ease.InQuad;

    // 제자리(프리팹에 배치된 원래 로컬 위치)를 기억해 그 위쪽에서 내려오도록 합니다.
    private Vector3 restPos1;
    private Vector3 restPos2;

    private Sequence appearSeq;

    public void OnEffectApply(in EffectVFXContext ctx)
    {
        KillSequence(ref appearSeq);

        appearSeq = DOTween.Sequence();
        // 첫 번째는 0초, 두 번째는 appearInterval만큼 늦게 내려오기 시작합니다.
        SetupAppear(sprite1, ref restPos1, 0f);
        SetupAppear(sprite2, ref restPos2, appearInterval);
    }

    /// <summary>한 스프라이트를 제자리 위쪽·투명 상태로 세팅하고, startTime 시점부터 제자리로 내려오며 페이드인하는 트윈을 시퀀스에 끼워넣습니다.</summary>
    private void SetupAppear(SpriteRenderer sprite, ref Vector3 restPos, float startTime)
    {
        if (sprite == null) return;

        Transform t = sprite.transform;
        restPos = t.localPosition;
        t.localPosition = restPos + new Vector3(0f, appearStartHeight, 0f);
        SetAlpha(sprite, 0f);

        appearSeq.Insert(startTime, t.DOLocalMove(restPos, appearDuration).SetEase(appearEase));
        appearSeq.Insert(startTime, sprite.DOFade(1f, appearDuration).SetEase(Ease.OutQuad));
    }

    public void OnEffectRevert(in EffectVFXContext ctx)
    {
        KillSequence(ref appearSeq);

        // 루프 VFX 루트는 이 호출 직후 Destroy됩니다. 스프라이트를 루트에서 떼어내(월드 좌표 유지)
        // 퇴장 연출을 끝까지 재생한 뒤 스스로 파괴되게 합니다. 루트의 부모에 그대로 붙여 기물 이동을 따라갑니다.
        PlayExit(sprite1);
        PlayExit(sprite2);
    }

    /// <summary>한 스프라이트를 루트에서 떼어내, 천천히 더 아래로 내려가며 페이드아웃시킨 뒤 파괴합니다.</summary>
    private void PlayExit(SpriteRenderer sprite)
    {
        if (sprite == null) return;

        Transform t = sprite.transform;
        SpriteRenderer detached = sprite;
        t.SetParent(transform.parent, worldPositionStays: true);

        Vector3 dropPos = t.localPosition + new Vector3(0f, -exitDropDistance, 0f);

        Sequence exit = DOTween.Sequence();
        exit.Join(t.DOLocalMove(dropPos, exitDuration).SetEase(exitMoveEase));
        exit.Join(detached.DOFade(0f, exitDuration).SetEase(exitFadeEase));
        exit.OnComplete(() =>
        {
            if (detached != null)
                Destroy(detached.gameObject);
        });
    }

    private static void SetAlpha(SpriteRenderer sprite, float a)
    {
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
        // exit 시퀀스는 떼어낸 스프라이트에서 독립적으로 끝까지 재생돼야 하므로 여기서 Kill하지 않습니다.
        KillSequence(ref appearSeq);
    }
}
