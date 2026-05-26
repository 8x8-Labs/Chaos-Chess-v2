using UnityEngine;
using UnityEngine.UI;

public class MapNodeButton : MonoBehaviour
{
    public Map mapData;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnSelected);
    }

    private void OnSelected()
    {
        if (mapData == null) return;
        MapManager.Instance.selectedNode = mapData;
        MapManager.Instance.curMap = mapData;
    }
}
