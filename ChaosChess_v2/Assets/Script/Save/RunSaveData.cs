using System;
using System.Collections.Generic;

/// <summary>
/// 버프 1개의 저장 데이터.
/// BuffSO는 ScriptableObject 참조라 JSON 직렬화 불가 → 에셋 이름(buffSOName)으로 저장하고
/// 로드 시 BuffRegistry에서 이름으로 역조회한다.
/// </summary>
[Serializable]
public class BuffSaveData
{
    public string buffSOName;   // BuffSO 에셋 파일명 (ScriptableObject.name)
    public int side;            // BuffSide enum → int (JsonUtility는 enum 직렬화 불안정)
    public int magnitude;       // 적용된 수치
    public bool hasMagnitude;   // magnitude가 확정된 값인지 여부
}

/// <summary>
/// 맵 노드(Map) 1개의 저장 데이터.
/// Map 클래스의 uiPosition(Vector2)은 JsonUtility 중첩 시 누락될 수 있어
/// float x/y로 풀어서 저장한다.
/// </summary>
[Serializable]
public class MapNodeSaveData
{
    public int elo;
    public string fen;
    public string mapName;
    public bool isCleared;
    public int floor;
    public int column;
    public List<int> nextColumns = new();
    public bool isAccessible;
    public float uiPositionX;   // Vector2 uiPosition.x
    public float uiPositionY;   // Vector2 uiPosition.y
    public int nodeType;        // NodeType enum → int
}

/// <summary>
/// 맵 한 층(MapFloor)의 저장 데이터.
/// </summary>
[Serializable]
public class MapFloorSaveData
{
    public List<MapNodeSaveData> nodes = new();
}

/// <summary>
/// 런(Run) 전체 상태의 저장 데이터 루트.
/// JsonUtility.ToJson/FromJson 대상 클래스.
/// 저장 경로: Application.persistentDataPath/run_save.json
/// </summary>
[Serializable]
public class RunSaveData
{
    public string gameMode;                     // GameMode enum → string
    public int winCount;
    public int drawCount;
    public int loseCount;
    public List<string> cardNames = new();      // CardDataSO.CardName 문자열 목록
    public List<BuffSaveData> buffs = new();
    public int currentFloor;                    // MapManager.currentFloor
    public int curMapFloor;                     // curMap 위치 (floor 인덱스)
    public int curMapColumn;                    // curMap 위치 (column 인덱스)
    public int[] nodesPerFloor;                 // 층별 노드 수 배열
    public List<MapFloorSaveData> mapGrid = new();
    public string savedScene;                    // 저장 시점의 활성 씬 이름 (비어 있으면 "MapScene" 사용)
}
