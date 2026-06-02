using UnityEngine;

/// <summary>
/// MapScene 진입 시 현재 런 상태를 즉시 저장한다.
/// 새 게임 시작 직후 및 보상 선택 후 맵으로 돌아올 때 모두 저장된다.
/// 연습 모드(Practice)에서는 저장하지 않는다.
/// </summary>
public class MapSceneSaver : MonoBehaviour
{
    private void Start()
    {
        if (GameCycleManager.Instance == null || SaveManager.Instance == null) return;
        if (GameCycleManager.Instance.IsPracticeMode) return;

        SaveManager.Instance.Save();
    }
}
