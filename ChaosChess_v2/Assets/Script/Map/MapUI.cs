using System.Collections.Generic;
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

    [Header("Node Type Visuals")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite eliteSprite;
    [SerializeField] private Sprite bossSprite;
    [SerializeField] private Sprite clearedSprite;
    [SerializeField] private string effectChildName = "Effect";

    private readonly Dictionary<Map, GameObject> _nodeObjects = new();
    private bool _built = false;

    private void Start()
    {
        // VerticalLayoutGroup / ContentSizeFitter는 수동 절대좌표 배치와 충돌하므로 제거
        var vlg = mapContainer.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) Destroy(vlg);
        var csf = mapContainer.GetComponent<ContentSizeFitter>();
        if (csf != null) Destroy(csf);

        Refresh();

        // 스크롤뷰 초기 위치를 맨 아래(0층)로 고정
        Canvas.ForceUpdateCanvases();
        var scrollRect = mapContainer.GetComponentInParent<ScrollRect>();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    // 노드 상태만 업데이트한다. 최초 호출 시 BuildMap()으로 오브젝트를 생성한다.
    public void Refresh()
    {
        if (!_built) BuildMap();

        var manager = MapManager.Instance;
        if (manager == null) return;

        foreach (var kvp in _nodeObjects)
            ApplyState(kvp.Value, kvp.Key);
    }

    // 노드 버튼과 연결선을 최초 1회 생성하고 캐싱한다.
    private void BuildMap()
    {
        var manager = MapManager.Instance;
        if (manager == null) return;

        _nodeObjects.Clear();

        // ── 노드 버튼 생성 ────────────────────────────────────────────────────
        for (int floor = 0; floor < manager.totalFloors; floor++)
        {
            int count = manager.nodesPerFloor[floor];
            for (int col = 0; col < count; col++)
            {
                Map map = manager.mapGrid[floor][col];
                if (map.uiPosition == Vector2.zero)
                    map.uiPosition = NodePosition(floor, col, count);
                Vector2 pos = map.uiPosition;

                GameObject obj = Instantiate(mapButtonPrefab, mapContainer);
                RectTransform rt = obj.GetComponent<RectTransform>();
                // 앵커를 하단 중앙으로 설정하여 절대좌표(anchoredPosition)로 배치
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.anchoredPosition = pos;

                ApplyState(obj, map);

                var nodeBtn = obj.AddComponent<MapNodeButton>();
                nodeBtn.mapData = map;

                _nodeObjects[map] = obj;
            }
        }

        // ── 층 간 연결선 생성 (노드 뒤에 렌더링되도록 SiblingIndex 0에 삽입) ──
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

        _built = true;
    }

    // 층(floor)과 열(col)로부터 화면 좌표를 계산. jitter로 노드 위치를 소폭 흔들어 유기적으로 보이게 함.
    private Vector2 NodePosition(int floor, int col, int count)
    {
        // 같은 층의 노드들을 수평으로 가운데 정렬한 뒤 jitter 추가
        float centerOffset = (count - 1) * nodeSpacing / 2f;
        float x = col * nodeSpacing - centerOffset + Random.Range(-xJitter, xJitter);
        float y = floor * floorHeight + bottomPadding + Random.Range(-yJitter, yJitter);
        return new Vector2(x, y);
    }

    // 노드의 클리어/접근 가능/잠금 상태에 따라 버튼 색상과 활성화 여부를 설정
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

        img.sprite = map.isCleared
            ? (clearedSprite != null ? clearedSprite : normalSprite)
            : map.nodeType switch {
                NodeType.Elite => eliteSprite,
                NodeType.Boss  => bossSprite,
                _              => normalSprite
            };

        var effectObj = obj.transform.Find(effectChildName);
        if (effectObj != null)
        {
            bool showEffect = map.nodeType != NodeType.Normal && !map.isCleared;
            effectObj.gameObject.SetActive(showEffect);

            if (showEffect)
            {
                Color effectColor = map.isAccessible ? Color.white : new Color(0.3f, 0.3f, 0.3f);
                SetEffectColor(effectObj, effectColor);
            }
        }

        if (tmp != null)
            tmp.text = $"F{map.floor + 1}\n{map.nodeType}";
    }

    private void SetEffectColor(Transform effectObj, Color color)
    {
        foreach (var img in effectObj.GetComponentsInChildren<Image>(true))
            img.color = color;

        foreach (var ps in effectObj.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = color;
        }
    }

    // Image를 길쭉한 RectTransform으로 회전시켜 두 점 사이의 연결선을 그린다.
    private void DrawLine(Vector2 from, Vector2 to)
    {
        var lineObj = new GameObject("Line");
        lineObj.transform.SetParent(mapContainer, false);
        // 노드 버튼보다 먼저 렌더링되도록 가장 뒤로 이동
        lineObj.transform.SetSiblingIndex(0);

        var img = lineObj.AddComponent<Image>();
        img.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);

        var rt = lineObj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 dir = to - from;
        float dist = dir.magnitude;
        // x축 기준 각도를 구해 RectTransform을 회전시키면 정확한 방향의 선이 됨
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rt.sizeDelta = new Vector2(dist, lineWidth);
        rt.anchoredPosition = from + dir * 0.5f;
        rt.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }
}
