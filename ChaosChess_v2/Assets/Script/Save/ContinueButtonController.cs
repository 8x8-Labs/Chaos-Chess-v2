using UnityEngine;

/// <summary>
/// MainScene 진입 시 저장 파일 유무에 따라 "이어하기" 버튼을 표시/숨김 처리한다.
/// 저장 파일이 없으면 버튼 GameObject 자체를 비활성화한다.
///
/// 클라우드 동기화는 네트워크 왕복이라 Start()보다 늦게 끝나는 경우가 많다.
/// 그래서 최초 1회 판정만으로는 부족하고, 동기화가 로컬에 반영된 시점에 다시 갱신해야 한다.
/// </summary>
public class ContinueButtonController : MonoBehaviour
{
    [SerializeField] private GameObject continueButtonObject;

    private void Start()
    {
        Refresh();

        if (CloudSaveManager.Instance != null)
            CloudSaveManager.Instance.OnCloudDataApplied += Refresh;
    }

    private void OnDestroy()
    {
        if (CloudSaveManager.Instance != null)
            CloudSaveManager.Instance.OnCloudDataApplied -= Refresh;
    }

    private void Refresh()
    {
        if (continueButtonObject == null) return;
        continueButtonObject.SetActive(SaveManager.Instance != null && SaveManager.Instance.HasSaveData());
    }
}
