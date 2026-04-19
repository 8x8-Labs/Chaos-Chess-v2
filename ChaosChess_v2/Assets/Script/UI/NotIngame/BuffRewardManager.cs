using System.Collections.Generic;
using UnityEngine;

public class BuffRewardManager : MonoBehaviour
{
    [SerializeField] private List<BuffSO> buffSO;
    [SerializeField] private List<BuffSO> debuffSO;

    [SerializeField] private BuffPanel buffPanel;
    [SerializeField] private BuffPanel debuffPanel;

    private BuffSO _selectedBuff;
    private BuffSO _selectedDebuff;

    void Start()
    {
        RollReward();

        buffPanel.Init(_selectedBuff);
        debuffPanel.Init(_selectedDebuff);
    }

    private void RollReward()
    {
        _selectedBuff = buffSO[Random.Range(0, buffSO.Count)];
        _selectedDebuff = debuffSO[Random.Range(0, debuffSO.Count)];
    }
}