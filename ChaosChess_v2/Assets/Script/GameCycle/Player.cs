using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private CardRandomizer cardRandomizer;

    [SerializeField] private List<GameObject> _cardPool;
    [SerializeField] private List<BuffPick> _buffs;

    private int _cardInterval;
    private int _maxCardCount;
    private int _playerTurnCount = 0;

    public bool IsCardGrantInitialized { get; private set; }
    public int RemainingTurnsUntilCardGrant => Mathf.Max(0, _cardInterval - _playerTurnCount);
    public bool IsCardGrantReady => _playerTurnCount >= _cardInterval;
    public bool IsCardHandFull => CurrentCardCount >= _maxCardCount;
    public bool IsCardGrantPaused => GameManager.Instance != null && GameManager.Instance.IsCardIntervalPaused;

    private int CurrentCardCount => cardRandomizer != null ? cardRandomizer.CurrentCardCnt : 0;

    public event System.Action OnCardGrantStateChanged;
    public event System.Action OnCardGranted;

    private void Start()
    {
        GameManager.Instance.OnPlayerTurnStarted += HandlePlayerTurnStarted;
        GameManager.Instance.OnCardIntervalPauseChanged += HandleCardIntervalPauseChanged;
        cardRandomizer.OnCardCountChanged += HandleCardCountChanged;

        _cardInterval = PlayerState.Instance.DefaultCardInterval;
        _maxCardCount = PlayerState.Instance.DefaultMaxCardCount;

        _cardPool = new List<GameObject>(PlayerState.Instance.CardPool);
        _buffs = new List<BuffPick>(PlayerState.Instance.Buffs);

        ExecuteBuffs();
        int currentCardCnt = cardRandomizer.CurrentCardCnt;
        int spawnCount = _maxCardCount - currentCardCnt;
        if (spawnCount > 0)
            cardRandomizer.GenerateCard(_cardPool, spawnCount);

        IsCardGrantInitialized = true;
        NotifyCardGrantStateChanged();
    }

    private void ExecuteBuffs()
    {
        foreach (var buff in _buffs)
        {
            buff.TryApply(this);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerTurnStarted -= HandlePlayerTurnStarted;
            GameManager.Instance.OnCardIntervalPauseChanged -= HandleCardIntervalPauseChanged;
        }

        if (cardRandomizer != null)
            cardRandomizer.OnCardCountChanged -= HandleCardCountChanged;
    }

    private void HandlePlayerTurnStarted()
    {
        if (GameManager.Instance.IsCardIntervalPaused)
        {
            NotifyCardGrantStateChanged();
            return;
        }

        _playerTurnCount++;
        if (_playerTurnCount < _cardInterval || cardRandomizer.CurrentCardCnt >= _maxCardCount)
        {
            NotifyCardGrantStateChanged();
            return;
        }

        int generatedCount = cardRandomizer.GenerateCard(_cardPool);
        if (generatedCount <= 0)
        {
            NotifyCardGrantStateChanged();
            return;
        }

        _playerTurnCount = 0;
        NotifyCardGrantStateChanged();
        OnCardGranted?.Invoke();
    }

    /// <summary>카드 지급 주기를 delta만큼 조정합니다. 최소값은 1입니다.</summary>
    public void ModifyCardInterval(int delta)
    {
        _cardInterval = Mathf.Max(1, _cardInterval + delta);
        NotifyCardGrantStateChanged();
    }

    /// <summary>보유할 수 있는 카드 개수를 delta만큼 조정합니다. 최소값은 1, 최대값은 PlayerState.Instance.DefaultMaxCardCount(4)입니다.</summary>
    public void ModifyMaxCardCount(int delta)
    {
        _maxCardCount = Mathf.Clamp(_maxCardCount + delta, 1, PlayerState.Instance.DefaultMaxCardCount);
        NotifyCardGrantStateChanged();
    }

    private void HandleCardCountChanged(int _) => NotifyCardGrantStateChanged();

    private void HandleCardIntervalPauseChanged() => NotifyCardGrantStateChanged();

    private void NotifyCardGrantStateChanged()
    {
        if (IsCardGrantInitialized)
            OnCardGrantStateChanged?.Invoke();
    }
}
