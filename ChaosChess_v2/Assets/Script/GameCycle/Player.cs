using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public string StartFEN;
    public List<GameObject> CurrentCard;
    [SerializeField] private CardRandomizer cardRandomizer;

    private List<GameObject> _cardPool;
    private List<IPlayerBuff> _buffs;

    private int _cardInterval;
    private int _playerTurnCount = 0;

    private void Start()
    {
        GameManager.Instance.OnPlayerTurnStarted += HandlePlayerTurnStarted;

        _cardInterval = PlayerState.Instance.DefaultCardInterval;

        _cardPool = new List<GameObject>(PlayerState.Instance.CardPool);
        _buffs = new List<IPlayerBuff>(PlayerState.Instance.Buffs);

        ExecuteBuffs();
    }

    private void ExecuteBuffs()
    {
        // TODO: 버프 실행하는 로직 필요
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
        cardRandomizer.GenerateCard(_cardPool);
    }

    /// <summary>카드 지급 주기를 delta만큼 조정합니다. 최소값은 1입니다.</summary>
    public void ModifyCardInterval(int delta)
    {
        _cardInterval = Mathf.Max(1, _cardInterval + delta);
    }
}
