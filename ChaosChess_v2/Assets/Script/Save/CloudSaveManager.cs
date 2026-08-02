using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
#endif

/// <summary>
/// 클라우드에 올라가는 세이브 봉투(envelope).
///
/// 로컬 세이브가 run_save.json / collection_save.json 두 개로 나뉘어 있는데,
/// 이를 각각 따로 올리면 한쪽만 업로드에 성공했을 때 두 파일의 시점이 어긋난다.
/// 그래서 두 파일의 "원문 JSON 문자열"을 한 봉투에 담아 스냅샷 1개로 커밋한다.
/// (JsonUtility는 문자열 안의 JSON을 이스케이프해서 안전하게 중첩 직렬화한다.)
/// </summary>
[Serializable]
public class CloudSaveEnvelope
{
    /// <summary>이 봉투를 만든 시각(UTC, 유닉스 밀리초). 로컬/클라우드 최신 판정 기준.</summary>
    public long savedAtUnixMs;

    /// <summary>run_save.json 원문. 진행 중인 런이 없으면 빈 문자열.</summary>
    public string runJson = string.Empty;

    /// <summary>collection_save.json 원문. 발견한 카드가 없으면 빈 문자열.</summary>
    public string collectionJson = string.Empty;
}

/// <summary>
/// 구글 플레이 게임즈 Saved Games(스냅샷)로 세이브를 구글 계정에 귀속시키는 싱글톤.
///
/// 동작 흐름:
///   앱 실행 → GooglePlayAuthManager 자동 로그인 성공
///           → SyncFromCloud() : 클라우드 스냅샷을 내려받아 로컬과 시각 비교
///           → 최신본으로 로컬 덮어쓰기(또는 로컬이 최신이면 업로드)
///   이후 SaveManager.Save() / CollectionManager.Save()가 호출될 때마다
///   RequestUpload()로 디바운스 업로드.
///
/// 병합 정책은 **최신본 우선(most-recent-wins)** 이다. 두 기기에서 각각 진행한
/// 런을 합치지 않고, 나중에 저장된 쪽이 이긴다. 로그라이크 런은 부분 병합이
/// 의미가 없기 때문이다.
///
/// 안전장치:
///   - 클라우드가 비어 있으면 로컬을 절대 지우지 않고 로컬을 올린다.
///   - 런 진행 중(MainScene이 아닐 때)에는 로컬을 덮어쓰지 않는다.
///     이미 메모리에 올라간 런 상태와 파일이 어긋나면 복구가 불가능하기 때문.
///
/// 씬에 배치할 필요 없이 RuntimeInitializeOnLoadMethod로 자동 생성된다.
/// </summary>
public class CloudSaveManager : MonoBehaviour
{
    public static CloudSaveManager Instance { get; private set; }

    /// <summary>
    /// 스냅샷 파일명. 허용 문자는 a-z A-Z 0-9 와 "-", ".", "_", "~" 뿐이며 1~100자여야 한다
    /// (ISavedGameClient 규격). 기기와 무관한 전역 식별자라 계정당 하나로 고정한다.
    /// </summary>
    private const string SnapshotFileName = "chaoschess_save";

    /// <summary>연속 저장이 몰릴 때 업로드를 합치는 대기 시간. 클라우드 쓰기는 비싸다.</summary>
    private const float UploadDebounceSeconds = 2f;

    /// <summary>클라우드 데이터를 로컬에 적용한 직후 발생. 이어하기 버튼 등 UI 갱신용.</summary>
    public event Action OnCloudDataApplied;

    /// <summary>다운로드/업로드가 진행 중인지. UI에서 "동기화 중" 표시에 쓴다.</summary>
    public bool IsSyncing { get; private set; }

    private Coroutine _pendingUpload;

    /// <summary>로그인 전에 저장이 발생했을 때, 로그인 성공 후 한 번 올려주기 위한 플래그.</summary>
    private bool _uploadRequestedWhileSignedOut;

    private static readonly DateTime UnixEpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject go = new GameObject(nameof(CloudSaveManager));
        go.AddComponent<CloudSaveManager>();
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

        // GooglePlayAuthManager도 BeforeSceneLoad로 자동 생성되는데, 같은 타이밍의
        // RuntimeInitializeOnLoadMethod끼리는 실행 순서가 보장되지 않는다.
        // 아직 없으면 여기서 먼저 만들어 구독 대상을 확보한다.
        GooglePlayAuthManager auth = GooglePlayAuthManager.EnsureInstance();
        if (auth == null) return;

