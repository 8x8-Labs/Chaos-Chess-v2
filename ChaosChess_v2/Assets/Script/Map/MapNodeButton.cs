using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MapNodeButton : MonoBehaviour
{
    public Map mapData;

    [SerializeField] private float bobHeight = 20f;
    [SerializeField] private float bobDuration = 1.2f;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnSelected);

        var rt = GetComponent<RectTransform>();
        // 각 버튼마다 위상을 다르게 주어 일제히 움직이는 느낌을 방지
        float delay = Random.Range(0f, bobDuration);
        rt.DOAnchorPosY(rt.anchoredPosition.y + bobHeight, bobDuration)
          .SetEase(Ease.InOutSine)
          .SetLoops(-1, LoopType.Yoyo)
          .SetDelay(delay);
    }

    private void OnDestroy()
    {
        GetComponent<RectTransform>()?.DOKill();
    }

    private void OnSelected()
    {
        if (mapData == null) return;
        MapManager.Instance.curMap = mapData;
    }
}
