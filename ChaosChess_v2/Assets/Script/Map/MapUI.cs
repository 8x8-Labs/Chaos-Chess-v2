using UnityEngine;
using UnityEngine.UI;

public class MapUI : MonoBehaviour
{
    [SerializeField] private Transform mapContainer;
    [SerializeField] private GameObject mapButtonPrefab;

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        var manager = MapManager.Instance;

        foreach (Transform child in mapContainer)
        {
            Destroy(child.gameObject);
        }

        for (int i = manager.totalFloors - 1; i >= 0; i--)
        {
            int id = i;
            Map map = manager.maps[i];

            GameObject obj = Instantiate(mapButtonPrefab, mapContainer);
            UIButton button = obj.GetComponent<UIButton>();
            Image image = obj.GetComponent<Image>();

            if (map.isCleared)
            {
                button.interactable = false;
                image.color = Color.gray;
            }
            else if (id == manager.currentFloor)
            {
                image.color = Color.yellow;
            }
            else
            {
                button.interactable = false;
                image.color = new Color(0.3f, 0.3f, 0.3f);
            }
        }
    }
}