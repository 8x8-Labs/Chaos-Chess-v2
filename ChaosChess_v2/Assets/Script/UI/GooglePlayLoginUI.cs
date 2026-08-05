using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 구글 플레이 게임즈 로그인 상태를 표시하고, 자동 로그인이 실패했을 때 재시도 버튼을 제공한다.
///
/// GPGS v2는 앱 시작 시 SDK가 스스로 사인인을 시도하므로, 이 UI가 켜지는 시점에는
/// 이미 로그인이 끝나 있을 수도(=이벤트를 놓쳤을 수도) 있다.
/// 그래서 이벤트 구독만으로는 부족하고 OnEnable에서 현재 상태를 한 번 직접 읽어 갱신한다.
///
/// 타이틀/설정 캔버스 등 아무 곳에나 붙여서 쓰면 된다.
/// </summary>
public class GooglePlayLoginUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button signInButton;

    [Header("Options")]
    [Tooltip("로그인된 뒤에는 재시도 버튼을 아예 숨긴다.")]
    [SerializeField] private bool hideButtonWhenSignedIn = true;

    [Header("Messages")]
    [Tooltip("{0} 자리에 플레이어 닉네임이 들어간다.")]
    [SerializeField] private string signedInFormat = "{0}";
    [SerializeField] private string signedOutMessage = "로그인되지 않음";
    [SerializeField] private string connectingMessage = "로그인 중...";

    private GooglePlayAuthManager auth;

    private void OnEnable()
    {
        // RuntimeInitializeOnLoadMethod(BeforeSceneLoad)로 생성되므로 이 시점엔 이미 존재한다.
        // 그래도 씬을 단독 실행하는 경우 등을 대비해 null을 허용한다.
        auth = GooglePlayAuthManager.Instance;

        if (auth != null)
            auth.OnAuthenticationChanged += HandleAuthenticationChanged;

        if (signInButton != null)
            signInButton.onClick.AddListener(OnSignInButtonClicked);

        Refresh();
    }

    private void OnDisable()
    {
        if (auth != null)
            auth.OnAuthenticationChanged -= HandleAuthenticationChanged;

        if (signInButton != null)
            signInButton.onClick.RemoveListener(OnSignInButtonClicked);

        auth = null;
    }

    private void OnSignInButtonClicked()
    {
        // 진행 중 중복 호출은 SignInManually() 내부에서도 막지만,
        // 여기서 먼저 걸러야 눌린 직후 UI가 곧바로 "로그인 중"으로 바뀐다.
        if (auth == null || auth.IsAuthenticated || auth.IsSignInInProgress) return;

        auth.SignInManually();
        Refresh();
    }

    // 로그인 성공/실패 모두 이 콜백으로 들어온다. 인자 대신 매니저 상태를 다시 읽어
    // "로그인 중"과 "실패"를 구분한다.
    private void HandleAuthenticationChanged(bool _) => Refresh();

    private void Refresh()
    {
        bool signedIn = auth != null && auth.IsAuthenticated;
        bool connecting = auth != null && auth.IsSignInInProgress;

        if (statusText != null)
        {
            if (signedIn)
            {
                // 닉네임을 아직 못 받아온 드문 경우 빈 줄이 보이지 않도록 ID로 대체한다.
                string name = string.IsNullOrEmpty(auth.UserName) ? auth.UserId : auth.UserName;
                statusText.text = string.IsNullOrEmpty(name)
                    ? signedOutMessage
                    : string.Format(signedInFormat, name);
            }
            else
            {
                statusText.text = connecting ? connectingMessage : signedOutMessage;
            }
        }

        if (signInButton != null)
        {
            bool showButton = !signedIn || !hideButtonWhenSignedIn;
            if (signInButton.gameObject.activeSelf != showButton)
                signInButton.gameObject.SetActive(showButton);

            // 응답을 기다리는 동안에는 눌러도 소용없으므로 잠근다.
            signInButton.interactable = !signedIn && !connecting;
        }
    }
}