        auth.OnAuthenticationChanged += HandleAuthenticationChanged;

        // 이벤트 구독만으로는 부족하다. 자동 로그인이 이 시점보다 먼저 끝났다면
        // 이벤트를 이미 놓친 상태이므로, 현재 상태를 직접 한 번 읽어 처리한다.
        if (auth.IsAuthenticated) HandleAuthenticationChanged(true);
    }

    private void OnDestroy()
    {
        if (GooglePlayAuthManager.Instance != null)
            GooglePlayAuthManager.Instance.OnAuthenticationChanged -= HandleAuthenticationChanged;

        if (Instance == this) Instance = null;
    }

    private void HandleAuthenticationChanged(bool authenticated)
    {
        if (!authenticated) return;

        // 로그인 전에 쌓인 저장이 있으면 내려받기 대신 곧바로 올린다.
        // (로그인 전 플레이 = 이 기기가 최신이라는 뜻)
        if (_uploadRequestedWhileSignedOut)
        {
            _uploadRequestedWhileSignedOut = false;
            RequestUpload();
            return;
        }

        SyncFromCloud();
    }

    // ── 다운로드 ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 클라우드 스냅샷을 내려받아 로컬보다 최신이면 로컬에 적용한다.
    /// 로그인 직후 자동 호출되며, 설정 화면 등에서 수동 동기화 버튼에 연결해도 된다.
    /// </summary>
    public void SyncFromCloud()
    {
        if (IsSyncing) return;
        if (!IsSignedIn())
        {
            Debug.Log("[CloudSave] 미로그인 상태라 동기화를 건너뜁니다.");
            return;
        }

        IsSyncing = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        // ReadNetworkOnly: 실행 직후 1회뿐인 조회이므로, 캐시된 낡은 값 대신
        // 반드시 서버의 현재 값을 본다. (오프라인이면 실패 → 로컬 유지)
        PlayGamesPlatform.Instance.SavedGame.OpenWithAutomaticConflictResolution(
            SnapshotFileName,
            DataSource.ReadNetworkOnly,
            ConflictResolutionStrategy.UseMostRecentlySaved,
            OnOpenedForRead);
#else
        IsSyncing = false;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void OnOpenedForRead(SavedGameRequestStatus status, ISavedGameMetadata metadata)
    {
        if (status != SavedGameRequestStatus.Success || metadata == null)
        {
            IsSyncing = false;
            Debug.LogWarning($"[CloudSave] 스냅샷 열기 실패: {status}. 로컬 세이브를 그대로 사용합니다.");
            return;
        }

        PlayGamesPlatform.Instance.SavedGame.ReadBinaryData(metadata, OnReadBinaryData);
    }

    private void OnReadBinaryData(SavedGameRequestStatus status, byte[] data)
    {
        IsSyncing = false;

        if (status != SavedGameRequestStatus.Success)
        {
            Debug.LogWarning($"[CloudSave] 스냅샷 읽기 실패: {status}. 로컬 세이브를 그대로 사용합니다.");
            return;
        }

        // 길이 0 = 이 계정에 아직 세이브가 없음(최초 실행).
        // 클라우드를 로컬에 적용하면 안 되고, 반대로 로컬을 올려야 한다.
        if (data == null || data.Length == 0)
        {
            Debug.Log("[CloudSave] 클라우드에 세이브가 없습니다. 로컬 데이터를 업로드합니다.");
            RequestUpload();
            return;
        }

        CloudSaveEnvelope cloud = DeserializeEnvelope(data);
        if (cloud == null)
        {
            Debug.LogWarning("[CloudSave] 스냅샷을 해석하지 못했습니다. 로컬 세이브를 그대로 사용합니다.");
            return;
        }

        ResolveAndApply(cloud);
    }
#endif

    /// <summary>클라우드 봉투와 로컬 파일의 시각을 비교해 최신본을 확정한다.</summary>
    private void ResolveAndApply(CloudSaveEnvelope cloud)
    {
        long localTime = GetLocalTimestampUnixMs();

        if (cloud.savedAtUnixMs <= localTime)
        {
            Debug.Log($"[CloudSave] 로컬이 최신입니다 (로컬 {localTime} ≥ 클라우드 {cloud.savedAtUnixMs}). 업로드합니다.");
            RequestUpload();
            return;
        }

        // 런 진행 중에 파일을 갈아끼우면 메모리 상태와 어긋나므로 다음 실행으로 미룬다.
        if (!IsSafeToOverwriteLocal())
        {
            Debug.LogWarning("[CloudSave] 런 진행 중이라 클라우드 데이터 적용을 보류합니다. 다음 실행 시 적용됩니다.");
            return;
        }

        ApplyEnvelopeToLocal(cloud);
        Debug.Log($"[CloudSave] 클라우드 데이터를 적용했습니다 (클라우드 {cloud.savedAtUnixMs} > 로컬 {localTime}).");
        OnCloudDataApplied?.Invoke();
    }

    /// <summary>
    /// 런이 메모리에 올라가 있어 로컬 파일을 덮어쓰면 안 되는 씬 목록.
    ///
    /// "MainScene일 때만 안전" 같은 화이트리스트로 짜면, 나중에 타이틀 앞에 다른 씬
    /// (예: LoginScene)을 추가하는 순간 동기화가 조용히 멈춘다. 그래서 위험한 쪽을 나열한다.
    /// </summary>
    private static readonly string[] InRunSceneNames =
    {
        "MapScene", "MainGameScene", "RewardScene", "ResultScene"
    };

    /// <summary>로컬 파일을 덮어써도 안전한 시점인지 판정한다.</summary>
    private bool IsSafeToOverwriteLocal()
    {
        string active = SceneManager.GetActiveScene().name;

        foreach (string name in InRunSceneNames)
        {
            if (active == name) return false;
        }

        return true;
    }

    /// <summary>봉투 내용을 로컬 세이브 파일에 기록하고, 메모리에 올라간 컬렉션도 다시 읽는다.</summary>
    private void ApplyEnvelopeToLocal(CloudSaveEnvelope envelope)
    {
        try
        {
            string runPath = SaveManager.RunSavePath;
            if (string.IsNullOrEmpty(envelope.runJson))
            {
                // 클라우드 기준으로 진행 중인 런이 없음 → 로컬 런도 정리해 상태를 일치시킨다.
                // 복구 후보(.tmp/.bak)까지 지워야 이어하기 버튼이 되살아나지 않는다.
                SafeFile.Delete(runPath);
            }
            else
            {
                SafeFile.WriteAtomic(runPath, envelope.runJson);
            }

            if (!string.IsNullOrEmpty(envelope.collectionJson))
            {
                SafeFile.WriteAtomic(CollectionManager.CollectionSavePath, envelope.collectionJson);
                // 컬렉션은 Awake에서 이미 로드된 뒤라, 파일만 바꾸면 메모리에 반영되지 않는다.
                CollectionManager.Instance?.ReloadFromDisk();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CloudSave] 로컬 적용 중 오류: {e.Message}");
        }
    }

    // ── 업로드 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 로컬 세이브를 클라우드에 올린다. 짧은 시간에 여러 번 불려도
    /// UploadDebounceSeconds 동안 모아 한 번만 커밋한다.
    /// </summary>
    public void RequestUpload()
    {
        if (!IsSignedIn())
        {
            // 로그인 후에 만회할 수 있도록 기록만 남긴다.
            _uploadRequestedWhileSignedOut = true;
            return;
        }

        if (_pendingUpload != null) StopCoroutine(_pendingUpload);
        _pendingUpload = StartCoroutine(UploadAfterDelay());
    }

    private IEnumerator UploadAfterDelay()
    {
        yield return new WaitForSecondsRealtime(UploadDebounceSeconds);
        _pendingUpload = null;
        UploadNow();
    }

    /// <summary>디바운스 없이 즉시 업로드한다.</summary>
    public void UploadNow()
    {
        if (!IsSignedIn()) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        IsSyncing = true;

        // 쓰기 직전 조회는 캐시를 허용한다. 어차피 커밋 시점에 충돌이 감지되고,
        // 그 충돌은 다음 Open에서 UseMostRecentlySaved로 해소된다.
        PlayGamesPlatform.Instance.SavedGame.OpenWithAutomaticConflictResolution(
            SnapshotFileName,
            DataSource.ReadCacheOrNetwork,
            ConflictResolutionStrategy.UseMostRecentlySaved,
            OnOpenedForWrite);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void OnOpenedForWrite(SavedGameRequestStatus status, ISavedGameMetadata metadata)
    {
        if (status != SavedGameRequestStatus.Success || metadata == null)
        {
            IsSyncing = false;
            Debug.LogWarning($"[CloudSave] 업로드용 스냅샷 열기 실패: {status}");
            return;
        }

        CloudSaveEnvelope envelope = BuildEnvelopeFromLocal();
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope));

        SavedGameMetadataUpdate update = new SavedGameMetadataUpdate.Builder()
            .WithUpdatedDescription(BuildDescription())
            .Build();

        PlayGamesPlatform.Instance.SavedGame.CommitUpdate(metadata, update, bytes, OnCommitCompleted);
    }

    private void OnCommitCompleted(SavedGameRequestStatus status, ISavedGameMetadata metadata)
    {
        IsSyncing = false;

        if (status == SavedGameRequestStatus.Success)
            Debug.Log("[CloudSave] 업로드 완료");
        else
            Debug.LogWarning($"[CloudSave] 업로드 실패: {status}");
    }
