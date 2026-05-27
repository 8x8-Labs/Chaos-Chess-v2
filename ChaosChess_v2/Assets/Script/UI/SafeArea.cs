using UnityEngine;

/// <summary>
/// 노치/홈 인디케이터 등 디바이스 Safe Area에 맞게 RectTransform을 조정한다.
/// Canvas 바로 아래 자식 패널에 붙여두면 하위 모든 UI가 안전 영역 안에 위치한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private Canvas canvas;

    private Rect lastSafeArea;
    private Vector2 lastScreenSize;
    private ScreenOrientation lastOrientation;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases += OnWillRenderCanvases;
        Apply();
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= OnWillRenderCanvases;
    }

    private void OnWillRenderCanvases()
    {
        Vector2 currentSize = new Vector2(Screen.width, Screen.height);
        if (Screen.safeArea != lastSafeArea ||
            currentSize != lastScreenSize ||
            Screen.orientation != lastOrientation)
        {
            Apply();
        }
    }

    private void Apply()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;
        lastScreenSize = new Vector2(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;

        if (canvas == null || Screen.width == 0 || Screen.height == 0)
            return;

        // Safe Area를 0~1 정규화된 앵커 좌표로 변환
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
