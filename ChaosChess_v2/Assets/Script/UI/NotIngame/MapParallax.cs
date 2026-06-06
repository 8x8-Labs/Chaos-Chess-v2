using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MapParallax : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform[] bgLayers;
    [SerializeField] private float[] parallaxRates;

    private Vector2[] _originPositions;
    private float _scrollRange;

    private void Start()
    {
        _originPositions = new Vector2[bgLayers.Length];
        for (int i = 0; i < bgLayers.Length; i++)
            _originPositions[i] = bgLayers[i].anchoredPosition;

        scrollRect?.onValueChanged.AddListener(OnScroll);
        StartCoroutine(InitScrollRange());
    }

    // MapUI가 content sizeDelta를 설정한 뒤 레이아웃이 확정되도록 한 프레임 대기
    private IEnumerator InitScrollRange()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if(scrollRect == null) yield break;

        float contentHeight = scrollRect.content.rect.height;
        float viewportHeight = scrollRect.viewport != null
            ? scrollRect.viewport.rect.height
            : ((RectTransform)scrollRect.transform).rect.height;
        _scrollRange = Mathf.Max(0f, contentHeight - viewportHeight);
        OnScroll(scrollRect.normalizedPosition);
    }

    private void OnDestroy() => scrollRect?.onValueChanged.RemoveListener(OnScroll);

    private void OnScroll(Vector2 normalizedPos)
    {
        if (_scrollRange <= 0f) return;

        float offset = (0.5f - normalizedPos.y) * _scrollRange;

        for (int i = 0; i < bgLayers.Length; i++)
        {
            float rate = i < parallaxRates.Length ? parallaxRates[i] : 0.3f;
            bgLayers[i].anchoredPosition = _originPositions[i] + new Vector2(0f, offset * rate);
        }
    }
}
