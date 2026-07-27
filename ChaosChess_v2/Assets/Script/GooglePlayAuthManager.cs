using System;
using UnityEngine;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

/// <summary>
/// 구글 플레이 게임즈(GPGS v2) 로그인을 담당하는 싱글톤.
///
/// GPGS v2부터는 앱이 시작될 때 네이티브 SDK가 스스로 사인인을 시도하므로,
/// 게임 코드는 Authenticate()로 그 "자동 로그인 결과"를 받아오기만 하면 된다.
/// 자동 로그인이 실패(취소/오류)했을 때만 SignInManually()로 다시 시도한다.
///
/// 씬에 배치할 필요 없이 RuntimeInitializeOnLoadMethod로 자동 생성된다.
/// </summary>
public class GooglePlayAuthManager : MonoBehaviour
{
    public static GooglePlayAuthManager Instance { get; private set; }

    /// <summary>로그인 상태가 바뀔 때마다 호출된다. (UI 갱신용)</summary>
    public event Action<bool> OnAuthenticationChanged;

    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// 로그인 요청을 보내고 아직 결과를 받지 못한 상태.
    /// UI가 "로그인 중" 표시와 버튼 중복 클릭 차단에 쓴다.
    /// </summary>
    public bool IsSignInInProgress { get; private set; }

    /// <summary>로그인된 플레이어의 표시 이름. 미로그인 시 빈 문자열.</summary>
    public string UserName { get; private set; } = string.Empty;

    /// <summary>로그인된 플레이어의 고유 ID. 미로그인 시 빈 문자열.</summary>
    public string UserId { get; private set; } = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject go = new GameObject(nameof(GooglePlayAuthManager));
        go.AddComponent<GooglePlayAuthManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        TryAutoSignIn();
    }

    /// <summary>
    /// 앱 시작 시 SDK가 수행한 자동 사인인의 결과를 조회한다.
    /// 에디터/비안드로이드 환경에서는 아무것도 하지 않는다.
    /// </summary>
    private void TryAutoSignIn()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Social.Active를 GPGS로 교체해 Social API도 함께 쓸 수 있게 한다.
        PlayGamesPlatform.Activate();
        IsSignInInProgress = true;
        PlayGamesPlatform.Instance.Authenticate(ProcessAuthentication);
#else
        Debug.Log("[GPGS] 안드로이드 빌드에서만 동작합니다. 자동 로그인을 건너뜁니다.");
#endif
    }

    /// <summary>
    /// 자동 로그인이 실패했을 때 사용자가 직접(버튼 등) 로그인을 재시도할 때 호출한다.
    /// 이미 로그인된 상태면 무시한다.
    /// </summary>
    public void SignInManually()
    {
        // 자동 로그인이 아직 진행 중일 때 겹쳐 호출하면 콜백이 꼬이므로 함께 막는다.
        if (IsAuthenticated || IsSignInInProgress) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        IsSignInInProgress = true;
        PlayGamesPlatform.Instance.ManuallyAuthenticate(ProcessAuthentication);
#else
        Debug.Log("[GPGS] 안드로이드 빌드에서만 동작합니다. 수동 로그인을 건너뜁니다.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void ProcessAuthentication(SignInStatus status)
    {
        IsSignInInProgress = false;

        if (status == SignInStatus.Success)
        {
            UserName = PlayGamesPlatform.Instance.GetUserDisplayName();
            UserId = PlayGamesPlatform.Instance.GetUserId();
            SetAuthenticated(true);
            Debug.Log($"[GPGS] 로그인 성공: {UserName} ({UserId})");
        }
        else
        {
            UserName = string.Empty;
            UserId = string.Empty;
            SetAuthenticated(false);
            Debug.LogWarning($"[GPGS] 로그인 실패: {status}. 로그인 버튼으로 재시도할 수 있습니다.");
        }
    }
#endif

    private void SetAuthenticated(bool value)
    {
        IsAuthenticated = value;
        OnAuthenticationChanged?.Invoke(value);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