#endif

    /// <summary>Play 게임즈 앱의 세이브 목록에 보이는 설명 문구를 만든다.</summary>
    private string BuildDescription()
    {
        PlayerState ps = PlayerState.Instance;
        if (ps == null) return "카오스 체스";

        return $"{ps.WinCount}승 {ps.DrawCount}무 {ps.LoseCount}패";
    }

    // ── 로컬 파일 입출력 ────────────────────────────────────────────────────────

    private CloudSaveEnvelope BuildEnvelopeFromLocal()
    {
        return new CloudSaveEnvelope
        {
            savedAtUnixMs = ToUnixMs(DateTime.UtcNow),
            runJson = ReadFileOrEmpty(SaveManager.RunSavePath),
            collectionJson = ReadFileOrEmpty(CollectionManager.CollectionSavePath)
        };
    }

    /// <summary>
    /// 업로드 대상 파일을 읽는다. 본 파일이 손상됐으면 SafeFile이 복구 후보로 폴백하므로,
    /// 손상된 세이브를 그대로 클라우드에 밀어 올리는 사고를 막는다.
    /// </summary>
    private static string ReadFileOrEmpty(string path)
    {
        return SafeFile.TryRead(path, null, out string contents) ? contents : string.Empty;
    }

    /// <summary>
    /// 로컬 세이브의 최종 수정 시각(둘 중 더 최신). 파일이 하나도 없으면 0.
    ///
    /// 파일 mtime을 쓰는 이유: 두 세이브 데이터 클래스에 타임스탬프 필드를 새로 넣으면
    /// 기존 저장 파일과의 호환을 따로 처리해야 한다. 클라우드에서 내려받아 로컬에 쓰면
    /// mtime이 "지금"으로 갱신되므로, 적용 직후 로컬이 최신으로 판정돼 일관성도 맞다.
    /// </summary>
    private long GetLocalTimestampUnixMs()
    {
        long latest = 0;

        foreach (string path in new[] { SaveManager.RunSavePath, CollectionManager.CollectionSavePath })
        {
            // 복구 후보(.tmp/.bak)까지 포함해 최신 시각을 본다. 본 파일이 손상돼 백업으로
            // 폴백하는 상황에서도 "로컬에 최근 진행분이 있다"는 사실은 유지돼야 한다.
            DateTime utc = SafeFile.GetLastWriteUtc(path);
            if (utc == DateTime.MinValue) continue;

            long t = ToUnixMs(utc);
            if (t > latest) latest = t;
        }

        return latest;
    }

    private static long ToUnixMs(DateTime utc) => (long)(utc - UnixEpochUtc).TotalMilliseconds;

    private static CloudSaveEnvelope DeserializeEnvelope(byte[] data)
    {
        try
        {
            string json = System.Text.Encoding.UTF8.GetString(data);
            return JsonUtility.FromJson<CloudSaveEnvelope>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CloudSave] 봉투 역직렬화 실패: {e.Message}");
            return null;
        }
    }

    private bool IsSignedIn()
    {
        return GooglePlayAuthManager.Instance != null && GooglePlayAuthManager.Instance.IsAuthenticated;
    }
}
