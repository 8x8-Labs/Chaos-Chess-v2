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
        RollDistinctPair(out _selectedBuff, out _selectedDebuff);

        buffPanel.Init(_selectedBuff, BuffSide.Buff);
        debuffPanel.Init(_selectedDebuff, BuffSide.Debuff);
    }

    /// <summary>
    /// 가능한 경우 같은 BuffSO가 동시에 나오지 않도록 버프/디버프 한 쌍을 가중치 기반으로 선택합니다.
    /// </summary>
    private void RollDistinctPair(out BuffSO selectedBuff, out BuffSO selectedDebuff)
    {
        selectedBuff = null;
        selectedDebuff = null;

        // 1. 양쪽에서 실제로 등장 가능한 후보를 모읍니다.
        List<BuffSO> buffCandidates = buffPool.FindAll(x => x != null && x.CanUse(BuffSide.Buff));
        List<BuffSO> debuffCandidates = buffPool.FindAll(x => x != null && x.CanUse(BuffSide.Debuff));
        if (buffCandidates.Count == 0 || debuffCandidates.Count == 0) return;

        // 후보가 같은 1개뿐이면 같은 종류 동시 등장을 예외적으로 허용합니다.
        if (buffCandidates.Count == 1 && debuffCandidates.Count == 1)
        {
            selectedBuff = buffCandidates[0];
            selectedDebuff = debuffCandidates[0];
            return;
        }

        // 2. 버프 후보별 쌍 가중치 계산에 사용할 디버프 전체 가중치를 구합니다.
        int totalDebuffWeight = 0;
        foreach (BuffSO debuffCandidate in debuffCandidates)
        {
            totalDebuffWeight += debuffCandidate.GetWeight(BuffSide.Debuff);
        }

        if (totalDebuffWeight <= 0)
        {
            PickFirstDistinctPair(buffCandidates, debuffCandidates, out selectedBuff, out selectedDebuff);
            return;
        }

        // 3. 같은 BuffSO를 제외했을 때 가능한 모든 쌍의 총 가중치를 구합니다.
        int totalPairWeight = 0;
        foreach (BuffSO buffCandidate in buffCandidates)
        {
            int buffWeight = buffCandidate.GetWeight(BuffSide.Buff);
            int blockedDebuffWeight = buffCandidate.CanUse(BuffSide.Debuff)
                ? buffCandidate.GetWeight(BuffSide.Debuff)
                : 0;

            // 버프 후보별로 함께 나올 수 있는 디버프 총 가중치를 반영합니다.
            totalPairWeight += buffWeight * Mathf.Max(0, totalDebuffWeight - blockedDebuffWeight);
        }

        if (totalPairWeight <= 0)
        {
            PickFirstDistinctPair(buffCandidates, debuffCandidates, out selectedBuff, out selectedDebuff);
            return;
        }

        // 4. 총 쌍 가중치에서 버프를 먼저 선택합니다.
        int roll = Random.Range(0, totalPairWeight);
        int accumulated = 0;

        foreach (BuffSO buffCandidate in buffCandidates)
        {
            int buffWeight = buffCandidate.GetWeight(BuffSide.Buff);
            int blockedDebuffWeight = buffCandidate.CanUse(BuffSide.Debuff)
                ? buffCandidate.GetWeight(BuffSide.Debuff)
                : 0;
            int candidatePairWeight = buffWeight * Mathf.Max(0, totalDebuffWeight - blockedDebuffWeight);

            accumulated += candidatePairWeight;
            if (roll < accumulated)
            {
                selectedBuff = buffCandidate;
                // 5. 선택된 버프와 같은 BuffSO만 제외하고 디버프를 가중치 기반으로 선택합니다.
                selectedDebuff = RollFromCandidatesExcluding(
                    debuffCandidates,
                    BuffSide.Debuff,
                    selectedBuff,
                    totalDebuffWeight);
                return;
            }
        }
    }

    /// <summary>
    /// 가중치 기반 선택이 불가능할 때 첫 번째 유효 쌍을 고정 선택합니다.
    /// </summary>
    private void PickFirstDistinctPair(
        List<BuffSO> buffCandidates,
        List<BuffSO> debuffCandidates,
        out BuffSO selectedBuff,
        out BuffSO selectedDebuff)
    {
        foreach (BuffSO buffCandidate in buffCandidates)
        {
            foreach (BuffSO debuffCandidate in debuffCandidates)
            {
                if (buffCandidate == debuffCandidate) continue;

                selectedBuff = buffCandidate;
                selectedDebuff = debuffCandidate;
                return;
            }
        }

        selectedBuff = buffCandidates[0];
        selectedDebuff = debuffCandidates[0];
    }

    /// <summary>
    /// 이미 계산된 후보/전체 가중치를 재사용해 특정 BuffSO만 제외하고 하나를 선택합니다.
    /// </summary>
    private BuffSO RollFromCandidatesExcluding(List<BuffSO> candidates, BuffSide side, BuffSO excluded, int totalWeight)
    {
        // 1. 제외할 후보의 가중치를 전체 가중치에서 빼 실제 선택 가능한 가중치를 구합니다.
        int excludedWeight = excluded != null && excluded.CanUse(side) ? excluded.GetWeight(side) : 0;
        int availableWeight = Mathf.Max(0, totalWeight - excludedWeight);
        if (availableWeight <= 0) return null;

        // 2. 제외 후보를 건너뛰면서 남은 후보 중 하나를 가중치 기반으로 선택합니다.
        int roll = Random.Range(0, availableWeight);
        int accumulated = 0;

        foreach (BuffSO candidate in candidates)
        {
            if (candidate == excluded) continue;

            accumulated += candidate.GetWeight(side);
            if (roll < accumulated)
            {
                return candidate;
            }
        }

        return null;
    }
}
