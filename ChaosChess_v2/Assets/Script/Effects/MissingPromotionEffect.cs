using DG.Tweening;
using UnityEngine;

/// <summary>
/// 진급누락 효과의 1회성 연출 컴포넌트입니다. 대상 위치에 프리팹을 스폰하면 스스로 재생되고 끝나면 자기 파괴합니다.
/// (진급누락 카드는 effector를 만들지 않으므로 VFX 리스너가 아닌 독립 연출로 동작합니다.)
///
/// 스폰 시: 이 오브젝트가 위에서 아래로 아주 살짝 내려왔다가, 잠시 뒤 페이드아웃되며 사라집니다.
/// </summary>
public class MissingPromotionEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sprite;

    [Header("등장 연출 (살짝 강하)")]
    [Tooltip("스폰 위치 기준 내려오기 시작하는 오프셋 (살짝만 위)")]
    [SerializeField] private Vector2 appearStartOffset = new Vector2(0f, 0.3f);
    [Tooltip("내려오는 시간(초)")]
    [SerializeField] private float appearDuration = 0.25f;
    [Tooltip("내려오는 이징")]
    [SerializeField] private Ease appearEase = Ease.OutQuad;

    [Header("정지 후 페이드아웃")]
    [Tooltip("내려온 뒤 머무는 시간(초)")]
    [SerializeField] private float holdDuration = 0.4f;
    [Tooltip("사라지는 시간(초)")]
    [SerializeField] private float fadeOutDuration = 0.35f;
    [Tooltip("페이드 이징")]
    [SerializeField] private Ease fadeOutEase = Ease.InQuad;

    private Sequence seq;

    private void Start()
    {
        Play();
    }

    public void Play()
    {
        if (sprite == null) return;

        seq?.Kill();

        // 스프라이트가 아니라 이 컴포넌트를 가진 오브젝트 자체를 내립니다.
        // 스폰된 위치(현재 위치)를 도착점(rest)으로 잡고, 그 위에서 시작해 내려옵니다.
        Transform t = transform;
        Vector3 restPos = t.position;
        t.position = restPos + (Vector3)appearStartOffset;
        SetAlpha(1f);

        seq = DOTween.Sequence();
        // 1) 위에서 아래로 아주 살짝 내려옴
        seq.Append(t.DOMove(restPos, appearDuration).SetEase(appearEase));
        // 2) 잠시 머무름
        seq.AppendInterval(holdDuration);
        // 3) 페이드아웃되며 사라짐
        seq.Append(sprite.DOFade(0f, fadeOutDuration).SetEase(fadeOutEase));
        seq.OnComplete(() => Destroy(gameObject));
    }

    private void SetAlpha(float a)
    {
        Color c = sprite.color;
        c.a = a;
        sprite.color = c;
    }

    private void OnDestroy()
    {
        seq?.Kill();
        seq = null;
    }
}
