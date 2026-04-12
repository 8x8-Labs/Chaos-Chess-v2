using UnityEngine;
using UnityEngine.Events;

public class BuffPanel : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private UnityEvent OnPanelEnabled;
    [SerializeField] private UnityEvent OnPanelDisabled;

    // 패널을 활성화시키고 효과 및 첫 선택 버튼을 설정
    public virtual void EnablePanel()
    {
        OnPanelEnabled?.Invoke();
    }

    // 패널을 비활성화 시키고 알파를 0으로 바꿈
    public virtual void DisablePanel()
    {
        OnPanelDisabled?.Invoke();
        Destroy(gameObject);
    }
}