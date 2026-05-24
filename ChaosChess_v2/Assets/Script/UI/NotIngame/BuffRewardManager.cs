using System.Collections.Generic;
using UnityEngine;

public class BuffRewardManager : MonoBehaviour
{
    [SerializeField] private List<BuffSO> buffPool;

    [SerializeField] private BuffPanel buffPanel;
    [SerializeField] private BuffPanel debuffPanel;

    private BuffSO _selectedBuff;
    private BuffSO _selectedDebuff;

    void Start()
    {
        RollReward();

        buffPanel.Init(_selectedBuff, BuffSide.Buff);
        debuffPanel.Init(_selectedDebuff, BuffSide.Debuff);
    }

    private void RollReward()
    {
        _selectedBuff = RollBySide(BuffSide.Buff);
        _selectedDebuff = RollBySide(BuffSide.Debuff);
    }

    private BuffSO RollBySide(BuffSide side)
    {
        List<BuffSO> candidates = buffPool.FindAll(x => x != null && x.CanUse(side));
        if (candidates.Count == 0) return null;

        int totalWeight = 0;
        foreach (BuffSO candidate in candidates)
        {
            totalWeight += candidate.GetWeight(side);
        }

        if (totalWeight <= 0) return candidates[Random.Range(0, candidates.Count)];

        int roll = Random.Range(0, totalWeight);
        int accumulated = 0;

        foreach (BuffSO candidate in candidates)
        {
            accumulated += candidate.GetWeight(side);
            if (roll < accumulated)
            {
                return candidate;
            }
        }

        return candidates[candidates.Count - 1];
    }
}
