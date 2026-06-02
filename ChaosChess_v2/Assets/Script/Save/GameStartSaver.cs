using UnityEngine;

/// <summary>
/// MainGameScene 진입 시 현재 런 상태를 즉시 저장한다.
/// 전투 도중 앱이 종료되더라도 전투 시작 직전 상태로 이어하기가 가능하다.
/// 연습 모드(Practice)에서는 저장하지 않는다.
/// </summary>
public class GameStartSaver : MonoBehaviour
{
    private void Start()
    {
        if (GameCycleManager.Instance == null || SaveManager.Instance == null) return;
        if (GameCycleManager.Instance.IsPracticeMode) return;

        SaveManager.Instance.Save();
    }
}
