using System;
using System.Text;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>이 클라이언트가 방을 여는 쪽인지 들어가는 쪽인지입니다.</summary>
public enum MatchRole
{
    Host,
    Guest
}

/// <summary>
/// UGS Relay를 통해 실제 상대와 매치 메시지를 주고받는 전송 계층입니다.
///
/// 기물 상태를 복제하지 않고 행동(MatchMessage)만 흘리므로 NGO가 필요 없습니다.
/// Relay가 뚫어 준 경로 위에서 UnityTransport로 바이트만 주고받습니다.
///
/// 수신은 Tick()에서 드라이버를 돌려 꺼내므로 항상 메인 스레드입니다.
/// (IMatchTransport.MessageReceived의 메인 스레드 발행 계약을 이렇게 만족시킵니다.)
///
/// 연결 설정은 Configure()로 미리 받습니다. 매치 씬에 들어오기 전에
/// 호스트/게스트와 join code가 정해져 있어야 하기 때문입니다.
/// </summary>
public sealed class RelayMatchTransport : IMatchTransport
{
    // Relay는 UDP/DTLS/WSS를 지원합니다. 에디터·데스크톱 검증에는 UDP가 가장 단순합니다.
    private const RelayProtocol Protocol = RelayProtocol.UDP;

    // 커맨드 릴레이라 한 메시지가 100바이트 안쪽입니다. 넉넉히 잡아도 이 정도면 충분합니다.
    private const int MaxPayloadBytes = 1024;

    // 상대 응답이 이 시간을 넘기면 경고를 남깁니다. 끊김을 눈치채기 위한 최소한의 장치입니다.
    private const float RemoteResponseTimeoutSeconds = 60f;

    private enum ConnectionState
    {
        Idle,
        Connecting,
        Connected,
        Failed
    }

    private ConnectionState state = ConnectionState.Idle;

    private MatchRole role = MatchRole.Host;
    private string joinCode;

    private NetworkDriver driver;
    private NetworkPipeline reliablePipeline;
    private NetworkConnection connection;

    private readonly byte[] receiveBuffer = new byte[MaxPayloadBytes];

    private float remoteTurnStartedAt = -1f;
    private bool remoteTimeoutReported;

    public bool IsConnected => state == ConnectionState.Connected && connection.IsCreated;

    public event Action<MatchMessage> MessageReceived;

    /// <summary>호스트가 발급받은 join code입니다. 게스트에게 전달할 값입니다.</summary>
    public string HostJoinCode { get; private set; }

    /// <summary>join code가 발급되면 발행됩니다. 연결 UI가 화면에 띄웁니다.</summary>
    public event Action<string> JoinCodeIssued;

    /// <summary>연결에 실패하면 사유와 함께 발행됩니다.</summary>
    public event Action<string> ConnectionFailed;

    /// <summary>
    /// 매치를 열기 전에 역할과 join code를 지정합니다.
    /// 게스트는 joinCode가 반드시 있어야 하고, 호스트는 비워 둡니다.
    /// </summary>
    public void Configure(MatchRole matchRole, string code)
    {
        role = matchRole;
        joinCode = code;
    }

    public void StartMatch(PieceColor localColor)
    {
        // 연결은 비동기인데 인터페이스는 동기라, 진행 중 재진입을 여기서 막습니다.
        // RemoteTurnProvider가 IsConnected가 false인 동안 매 턴 호출할 수 있습니다.
        if (state == ConnectionState.Connecting || state == ConnectionState.Connected)
            return;

        state = ConnectionState.Connecting;
        Debug.Log($"[Relay] 연결 시작. 역할: {role}, 로컬 진영: {localColor}");

        ConnectAsync();
    }

    public void StopMatch()
    {
        MessageReceived = null;

        if (connection.IsCreated)
        {
            connection.Disconnect(driver);
            connection = default;
        }

        if (driver.IsCreated)
            driver.Dispose();

        state = ConnectionState.Idle;
        remoteTurnStartedAt = -1f;
        Debug.Log("[Relay] 매치를 종료했습니다.");
    }

    public void Send(MatchMessage message)
    {
        if (!IsConnected)
        {
            Debug.LogError($"[Relay] 연결되지 않아 전송하지 못했습니다: {message}");
            return;
        }

        byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
        if (payload.Length > MaxPayloadBytes)
        {
            Debug.LogError($"[Relay] 메시지가 너무 큽니다({payload.Length}B): {message}");
            return;
        }

        int result = driver.BeginSend(reliablePipeline, connection, out DataStreamWriter writer);
        if (result < 0)
        {
            Debug.LogError($"[Relay] 전송 시작 실패(코드 {result}): {message}");
            return;
        }

        // 길이를 앞에 붙여 두면 수신 측이 스트림 길이에 의존하지 않아도 됩니다.
        writer.WriteUShort((ushort)payload.Length);
        writer.WriteBytes(new Span<byte>(payload));

        result = driver.EndSend(writer);
        if (result < 0)
        {
            Debug.LogError($"[Relay] 전송 실패(코드 {result}): {message}");
            return;
        }

        Debug.Log($"[Relay] 송신 {message}");
    }

    public void NotifyRemoteTurnStarted(int turn)
    {
        remoteTurnStartedAt = Time.realtimeSinceStartup;
        remoteTimeoutReported = false;
    }

    public void Tick()
    {
        if (!driver.IsCreated)
            return;

        driver.ScheduleUpdate().Complete();

        if (role == MatchRole.Host)
            AcceptPendingConnection();

        PumpEvents();
        CheckRemoteTimeout();
    }

