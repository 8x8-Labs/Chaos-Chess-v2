using UnityEngine;

/// <summary>
/// MainScene 진입 시 저장 파일 유무에 따라 "이어하기" 버튼을 표시/숨김 처리한다.
/// 저장 파일이 없으면 버튼 GameObject 자체를 비활성화한다.
/// </summary>
public class ContinueButtonController : MonoBehaviour
{
    [SerializeField] private GameObject continueButtonObject;

    private void Awake()
    {
        if (continueButtonObject == null) return;
        continueButtonObject.SetActive(SaveManager.Instance != null && SaveManager.Instance.HasSaveData());
    }
}
