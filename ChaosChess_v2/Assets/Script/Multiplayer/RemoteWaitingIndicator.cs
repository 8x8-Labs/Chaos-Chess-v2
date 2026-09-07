using UnityEngine;

/// <summary>
/// 상대 행동을 기다리는 동안 상대 프로필의 "..." 표시를 켭니다.
///
/// 내 차례에는 아무것도 표시하지 않고, 상대 수가 도착하거나 대국이 끝나면 꺼집니다.
///
/// 연출은 연결한 오브젝트에 맡깁니다. Animator가 붙어 있으면 켜지는 순간 재생이 시작되고
/// 꺼질 때 멈추며, 다시 켜면 처음 프레임부터 재생됩니다.
///
/// 원격 대전이 아닌 매치(AI 대전)에서는 표시하지 않습니다.
/// </summary>
public class RemoteWaitingIndicator : MonoBehaviour
{
    [Tooltip("상대 차례일 때 켤 오브젝트입니다. 상대 프로필 아래의 \"...\" 표시를 연결하세요.")]
    [SerializeField] private GameObject remoteThinkingRoot;

    [Tooltip("비워두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private RemoteTurnProvider remoteTurnProvider;

    private void Start()
    {
        SetVisible(false);

        // RemoteTurnProvider 오브젝트는 AI 대전에서도 씬에 그대로 있으므로,
        // 존재 여부가 아니라 이번 매치의 모드로 판단해야 합니다.
        bool multiplayer = GameCycleManager.Instance != null
            && GameCycleManager.Instance.CurrentMode == GameMode.Multiplayer;

        if (!multiplayer)
            return;

        if (remoteTurnProvider == null)
            remoteTurnProvider = FindFirstObjectByType<RemoteTurnProvider>();

        if (remoteTurnProvider == null)
            return;

        remoteTurnProvider.WaitingForRemoteChanged += HandleWaitingChanged;
        HandleWaitingChanged(remoteTurnProvider.IsWaitingForRemote);
    }

    private void OnDestroy()
    {
        if (remoteTurnProvider != null)
            remoteTurnProvider.WaitingForRemoteChanged -= HandleWaitingChanged;
    }

    private void HandleWaitingChanged(bool waitingForRemote)
    {
        // 대국이 끝났으면 상대도 더 둘 것이 없습니다.
        bool running = GameManager.Instance != null && !GameManager.Instance.IsEndGame;

        SetVisible(running && waitingForRemote);
    }

    private void SetVisible(bool visible)
    {
        if (remoteThinkingRoot != null)
            remoteThinkingRoot.SetActive(visible);
    }
}
