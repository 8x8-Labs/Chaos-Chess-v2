using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 런(Run) 상태의 저장/불러오기/삭제를 담당하는 매니저.
///
/// 저장 시점: 보상 선택 완료 후 (RewardNextButton에서 호출)
/// 불러오기 시점: 이어하기 버튼 클릭 시 (GameCycleManager.ContinueRun에서 호출)
/// 삭제 시점: 패배 또는 보스 클리어로 런 종료 시 (UIButton.LoadEndGameScene에서 호출)
///
/// 저장 형식: JSON (JsonUtility)
/// 저장 경로: Application.persistentDataPath/run_save.json
///            → 플랫폼별 자동 매핑 (Android: /data/data/<package>/files, iOS: Documents)
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance;

    // Path.Combine으로 경로 구분자 차이(Windows \, Android/iOS /)를 자동 처리한다
    private string SavePath => Path.Combine(Application.persistentDataPath, "run_save.json");

    private string _loadedSavedScene = "MapScene";

    /// <summary>Load() 이후 복원된 저장 씬 이름을 반환한다. 저장 파일에 없으면 "MapScene".</summary>
    public string GetSavedScene() =>
        string.IsNullOrWhiteSpace(_loadedSavedScene) ? "MapScene" : _loadedSavedScene;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>저장 파일이 존재하는지 확인한다. 타이틀 UI에서 이어하기 버튼 표시 여부 결정에 사용.</summary>
    public bool HasSaveData() => File.Exists(SavePath);

    /// <summary>
    /// 현재 런 상태를 JSON으로 직렬화하여 파일에 저장한다.
    /// PlayerState, MapManager, GameCycleManager 순으로 수집한다.
    /// </summary>
    public void Save()
    {
        try
        {
            RunSaveData data = new RunSaveData();

            WritePlayerState(data);
            WriteMapState(data);
            WriteGameCycleState(data);

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveManager.Save: 저장 중 오류 발생 - {e.Message}");
        }
    }

    /// <summary>
    /// 저장 파일을 읽어 런 상태를 복원한다.
    /// PlayerState, MapManager, GameCycleManager 순으로 적용한다.
    /// </summary>
    public void Load()
    {
        if (!HasSaveData())
        {
            Debug.LogWarning("SaveManager.Load: 저장 파일 없음");
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            RunSaveData data = JsonUtility.FromJson<RunSaveData>(json);

            if (data == null)
            {
                Debug.LogError("SaveManager.Load: 저장 데이터가 비어있거나 올바르지 않습니다.");
                return;
            }

            ReadPlayerState(data);
            ReadMapState(data);
            ReadGameCycleState(data);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveManager.Load: 로드 중 오류 발생 - {e.Message}");
        }
    }

    /// <summary>저장 파일을 삭제한다. 런 종료(패배/클리어) 시 호출한다.</summary>
    public void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }

    // ── 저장 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// PlayerState의 카드 목록, 버프 목록, 승/무/패 횟수를 data에 기록한다.
    /// - 카드: GameObject 참조 불가 → CardDataSO.CardName 문자열로 변환
    /// - 버프: BuffSO 참조 불가 → ScriptableObject.name(에셋 파일명)으로 변환
    /// </summary>
    private void WritePlayerState(RunSaveData data)
    {
        PlayerState ps = PlayerState.Instance;
        if (ps == null) return;

        data.winCount = ps.WinCount;
        data.drawCount = ps.DrawCount;
        data.loseCount = ps.LoseCount;

        data.cardNames = new List<string>();
        foreach (GameObject card in ps.CardPool)
        {
            CardData cardData = card.GetComponent<CardData>();
            if (cardData?.DataSO != null)
                data.cardNames.Add(cardData.DataSO.CardName);
        }

        data.buffs = new List<BuffSaveData>();
        foreach (BuffPick pick in ps.Buffs)
        {
            if (pick.Definition == null) continue;
            data.buffs.Add(new BuffSaveData
            {
                buffSOName = pick.Definition.name,
                side = (int)pick.Side,
                magnitude = pick.AppliedMagnitude,
                hasMagnitude = pick.HasAppliedMagnitude
            });
        }
    }

    /// <summary>
    /// MapManager의 맵 그래프 전체와 현재 노드 위치를 data에 기록한다.
    /// - curMap: 객체 참조 불가 → floor/column 좌표로 저장 (로드 시 재연결)
    /// - uiPosition(Vector2): 중첩 직렬화 누락 방지 위해 float x/y로 분리 저장
    /// </summary>
    private void WriteMapState(RunSaveData data)
    {
        MapManager mm = MapManager.Instance;
        if (mm == null) return;

        data.currentFloor = mm.currentFloor;
        data.curMapFloor = mm.curMap?.floor ?? 0;
        data.curMapColumn = mm.curMap?.column ?? 0;
        data.nodesPerFloor = mm.nodesPerFloor;
        data.savedScene = SceneManager.GetActiveScene().name;

        data.mapGrid = new List<MapFloorSaveData>();
        foreach (MapFloor floor in mm.mapGrid)
        {
            MapFloorSaveData floorData = new MapFloorSaveData();
            foreach (Map node in floor.nodes)
            {
                floorData.nodes.Add(new MapNodeSaveData
                {
                    elo = node.ELO,
                    fen = node.FEN,
                    isCleared = node.isCleared,
                    floor = node.floor,
                    column = node.column,
                    nextColumns = new List<int>(node.nextColumns),
                    isAccessible = node.isAccessible,
                    uiPositionX = node.uiPosition.x,
                    uiPositionY = node.uiPosition.y,
                    nodeType = (int)node.nodeType
                });
            }
            data.mapGrid.Add(floorData);
        }
    }

    private void WriteGameCycleState(RunSaveData data)
    {
        if (GameCycleManager.Instance == null) return;
        data.gameMode = GameCycleManager.Instance.CurrentMode.ToString();
    }

    // ── 불러오기 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 저장된 카드/버프/승무패를 PlayerState에 복원한다.
    /// - InitializeRun()으로 초기화 후 저장값으로 덮어쓴다.
    /// - 카드: CardName → CardRandomizerManager.AllCards에서 역조회
    /// - 버프: 에셋 이름 → BuffRegistry에서 역조회
    /// </summary>
    private void ReadPlayerState(RunSaveData data)
    {
        PlayerState ps = PlayerState.Instance;
        if (ps == null) return;

        ps.InitializeRun();
        ps.SetWinDrawLose(data.winCount, data.drawCount, data.loseCount);

        if (CardRandomizerManager.Instance != null && data.cardNames != null)
        {
            foreach (string cardName in data.cardNames)
            {
                GameObject found = FindCardByName(cardName);
                if (found != null)
                    ps.AddCard(found);
                else
                    Debug.LogWarning($"SaveManager.Load: 카드 '{cardName}' 를 찾을 수 없음");
            }
        }

        if (BuffRegistry.Instance != null && data.buffs != null)
        {
            foreach (BuffSaveData buffData in data.buffs)
            {
                BuffSO buffSO = BuffRegistry.Instance.GetByName(buffData.buffSOName);
                if (buffSO == null)
                {
                    Debug.LogWarning($"SaveManager.Load: 버프 '{buffData.buffSOName}' 를 찾을 수 없음");
                    continue;
                }

                BuffSide side = (BuffSide)buffData.side;
                if (buffData.hasMagnitude)
                    ps.AddBuff(buffSO, side, buffData.magnitude);
                else
                    ps.AddBuff(buffSO, side);
            }
        }
    }

    /// <summary>
    /// 저장된 맵 그래프를 MapManager에 복원한다.
    /// 실제 복원 로직은 MapManager.LoadFromSaveData에 위임한다.
    /// </summary>
    private void ReadMapState(RunSaveData data)
    {
        MapManager mm = MapManager.Instance;
        if (mm == null) return;

        mm.LoadFromSaveData(data);
        _loadedSavedScene = string.IsNullOrWhiteSpace(data.savedScene) ? "MapScene" : data.savedScene;
    }

    private void ReadGameCycleState(RunSaveData data)
    {
        // CurrentMode는 ContinueRun()에서 GameMode.Run으로 직접 설정하므로 여기서는 처리하지 않는다
    }

    /// <summary>CardRandomizerManager.AllCards에서 CardName이 일치하는 프리팹을 반환한다.</summary>
    private GameObject FindCardByName(string cardName)
    {
        if (CardRandomizerManager.Instance.AllCards == null) return null;
        foreach (GameObject card in CardRandomizerManager.Instance.AllCards)
        {
            if (card == null) continue;
            CardData data = card.GetComponent<CardData>();
            if (data?.DataSO != null && data.DataSO.CardName == cardName)
                return card;
        }
        return null;
    }
}
