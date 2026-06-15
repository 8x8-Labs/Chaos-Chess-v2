using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 화면 중앙 상단에 짧은 안내 메시지(토스트)를 띄우는 싱글톤 UI.
/// 매치 중 즉시 피드백이 필요한 상황(카드 사용 불가 등)에 사용합니다.
/// 표시 위치는 RectTransform(상단 중앙 앵커)으로 에디터에서 지정합니다.
/// </summary>
public class IngameToastUI : MonoBehaviour
{
    public static IngameToastUI Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float holdDuration = 1.5f;

    private Sequence sequence;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        sequence?.Kill();
        if (Instance == this) Instance = null;
    }

    /// <summary>메시지를 페이드 인 → 유지 → 페이드 아웃 순으로 표시합니다. 표시 중 재호출 시 즉시 교체됩니다.</summary>
    public void Show(string message)
    {
        if (messageText == null || canvasGroup == null) return;

        messageText.text = $"[{message}]";

        sequence?.Kill();
        canvasGroup.alpha = 0f;

        sequence = DOTween.Sequence()
            .Append(canvasGroup.DOFade(1f, fadeDuration))
            .AppendInterval(holdDuration)
            .Append(canvasGroup.DOFade(0f, fadeDuration));
    }
}
