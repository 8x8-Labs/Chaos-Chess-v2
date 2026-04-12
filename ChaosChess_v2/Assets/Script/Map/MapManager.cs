using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class MapManager : MonoBehaviour
{
    public static MapManager Instance;
    public int totalFloors = 6;
    public Transform mapContainer;
    public GameObject mapButtonPrefab;
    public int currentFloor = 0;
    public List<Map> maps = new List<Map>();
    public List<string> bossFENs = new List<string>();
    public string DefaultFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    public Map curMap;

    private void Awake()
    {
        if (Instance == null)
        {
            DontDestroyOnLoad(gameObject);
            Init();
        }
        else Destroy(gameObject);
    }

    private void Init()
    {
        Instance = this;

        maps.Clear();
        bossFENs.Clear();

        currentFloor = 0;

        int startELO = Random.Range(800, 1200);
        for (int i = totalFloors - 1; i >= 0; i--)
        {
            Map map = new Map();
            map.ELO = startELO + 150 * i;
            map.floor = i;
            map.isCleared = false;
            map.FEN = DefaultFEN;
            maps.Add(map);
        }

        RefreshMapUI();
    }

    public void OnMapClicked(int mapId)
    {
        if (mapId != currentFloor)
            return;
        curMap = maps[mapId];
        EnterMap();
    }

    private void EnterMap()
    {
        SceneManager.LoadScene("MainGameScene");
    }

    public void OnCombatCleared()
    {
        maps[currentFloor].isCleared = true;
        int next = currentFloor + 1;
        if (next == totalFloors)
        {
        }
        RefreshMapUI();
    }

    private void RefreshMapUI()
    {
        foreach (Transform child in mapContainer)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < totalFloors; i++)
        {
            Map map = maps[i];

            GameObject buttonObj = Instantiate(mapButtonPrefab, mapContainer);
            Button button = buttonObj.GetComponent<UIButton>();

            if (map.isCleared)
            {
                button.interactable = false;
                buttonObj.GetComponent<Image>().color = Color.gray;
            }
            else if (map.floor == currentFloor)
            {
                buttonObj.GetComponent<Image>().color = Color.yellow;
            }
            else
            {
                button.interactable = false;
                buttonObj.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);
            }

            int capturedId = totalFloors - i - 1;
            button.onClick.AddListener(() => OnMapClicked(capturedId));
        }
    }
}