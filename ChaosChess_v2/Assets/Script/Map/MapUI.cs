using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapUI : MonoBehaviour
{
    [SerializeField] private RectTransform mapContainer;
    [SerializeField] private GameObject mapButtonPrefab;

    [SerializeField] private float floorHeight = 160f;
    [SerializeField] private float nodeSpacing = 180f;
    [SerializeField] private float lineWidth = 6f;
    [SerializeField] private float bottomPadding = 80f;
    [SerializeField] private float topPadding = 80f;
    [SerializeField] private float xJitter = 30f;
    [SerializeField] private float yJitter = 20f;

    private void Start()
    {
        var vlg = mapContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) Destroy(vlg);
        var csf = mapContainer.GetComponent<ContentSizeFitter>();
        if (csf != null) Destroy(csf);

        Refresh();

        Canvas.ForceUpdateCanvases();
        var scrollRect = mapContainer.GetComponentInParent<ScrollRect>();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    public void Refresh()
    {
        foreach (Transform child in mapContainer)
            Destroy(child.gameObject);

        var manager = MapManager.Instance;
        if (manager == null) return;

        for (int floor = 0; floor < manager.totalFloors; floor++)
        {
            int count = manager.nodesPerFloor[floor];
            for (int col = 0; col < count; col++)
            {
                Map map = manager.mapGrid[floor][col];
                Vector2 pos = NodePosition(floor, col, count);
                map.uiPosition = pos;

                GameObject obj = Instantiate(mapButtonPrefab, mapContainer);
                RectTransform rt = obj.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.anchoredPosition = pos;

                ApplyState(obj, map);

                var nodeBtn = obj.AddComponent<MapNodeButton>();
                nodeBtn.mapData = map;
            }
        }

        for (int floor = 0; floor < manager.totalFloors - 1; floor++)
        {
            foreach (var node in manager.mapGrid[floor])
            {
                foreach (int nextCol in node.nextColumns)
                {
                    DrawLine(node.uiPosition, manager.mapGrid[floor + 1][nextCol].uiPosition);
                }
            }
        }

        // 컨텐츠 높이를 실제 노드 범위에 맞게 설정해 ScrollRect 스크롤 범위를 고정
        float contentHeight = (manager.totalFloors - 1) * floorHeight + bottomPadding + topPadding;
        mapContainer.sizeDelta = new Vector2(mapContainer.sizeDelta.x, contentHeight);
    }

    private Vector2 NodePosition(int floor, int col, int count)
    {
        float centerOffset = (count - 1) * nodeSpacing / 2f;
        float x = col * nodeSpacing - centerOffset + Random.Range(-xJitter, xJitter);
        float y = floor * floorHeight + bottomPadding + Random.Range(-yJitter, yJitter);
        return new Vector2(x, y);
    }

    private void ApplyState(GameObject obj, Map map)
    {
        var img = obj.GetComponent<Image>();
        var btn = obj.GetComponent<Button>();
        var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();

        if (map.isCleared)
        {
            img.color = Color.gray;
            btn.interactable = false;
        }
        else if (map.isAccessible)
        {
            img.color = Color.white;
            btn.interactable = true;
        }
        else
        {
            img.color = new Color(0.3f, 0.3f, 0.3f);
            btn.interactable = false;
        }

        if (tmp != null)
            tmp.text = $"F{map.floor + 1}\n{map.nodeType}";
    }

    private void DrawLine(Vector2 from, Vector2 to)
    {
        var lineObj = new GameObject("Line");
        lineObj.transform.SetParent(mapContainer, false);
        lineObj.transform.SetSiblingIndex(0);

        var img = lineObj.AddComponent<Image>();
        img.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);

        var rt = lineObj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 dir = to - from;
        float dist = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rt.sizeDelta = new Vector2(dist, lineWidth);
        rt.anchoredPosition = from + dir * 0.5f;
        rt.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }
}
