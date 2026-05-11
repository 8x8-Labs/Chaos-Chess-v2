using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private CardRandomizer cardRandomizer;

    [SerializeField] private List<GameObject> _cardPool;
    [SerializeField] private List<IPlayerBuff> _buffs;

    private int _cardInterval;
    private int _maxCardCount;
    private int _playerTurnCount = 0;

    private void Start()
    {
        GameManager.Instance.OnPlayerTurnStarted += HandlePlayerTurnStarted;

        _cardInterval = PlayerState.Instance.DefaultCardInterval;
        _maxCardCount = PlayerState.Instance.DefaultMaxCardCount;

        _cardPool = new List<GameObject>(PlayerState.Instance.CardPool);
        _buffs = new List<IPlayerBuff>(PlayerState.Instance.Buffs);

        ExecuteBuffs();
        int currentCardCnt = cardRandomizer.CurrentCardCnt;
        for (int i = 0; i < _maxCardCount - currentCardCnt; i++)
        {
            cardRandomizer.GenerateCard(_cardPool);
        }
    }

    private void ExecuteBuffs()
    {
        foreach (var buff in _buffs)
        {
            buff.OnApply(this);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerTurnStarted -= HandlePlayerTurnStarted;
    }

    private void HandlePlayerTurnStarted()
    {
        _playerTurnCount++;
        if (_playerTurnCount < _cardInterval || cardRandomizer.CurrentCardCnt >= _maxCardCount) return;
        _playerTurnCount = 0;

        cardRandomizer.GenerateCard(_cardPool);
    }

    /// <summary>카드 지급 주기를 delta만큼 조정합니다. 최소값은 1입니다.</summary>
    public void ModifyCardInterval(int delta)
    {
        _cardInterval = Mathf.Max(1, _cardInterval + delta);
    }

    /// <summary>보유할 수 있는 카드 개수를 delta만큼 조정합니다. 최소값은 1, 최대값은 PlayerState.Instance.DefaultMaxCardCount(4)입니다.</summary>
    public void ModifyMaxCardCount(int delta)
    {   
        _maxCardCount = Mathf.Clamp(_maxCardCount + delta, 1, PlayerState.Instance.DefaultMaxCardCount);
    }
}
