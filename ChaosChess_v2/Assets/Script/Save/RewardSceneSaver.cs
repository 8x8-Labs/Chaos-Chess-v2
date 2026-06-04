using UnityEngine;

/// <summary>
/// RewardScene 진입 시 현재 런 상태를 즉시 저장한다.
/// Awake()를 사용하는 이유: CardRewardManager.Start()가 보상 카드를 덱에 즉시 추가하므로,
/// 그 전에 저장해야 이어하기 시 보상 카드가 중복으로 추가되는 것을 방지할 수 있다.
/// 연습 모드(Practice)에서는 저장하지 않는다.
/// </summary>
public class RewardSceneSaver : MonoBehaviour
{
    private void Awake()
    {
        if (GameCycleManager.Instance == null || SaveManager.Instance == null) return;
        if (GameCycleManager.Instance.IsPracticeMode) return;

        SaveManager.Instance.Save();
    }
}
