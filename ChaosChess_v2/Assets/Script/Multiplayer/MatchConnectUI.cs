using TMPro;
using UnityEngine;

/// <summary>
/// 룸코드 기반 멀티플레이 연결 화면의 상태 구독 담당입니다.
/// 버튼 클릭(호스트/참가/코드 복사/취소)은 전부 UIButton의 ButtonType
/// (HostMultiplayer/JoinMultiplayer/CopyMultiplayerJoinCode/CancelMultiplayerConnect)이
/// 처리하므로, 여기서는 MatchSession.StateChanged를 구독해 화면을 갱신하는 일만 합니다
/// (설계문서 9-2, UI-System.md 7절).
///
/// 진입/호스트 대기/게스트 대기/실패 네 화면은 전부 전체 화면이므로 ButtonCanvas로 둔다
/// (ButtonPanel이 아니라).
///
/// 씬 배치 방법
/// ── 네 화면(entryCanvas/hostWaitingCanvas/guestWaitingCanvas/failureCanvas)은 각각
///    ButtonCanvas입니다. entryCanvas만 isMainParent=true로 두고 나머지는 false로 둡니다.
/// ── "방 만들기" 버튼: UIButton(ButtonType.HostMultiplayer, disableCanvas=entryCanvas,
///    enableCanvas=hostWaitingCanvas).
/// ── "코드로 참가" 버튼: UIButton(ButtonType.ChangeCanvas, disableCanvas=entryCanvas,
///    enableCanvas=guestWaitingCanvas) — 입력이 필요하므로 화면 전환만 하고 접속은 안 함.
/// ── guestWaitingCanvas 안 "참가" 버튼: UIButton(ButtonType.JoinMultiplayer,
///    joinCodeInput=해당 입력창).
/// ── 코드 복사 버튼: UIButton(ButtonType.CopyMultiplayerJoinCode).
/// ── 대기/실패 화면의 "취소"/"되돌아가기" 버튼: UIButton(ButtonType.CancelMultiplayerConnect,
///    disableCanvas=해당 화면, enableCanvas=entryCanvas).
/// ── matchReadyCanvas에는 합의가 끝난 뒤 갈 화면을 연결합니다(아래 Ready 설명 참고).
///
/// Ready(합의 완료)/Failed(핸드셰이크 실패)는 버튼 클릭이 아니라 세션 상태 변화로 일어나므로
/// 이 스크립트가 직접 처리합니다: Ready면 대기 화면을 닫고 다음 화면을 열며, Failed면 대기 화면을
/// 닫고 실패 화면을 엽니다.
///
/// 다음 화면 전환을 여기서 하는 이유 — 단일 플레이는 ButtonType.GameStart 버튼의 changeCanvas()가
/// 클릭 즉시 다음 화면(StartRewardCanvas)을 열지만, 멀티는 클릭 시점에 아직 상대와 합의가 안 끝나
/// 대기 화면으로 갑니다. GameCycleManager가 Ready에서 부르는 MapManager.Init()은 맵 "데이터"만
/// 다시 만들 뿐 화면을 넘기지 않으므로(맵 화면은 MainScene의 캔버스가 아니라 별도 MapScene이고,
/// 그 진입은 StartRewardCanvas의 GoScene 버튼이 맡습니다), 화면 전환은 이 스크립트가 처리합니다.
/// </summary>
public class MatchConnectUI : MonoBehaviour
{
    [Header("화면 (전부 ButtonCanvas, entryCanvas만 isMainParent=true)")]
    [SerializeField] private ButtonCanvas hostWaitingCanvas;
    [SerializeField] private ButtonCanvas guestWaitingCanvas;
    [SerializeField] private ButtonCanvas failureCanvas;

    [Tooltip("합의가 끝나면(Ready) 열 화면입니다. 단일 플레이에서 GameStart 버튼이 여는 화면(StartRewardCanvas)과 같은 자리입니다.")]
    [SerializeField] private ButtonCanvas matchReadyCanvas;

    [Header("호스트 화면")]
    [SerializeField] private TMP_Text hostJoinCodeText;
    [SerializeField] private TMP_Text hostStatusText;

    [Header("게스트 화면")]
    [SerializeField] private TMP_Text guestStatusText;

    [Header("실패 화면")]
    [SerializeField] private TMP_Text failureReasonText;

    private void OnEnable()
    {
        MatchSession session = MatchSession.EnsureInstance();
        session.StateChanged -= HandleStateChanged;
        session.StateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (MatchSession.Instance != null)
            MatchSession.Instance.StateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(MatchSessionState state)
    {
        switch (state)
        {
            case MatchSessionState.Connecting:
                SetStatus(hostStatusText, "연결하는 중...");
                SetStatus(guestStatusText, "연결하는 중...");
                break;

            case MatchSessionState.Handshaking:
                SetStatus(hostStatusText, "상대와 초기 상태를 맞추는 중...");
                SetStatus(guestStatusText, "상대와 초기 상태를 맞추는 중...");
                break;

            case MatchSessionState.Ready:
                // 맵 데이터는 GameCycleManager가 Ready에서 이미 다시 만들어 뒀습니다.
                // 여기서는 대기 화면을 닫고 단일 플레이와 같은 다음 화면으로 넘깁니다.
                hostWaitingCanvas?.DisableParent();
                guestWaitingCanvas?.DisableParent();
                matchReadyCanvas?.EnableParent();
                break;

            case MatchSessionState.Failed:
                hostWaitingCanvas?.DisableParent();
                guestWaitingCanvas?.DisableParent();

                if (failureReasonText != null)
                    failureReasonText.text = MatchSession.Instance?.FailureReason ?? "알 수 없는 오류입니다.";
                failureCanvas?.EnableParent();
                break;
        }
    }

    // 호스트 join code는 상태 변화가 아니라 값 자체가 나중에 채워지므로 이벤트가 없습니다.
    // 화면이 안 보일 때도 갱신 자체는 저렴하므로 매 프레임 확인합니다.
    private void Update()
    {
        if (hostJoinCodeText == null) return;

        string code = MatchSession.Instance?.HostJoinCode;
        hostJoinCodeText.text = string.IsNullOrEmpty(code) ? "코드 발급 중..." : code;
    }

    private static void SetStatus(TMP_Text text, string message)
    {
        if (text != null)
            text.text = message;
    }
}
