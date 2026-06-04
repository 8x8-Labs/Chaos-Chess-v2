using UnityEngine;
using System.Collections.Generic;

public enum PracticeDifficulty
{
    Easy,
    Normal,
    Hard
}

[System.Serializable]
public class MapFloor
{
    public List<Map> nodes = new();
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

    public List<MapFloor> mapGrid = new();
    public int[] nodesPerFloor;
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

    // 맵 그래프를 처음부터 새로 생성한다. 런 시작 또는 재시작 시 호출.
    public void Init()
    {
        maps.Clear();
        mapGrid.Clear();
        currentFloor = 0;

        // 런마다 시작 ELO를 랜덤화하여 난이도 변동 부여
        int startELO = Random.Range(800, 1200);

        // ── 1단계: 각 층의 노드 수 결정 ──────────────────────────────────────
        nodesPerFloor = new int[totalFloors];
        for (int i = 0; i < totalFloors - 1; i++)
            nodesPerFloor[i] = Random.Range(nodesPerFloorMin, nodesPerFloorMax + 1);
        // 마지막 층은 보스 노드 하나로 고정
        nodesPerFloor[totalFloors - 1] = 1;

        // ── 2단계: 노드 생성 ──────────────────────────────────────────────────
        for (int floor = 0; floor < totalFloors; floor++)
        {
            mapGrid.Add(new MapFloor());
            for (int col = 0; col < nodesPerFloor[floor]; col++)
            {
                bool isBoss = floor == totalFloors - 1;
                mapGrid[floor].nodes.Add(new Map
                {
                    // 층이 높을수록 ELO 150씩 상승 → AI 강도 선형 증가
                    ELO = startELO + 150 * floor,
                    floor = floor,
                    column = col,
                    isCleared = false,
                    // 0층 노드만 초기에 접근 가능
                    isAccessible = floor == 0,
                    // 보스층이면 Boss, 30% 확률로 Elite, 나머지는 Normal
                    nodeType = isBoss ? NodeType.Boss
                               : (Random.value < 0.3f ? NodeType.Elite : NodeType.Normal),
                    FEN = SelectFEN(floor, isBoss)
                });
            }
        }

        // ── 3단계: 층 간 엣지(연결) 생성 ────────────────────────────────────
        for (int floor = 0; floor < totalFloors - 1; floor++)
        {
            int nextCount = nodesPerFloor[floor + 1];

            // 각 노드에서 다음 층 노드로 1~2개 랜덤 연결
            for (int col = 0; col < nodesPerFloor[floor]; col++)
            {
                var node = mapGrid[floor].nodes[col];
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
            foreach (var node in mapGrid[floor].nodes)
                foreach (int t in node.nextColumns)
                    hasIncoming[t] = true;

            for (int t = 0; t < nextCount; t++)
            {
                if (!hasIncoming[t])
                {
                    int src = Random.Range(0, nodesPerFloor[floor]);
                    if (!mapGrid[floor].nodes[src].nextColumns.Contains(t))
                        mapGrid[floor].nodes[src].nextColumns.Add(t);
                }
            }
        }

        // ── 4단계: 플랫 리스트 동기화 및 초기 위치 설정 ────────────────────
        foreach (var row in mapGrid)
            maps.AddRange(row.nodes);

        curMap = mapGrid[0].nodes[0];
    }

    // 층과 보스 여부에 따라 FEN 문자열을 결정한다.
    // Boss1FEN / Boss2FEN 리스트가 비어 있으면 DefaultFEN으로 폴백.
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

    /// <summary>
    /// 저장 데이터에서 맵 그래프를 복원한다. Init()의 랜덤 생성과 별개로 동작한다.
    ///
    /// curMap은 씬 재생성 시 객체 참조가 끊기므로 floor/column 좌표로 저장했다가
    /// mapGrid 재구성 후 좌표로 참조를 재연결한다.
    /// </summary>
    public void LoadFromSaveData(RunSaveData data)
    {
        maps.Clear();
        mapGrid.Clear();

        currentFloor = data.currentFloor;
        nodesPerFloor = data.nodesPerFloor;

        // MapNodeSaveData → Map 객체로 역변환하여 mapGrid 재구성
        if (data.mapGrid != null)
        {
            foreach (MapFloorSaveData floorData in data.mapGrid)
            {
                if (floorData?.nodes == null) continue;
                MapFloor floor = new MapFloor();
                foreach (MapNodeSaveData nodeData in floorData.nodes)
                {
                    if (nodeData == null) continue;
                    floor.nodes.Add(new Map
                    {
                        ELO = nodeData.elo,
                        FEN = nodeData.fen,
                        isCleared = nodeData.isCleared,
                        floor = nodeData.floor,
                        column = nodeData.column,
                        nextColumns = new System.Collections.Generic.List<int>(nodeData.nextColumns),
                        isAccessible = nodeData.isAccessible,
                        uiPosition = new UnityEngine.Vector2(nodeData.uiPositionX, nodeData.uiPositionY),
                        nodeType = (NodeType)nodeData.nodeType
                    });
                }
                mapGrid.Add(floor);
            }
        }

        // 플랫 리스트 동기화 (Init()과 동일한 마지막 단계)
        foreach (MapFloor floor in mapGrid)
            maps.AddRange(floor.nodes);

        // curMap 복원: 저장된 floor/column 좌표로 참조 재연결
        if (data.curMapFloor >= 0 && data.curMapFloor < mapGrid.Count &&
            data.curMapColumn >= 0 && data.curMapColumn < mapGrid[data.curMapFloor].nodes.Count)
            curMap = mapGrid[data.curMapFloor].nodes[data.curMapColumn];
        else if (maps.Count > 0)
            curMap = maps[0];
    }

    // 전투 승리 후 호출. 클리어 상태 갱신 및 다음 층 노드 접근권 해제.
    public void OnCombatCleared()
    {
        if (curMap == null)
        {
            Debug.LogError("OnCombatCleared: curMap is null. Check map state.");
            return;
        }

        curMap.isCleared = true;

        // 같은 층의 다른 노드 접근권 해제 (역주행 방지)
        foreach (var node in mapGrid[curMap.floor].nodes)
            node.isAccessible = false;

        currentFloor = curMap.floor + 1;

        if (currentFloor < totalFloors)
        {
            // 클리어한 노드의 nextColumns에 연결된 다음 층 노드만 활성화
            foreach (int nextCol in curMap.nextColumns)
                mapGrid[currentFloor].nodes[nextCol].isAccessible = true;
        }
    }
}
