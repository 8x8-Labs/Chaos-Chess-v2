using UnityEngine;
using System.Collections.Generic;

public enum PracticeDifficulty
{
    Easy,
    Normal,
    Hard
}

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    public int totalFloors = 6;
    public int currentFloor = 0;

    public List<Map> maps = new List<Map>();
    public string DefaultFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    public List<string> Boss1FEN = new();
    public List<string> Boss2FEN = new();

    [Header("Practice")]
    [SerializeField] private int easyPracticeELO = 900;
    [SerializeField] private int normalPracticeELO = 1200;
    [SerializeField] private int hardPracticeELO = 1500;
    [SerializeField] private string easyPracticeFEN;
    [SerializeField] private string normalPracticeFEN;
    [SerializeField] private string hardPracticeFEN;

    [Header("Graph Map")]
    public int nodesPerFloorMin = 1;
    public int nodesPerFloorMax = 3;

    public List<List<Map>> mapGrid = new();
    public int[] nodesPerFloor;
    public Map selectedNode;
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

    public void Init()
    {
        maps.Clear();
        mapGrid.Clear();
        currentFloor = 0;

        int startELO = Random.Range(800, 1200);

        nodesPerFloor = new int[totalFloors];
        for (int i = 0; i < totalFloors - 1; i++)
            nodesPerFloor[i] = Random.Range(nodesPerFloorMin, nodesPerFloorMax + 1);
        nodesPerFloor[totalFloors - 1] = 1;

        for (int floor = 0; floor < totalFloors; floor++)
        {
            mapGrid.Add(new List<Map>());
            for (int col = 0; col < nodesPerFloor[floor]; col++)
            {
                bool isBoss = floor == totalFloors - 1;
                mapGrid[floor].Add(new Map
                {
                    ELO = startELO + 150 * floor,
                    floor = floor,
                    column = col,
                    isCleared = false,
                    isAccessible = floor == 0,
                    nodeType = isBoss ? NodeType.Boss
                               : (Random.value < 0.3f ? NodeType.Elite : NodeType.Normal),
                    FEN = SelectFEN(floor, isBoss)
                });
            }
        }

        for (int floor = 0; floor < totalFloors - 1; floor++)
        {
            int nextCount = nodesPerFloor[floor + 1];

            for (int col = 0; col < nodesPerFloor[floor]; col++)
            {
                var node = mapGrid[floor][col];
                int connections = Random.Range(1, 3);
                for (int k = 0; k < connections; k++)
                {
                    int target = Random.Range(0, nextCount);
                    if (!node.nextColumns.Contains(target))
                        node.nextColumns.Add(target);
                }
            }

            // 고립 노드 방지: incoming이 없는 노드에 강제 연결 추가
            var hasIncoming = new bool[nextCount];
            foreach (var node in mapGrid[floor])
                foreach (int t in node.nextColumns)
                    hasIncoming[t] = true;

            for (int t = 0; t < nextCount; t++)
            {
                if (!hasIncoming[t])
                {
                    int src = Random.Range(0, nodesPerFloor[floor]);
                    if (!mapGrid[floor][src].nextColumns.Contains(t))
                        mapGrid[floor][src].nextColumns.Add(t);
                }
            }
        }

        foreach (var row in mapGrid)
            maps.AddRange(row);

        curMap = mapGrid[0][0];
    }

    private string SelectFEN(int floor, bool isBoss)
    {
        if (isBoss)
        {
            if (floor == 2 && Boss1FEN.Count > 0) return Boss1FEN[Random.Range(0, Boss1FEN.Count)];
            if (floor == 5 && Boss2FEN.Count > 0) return Boss2FEN[Random.Range(0, Boss2FEN.Count)];
        }
        return DefaultFEN;
    }

    public void StartRun() => Init();

    public void StartPractice(PracticeDifficulty difficulty)
    {
        maps.Clear();
        mapGrid.Clear();
        currentFloor = 0;
        Map practiceMap = CreatePracticeMap(difficulty);
        maps.Add(practiceMap);
        curMap = practiceMap;
    }

    private Map CreatePracticeMap(PracticeDifficulty difficulty)
    {
        Map practiceMap = new Map { floor = 0, isCleared = false };
        ApplyPracticeSetting(difficulty, practiceMap);
        return practiceMap;
    }

    private int GetPracticeElo(PracticeDifficulty difficulty)
    {
        switch (difficulty)
        {
            case PracticeDifficulty.Easy: return easyPracticeELO;
            case PracticeDifficulty.Normal: return normalPracticeELO;
            case PracticeDifficulty.Hard: return hardPracticeELO;
            default: return normalPracticeELO;
        }
    }

    private string GetPracticeFen(PracticeDifficulty difficulty)
    {
        switch (difficulty)
        {
            case PracticeDifficulty.Easy:
                return string.IsNullOrWhiteSpace(easyPracticeFEN) ? DefaultFEN : easyPracticeFEN;
            case PracticeDifficulty.Normal:
                return string.IsNullOrWhiteSpace(normalPracticeFEN) ? DefaultFEN : normalPracticeFEN;
            case PracticeDifficulty.Hard:
                return string.IsNullOrWhiteSpace(hardPracticeFEN) ? DefaultFEN : hardPracticeFEN;
            default: return DefaultFEN;
        }
    }

    private void ApplyPracticeSetting(PracticeDifficulty difficulty, Map map)
    {
        map.ELO = GetPracticeElo(difficulty);
        map.FEN = GetPracticeFen(difficulty);
    }

    public void OnCombatCleared()
    {
        if (selectedNode == null) return;

        selectedNode.isCleared = true;
        currentFloor = selectedNode.floor + 1;

        if (currentFloor < totalFloors)
        {
            foreach (int nextCol in selectedNode.nextColumns)
                mapGrid[currentFloor][nextCol].isAccessible = true;

            curMap = mapGrid[currentFloor][0];
        }
    }
}
