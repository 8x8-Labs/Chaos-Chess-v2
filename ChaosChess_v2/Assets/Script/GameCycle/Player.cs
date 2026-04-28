using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public string StartFEN;
    public List<GameObject> CurrentCard;

    [SerializeField] private CardRandomizer cardRandomizer;
    [SerializeField] private List<GameObject> cardPool;

    private const int DefaultCardInterval = 5;
    private int _cardInterval = DefaultCardInterval;
    private int _playerTurnCount = 0;

    private readonly List<IPlayerBuff> _buffs = new();

    private void Start()
    {
        GameManager.Instance.OnPlayerTurnStarted += HandlePlayerTurnStarted;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnPlayerTurnStarted -= HandlePlayerTurnStarted;
    }

    private void HandlePlayerTurnStarted()
    {
        _playerTurnCount++;
        if (_playerTurnCount < _cardInterval) return;

        _playerTurnCount = 0;
        cardRandomizer.GenerateCard(cardPool);
    }

    public void ApplyBuff(IPlayerBuff buff)
    {
        _buffs.Add(buff);
        buff.OnApply(this);
    }

    public void RemoveBuff(IPlayerBuff buff)
    {
        if (!_buffs.Remove(buff)) return;
        buff.OnRemove(this);
    }

    /// <summary>카드 지급 주기를 delta만큼 조정합니다. 최소값은 1입니다.</summary>
    public void ModifyCardInterval(int delta)
    {
        _cardInterval = Mathf.Max(1, _cardInterval + delta);
    }

    public List<GameObject> CardPool => cardPool;
}
