using UnityEngine;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    public int totalFloors = 6;
    public int currentFloor = 0;

    public List<Map> maps = new List<Map>();
    public string DefaultFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    public List<string> Boss1FEN = new();
    public List<string> Boss2FEN = new();

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
            string fen = DefaultFEN;
            if (i == 2)
                fen = Boss1FEN[Random.Range(0, Boss1FEN.Count - 1)];
            if (i == 5)
                fen = Boss2FEN[Random.Range(0, Boss2FEN.Count - 1)];
            Map map = new Map
            {
                ELO = startELO + 150 * i,
                floor = i,
                isCleared = false,
                FEN = fen
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
            // todo: ���� ����/���� ȭ������ �̵�/
            return;
        curMap = maps[currentFloor];
    }
}