using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임의 데이터를 저장하는 클래스
/// 데이터 종류
/// - 카드 획득 턴 수
/// - 현재 얻은 버프
/// - 현재 가지고있는 카드
/// - 승,무,패 횟수
/// - 현재 게임 결과
/// </summary>
public class PlayerState : MonoBehaviour
{
    public static PlayerState Instance;

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

    private int _defaultCardInterval = 5;
    public int DefaultCardInterval => _defaultCardInterval;

    [SerializeField] private List<GameObject> _cardPool = new();
    public IReadOnlyList<GameObject> CardPool => _cardPool;

    [SerializeField] private List<BuffSO> _buffs = new();
    public IReadOnlyList<BuffSO> Buffs => _buffs;

    public void AddCard(GameObject card) => _cardPool.Add(card);
    public void AddBuff(BuffSO buff) => _buffs.Add(buff);

    [field: SerializeField] public int WinCount { get; private set; } = 0;
    [field: SerializeField] public int DrawCount { get; private set; } = 0;
    [field: SerializeField] public int LoseCount { get; private set; } = 0;

    [SerializeField] private GameResult curGameResult = GameResult.None;

    public GameResult CurGameResult => curGameResult;

    public void EndGame(GameResult result)
    {
        switch (result)
        {
            case GameResult.WhiteWin:
                WinCount++;
                break;
            case GameResult.BlackWin:
                LoseCount++;
                break;
            case GameResult.Draw:
                DrawCount++;
                break;
        }
        curGameResult = result;
    }
}