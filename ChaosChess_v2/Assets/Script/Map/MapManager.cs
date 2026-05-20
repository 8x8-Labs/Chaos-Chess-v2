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
        currentFloor = 0;

        int startELO = Random.Range(800, 1200);

        for (int i = 0; i < totalFloors; i++)
        {
            string fen = DefaultFEN;
            if (i == 2 && Boss1FEN.Count > 0)
                fen = Boss1FEN[Random.Range(0, Boss1FEN.Count)];
            if (i == 5 && Boss2FEN.Count > 0)
                fen = Boss2FEN[Random.Range(0, Boss2FEN.Count)];
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

    public void StartRun() => Init();

    public void StartPractice(PracticeDifficulty difficulty)
    {
        maps.Clear();
        currentFloor = 0;
        Map practiceMap = CreatePracticeMap(difficulty);

        maps.Add(practiceMap);
        curMap = practiceMap;
    }

    private Map CreatePracticeMap(PracticeDifficulty difficulty)
    {
        Map practiceMap = new Map
        {
            floor = 0,
            isCleared = false
        };

        ApplyPracticeSetting(difficulty, practiceMap);
        return practiceMap;
    }

    private int GetPracticeElo(PracticeDifficulty difficulty)
    {
        switch (difficulty)
        {
            case PracticeDifficulty.Easy:
                return easyPracticeELO;
            case PracticeDifficulty.Normal:
                return normalPracticeELO;
            case PracticeDifficulty.Hard:
                return hardPracticeELO;
            default:
                return normalPracticeELO;
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
            default:
                return DefaultFEN;
        }
    }

    private void ApplyPracticeSetting(PracticeDifficulty difficulty, Map map)
    {
        map.ELO = GetPracticeElo(difficulty);
        map.FEN = GetPracticeFen(difficulty);
    }

    public void OnCombatCleared()
    {
        maps[currentFloor].isCleared = true;

        currentFloor++;

        if (currentFloor >= totalFloors)
            return;
        curMap = maps[currentFloor];
    }
}