    // ── 연결 ────────────────────────────────────────────

    private async void ConnectAsync()
    {
        try
        {
            await EnsureSignedInAsync();

            RelayServerData serverData = role == MatchRole.Host
                ? await CreateHostAllocationAsync()
                : await JoinAllocationAsync();

            var settings = new NetworkSettings();
            settings.WithRelayParameters(serverData: ref serverData);

            driver = NetworkDriver.Create(settings);
            reliablePipeline = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

            if (role == MatchRole.Host)
            {
                // Relay를 쓰더라도 드라이버는 무언가에 bind된 상태여야 Listen할 수 있습니다.
                if (driver.Bind(NetworkEndpoint.AnyIpv4) < 0)
                    throw new Exception("드라이버 bind 실패");

                if (driver.Listen() < 0)
                    throw new Exception("Listen 실패");

                Debug.Log("[Relay] 게스트 접속을 기다립니다.");
            }
            else
            {
                connection = driver.Connect();
                Debug.Log("[Relay] 호스트에 접속을 시도합니다.");
            }
        }
        catch (Exception e)
        {
            Fail($"연결 실패: {e.Message}");
        }
    }

    private static async System.Threading.Tasks.Task EnsureSignedInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        // 2단계는 신원이 필요 없으므로 익명 로그인으로 충분합니다.
        // GPGS 연동은 레이팅이 붙는 5단계에서 얹습니다.
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private async System.Threading.Tasks.Task<RelayServerData> CreateHostAllocationAsync()
    {
        // 1:1 대전이므로 상대는 한 명입니다.
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);

        HostJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        Debug.Log($"[Relay] join code 발급: {HostJoinCode}");
        JoinCodeIssued?.Invoke(HostJoinCode);

        return allocation.ToRelayServerData(Protocol);
    }

    private async System.Threading.Tasks.Task<RelayServerData> JoinAllocationAsync()
    {
        if (string.IsNullOrWhiteSpace(joinCode))
            throw new Exception("join code가 비어 있습니다");

        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim());
        return allocation.ToRelayServerData(Protocol);
    }

    private void AcceptPendingConnection()
    {
        NetworkConnection accepted;
        while ((accepted = driver.Accept()) != default)
        {
            if (connection.IsCreated)
            {
                // 1:1 매치라 두 번째 접속은 받지 않습니다.
                Debug.LogWarning("[Relay] 이미 상대가 있어 추가 접속을 끊습니다.");
                accepted.Disconnect(driver);
                continue;
            }

            connection = accepted;
            state = ConnectionState.Connected;
            Debug.Log("[Relay] 상대가 접속했습니다.");
        }
    }

    // ── 수신 ────────────────────────────────────────────

    private void PumpEvents()
    {
        NetworkEvent.Type eventType;
        while ((eventType = driver.PopEvent(out NetworkConnection from, out DataStreamReader reader))
               != NetworkEvent.Type.Empty)
        {
            switch (eventType)
            {
                case NetworkEvent.Type.Connect:
                    // 게스트만 받는 이벤트입니다. 호스트는 Accept로 연결을 잡습니다.
                    connection = from;
                    state = ConnectionState.Connected;
                    Debug.Log("[Relay] 호스트에 접속했습니다.");
                    break;

                case NetworkEvent.Type.Data:
                    HandleData(reader);
                    break;

                case NetworkEvent.Type.Disconnect:
                    connection = default;
                    state = ConnectionState.Failed;
                    Debug.LogWarning("[Relay] 상대와의 연결이 끊겼습니다.");
                    ConnectionFailed?.Invoke("상대와의 연결이 끊겼습니다.");
                    break;
            }
        }
    }

    private void HandleData(DataStreamReader reader)
    {
        ushort length = reader.ReadUShort();
        if (length == 0 || length > MaxPayloadBytes)
        {
            Debug.LogError($"[Relay] 잘못된 메시지 길이({length})를 받았습니다.");
            return;
        }

        reader.ReadBytes(new Span<byte>(receiveBuffer, 0, length));
        string json = Encoding.UTF8.GetString(receiveBuffer, 0, length);

        MatchMessage message;
        try
        {
            message = JsonUtility.FromJson<MatchMessage>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] 메시지 해석 실패: {e.Message} / 원문: {json}");
            return;
        }

        if (message == null)
        {
            Debug.LogError($"[Relay] 빈 메시지를 받았습니다. 원문: {json}");
            return;
        }

        remoteTurnStartedAt = -1f;
        Debug.Log($"[Relay] 수신 {message}");

        // Tick()은 Update에서 불리므로 이미 메인 스레드입니다.
        MessageReceived?.Invoke(message);
    }

    private void CheckRemoteTimeout()
    {
        if (remoteTurnStartedAt < 0f || remoteTimeoutReported)
            return;

        if (Time.realtimeSinceStartup - remoteTurnStartedAt < RemoteResponseTimeoutSeconds)
            return;

        remoteTimeoutReported = true;
        Debug.LogWarning(
            $"[Relay] 상대 응답이 {RemoteResponseTimeoutSeconds}초를 넘겼습니다. " +
            "이탈 처리는 4단계에서 다룹니다.");
    }

    private void Fail(string reason)
    {
        state = ConnectionState.Failed;
        Debug.LogError($"[Relay] {reason}");
        ConnectionFailed?.Invoke(reason);
    }
}
