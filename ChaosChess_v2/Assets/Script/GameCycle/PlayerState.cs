using System.Collections.Generic;
using UnityEngine;

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

    public int DefaultCardInterval = 5;

    [SerializeField] private List<GameObject> _cardPool = new();
    public IReadOnlyList<GameObject> CardPool => _cardPool;

    private List<IPlayerBuff> _buffs = new();
    public IReadOnlyList<IPlayerBuff> Buffs => _buffs;

    public void AddCard(GameObject card) => _cardPool.Add(card);
    public void AddBuff(IPlayerBuff buff) => _buffs.Add(buff);

    [field: SerializeField] public int WinCount { get; private set; } = 0;
    [field: SerializeField] public int DrawCount { get; private set; } = 0;
    [field: SerializeField] public int LoseCount { get; private set; } = 0;

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
    }
}