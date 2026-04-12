using UnityEngine;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    public int totalFloors = 6;
    public int currentFloor = 0;

    public List<Map> maps = new List<Map>();
    public string DefaultFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    public Map curMap;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Init()
    {
        maps.Clear();
        currentFloor = 0;

        int startELO = Random.Range(800, 1200);

        for (int i = 0; i < totalFloors; i++)
        {
            Map map = new Map
            {
                ELO = startELO + 150 * i,
                floor = i,
                isCleared = false,
                FEN = DefaultFEN
            };

            maps.Add(map);
        }
        curMap = maps[currentFloor];
    }

    public void OnCombatCleared()
    {
        maps[currentFloor].isCleared = true;

        currentFloor++;

        if (currentFloor >= totalFloors)
            // todo: 게임 종료/메인 화면으로 이동/
            return;
        curMap = maps[currentFloor];
    }
}