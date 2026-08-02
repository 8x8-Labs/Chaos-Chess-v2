# 구글 플레이 게임즈(GPGS) 연동 가이드

Unity 프로젝트에 **Google Play Games Services v2** 로그인을 처음부터 붙이는 전체 절차를 정리한 문서입니다.

- 대상 플러그인: **Google Play Games Plugin for Unity 2.1.0** (+ External Dependency Manager)
- 전제: **Google Cloud / Play Console 프로젝트가 이미 존재**하는 상태
- 결과물: 앱 실행 시 자동 로그인 → 실패 시 버튼으로 재시도 가능한 상태

> 이 문서는 "처음부터 다시 한다면" 기준의 순서입니다. 현재 저장소 상태와 무관하게 그대로 따라갈 수 있습니다.

---

## 0. 먼저 용어부터 (여기서 대부분 막힙니다)

이름이 비슷한 ID가 4개 등장하고, **서로 다른 값**입니다.

| 이름 | 예시 | 정체 |
| --- | --- | --- |
| Google Cloud **프로젝트 ID** | `chaos-chess-471203` | 문자 포함 문자열. **여기서 안 씁니다** |
| Google Cloud **프로젝트 번호** | `195283170109` | 숫자. **이게 App ID** |
| Play Games **App ID** | `195283170109` | 위 프로젝트 번호와 **같은 값** |
| **OAuth 클라이언트 ID** | `195283170109-abc….apps.googleusercontent.com` | 하이픈 앞 숫자 = App ID |

**검증법**: 고른 App ID가 OAuth 클라이언트 ID의 하이픈 앞부분과 글자 하나까지 같아야 합니다. 플러그인이 실제로 이 규칙으로 검사합니다(`GPGSAndroidSetupUI.cs`의 `webClientId.Split('-')[0]` 비교).

App ID 형식 제약 (`GPGSUtil.LooksLikeValidAppId`):

- **숫자만** 포함 — 하이픈·문자가 하나라도 있으면 거부
- **5자리 이상**

---

## 1. Play Console에 앱 등록

Play Games 서비스는 **Play Console에 등록된 앱**에만 연결할 수 있습니다.

1. Play Console에서 앱 생성
2. **패키지명(applicationId)을 확정** — 이후 변경 불가하며, 이 값이 OAuth 자격증명에 그대로 묶입니다
3. 내부 테스트 트랙에 AAB를 1회 업로드 (Play 앱 서명 키를 발급받기 위함, 3단계에서 필요)

> 이 프로젝트의 패키지명은 `com.esestudio.chaoschess` 입니다 (`ProjectSettings.asset`의 `applicationIdentifier.Android`).

---

## 2. 서명 키 준비와 SHA-1 추출

GPGS 인증은 **패키지명 + 서명 키 SHA-1** 조합으로 앱을 식별합니다. 이게 안 맞으면 코드가 완벽해도 로그인이 실패합니다.

### 2-1. 어떤 SHA-1이 필요한가

| 상황 | 필요한 SHA-1 |
| --- | --- |
| Play 스토어로 배포한 빌드 | **Play 앱 서명 키**의 SHA-1 |
| 로컬에서 서명해 직접 설치한 APK | **업로드(로컬) 키스토어**의 SHA-1 |

AAB로 업로드하면 Google이 **Play 앱 서명**으로 키를 재발급하므로, 배포본에서 실제로 검증되는 건 로컬 키가 아닙니다. **두 환경 모두 테스트하려면 자격증명을 2개 등록**하세요.

- **Play 앱 서명 키 SHA-1**: Play Console ▸ 테스트 및 출시 ▸ **앱 무결성** ▸ 앱 서명 키 인증서
- **로컬 키스토어 SHA-1**: 아래 명령으로 추출

### 2-2. 로컬 키스토어에서 SHA-1 뽑기

Unity에 동봉된 OpenJDK의 `keytool`을 씁니다. PowerShell에서 **한 줄씩** 실행하세요.

```powershell
$kt = "C:\Program Files\Unity\Hub\Editor\<UNITY_VERSION>\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe"
$ks = "$env:USERPROFILE\user.keystore"
& $kt -list -v -keystore $ks -alias <ALIAS_NAME>
```

비밀번호 프롬프트가 뜨면 입력 → 출력에서 `SHA1:` 줄을 복사합니다.

> **주의**
> - 명령을 한 줄로 길게 붙여넣으면 터미널에서 잘려 `-keystore` 인자가 유실되고, `~\.keystore가 존재하지 않음` 오류가 납니다. 변수로 쪼개서 실행하세요.
> - `-storepass`로 비밀번호를 인라인 전달하면 PowerShell 히스토리 파일에 평문으로 남습니다.
> - Unity의 `{dedicated}` 키스토어는 기본적으로 `%USERPROFILE%\user.keystore`에 있습니다.

---

## 3. Play Games 서비스 구성 + 자격증명 등록

Play Console 좌측 **Play Games 서비스 ▸ 설정 및 관리 ▸ 구성**

1. **새 게임 만들기** → 이름·카테고리 입력
2. 생성된 게임을 1단계의 Play Console 앱에 **연결**
3. **사용자 인증 정보 추가**

| 필드 | 값 |
| --- | --- |
| 이름 | 임의 (예: `Android release`) |
| 유형 | **Android** |
| 승인 | 연결된 앱 선택 |
| OAuth 클라이언트 | **OAuth 클라이언트 만들기** → 패키지명 + 2단계의 SHA-1 |

4. OAuth 클라이언트를 만들면 Google Cloud Console로 이동했다가 돌아옵니다. **새로고침 후 드롭다운에서 방금 만든 클라이언트를 선택**해야 저장됩니다
5. 저장 후 반드시 상단 **변경사항 게시** 클릭

> 게시하지 않으면 설정이 반영되지 않습니다. "저장했는데 왜 안 되지"의 대부분이 이것입니다.

**(선택) Web 자격증명**: 서버 인증·ID 토큰이 필요하면 유형 **웹**으로 하나 더 만들어 Web client ID를 확보합니다. 플러그인에서 이 값은 서버 토큰 요청(`AndroidClient.RequestServerSideAccess` 계열)에만 쓰이므로, 닉네임·플레이어 ID만 쓸 거면 **불필요합니다**.

---

## 4. 테스터 등록 (건너뛰면 100% 실패)

**Play Games 서비스 ▸ 설정 및 관리 ▸ 테스터**에 로그인할 Google 계정을 추가합니다.

게임 서비스가 정식 게시되기 전까지는 **테스터 목록에 없는 계정은 무조건 로그인 실패**합니다(`SignInStatus.InternalError`). 코드를 의심하기 전에 여기를 먼저 확인하세요.

---

## 5. Unity 플러그인 설치

1. [google-play-games-plugin-for-unity](https://github.com/playgameservices/play-games-plugin-for-unity) 릴리스에서 `.unitypackage` 다운로드
2. Unity에서 임포트 — `Assets/GooglePlayGames/`와 `Assets/ExternalDependencyManager/`가 생성됩니다
3. **Assets ▸ External Dependency Manager ▸ Android Resolver ▸ Force Resolve** 실행

Resolve가 성공하면 다음이 자동 생성/수정됩니다.

| 파일 | 내용 |
| --- | --- |
| `Assets/Plugins/Android/mainTemplate.gradle` | `com.google.games:gpgs-plugin-support` 의존성 주입 |
| `Assets/Plugins/Android/settingsTemplate.gradle` | `Assets/GeneratedLocalRepo`를 maven 저장소로 등록 |
| `Assets/Plugins/Android/gradleTemplate.properties` | `android.useAndroidX=true`, `android.enableJetifier=true` |
| `Assets/GeneratedLocalRepo/` | gpgs aar이 담긴 로컬 m2 저장소 |

---

## 6. Player Settings 구성

**Edit ▸ Project Settings ▸ Player ▸ Android**

| 항목 | 값 | 비고 |
| --- | --- | --- |
| Package Name | Play Console과 **동일** | 다르면 인증 실패 |
| Minimum API Level | 23 이상 | |
| Target API Level | Play 정책이 요구하는 최신 | |
| Target Architectures | **ARM64 포함** | Play 스토어 필수 |
| Custom Main Gradle Template | ✅ | |
| Custom Gradle Properties Template | ✅ | AndroidX 설정 반영에 필요 |
| Custom Gradle Settings Template | ✅ | 로컬 m2 저장소 참조에 필요 |
| Keystore / Key alias | 릴리즈 키 지정 | 2단계에서 SHA-1 뽑은 그 키 |

> 커스텀 템플릿 3종을 켜지 않으면 Resolver가 써둔 gradle 설정이 빌드에 반영되지 않습니다.

---

## 7. GPGS Setup 실행

**Window ▸ Google Play Games ▸ Setup ▸ Android setup**

| 필드 | 값 |
| --- | --- |
| Directory to save constants | `Assets` |
| Constants class name | `GPGSIds` |
| **Resources Definition** | 리소스 XML (아래) |
| Client ID (web) | **비워두기** (서버 인증 안 쓸 경우) |

### 리소스 XML 얻기

**정석**: Play Games 서비스 ▸ 설정 및 관리 ▸ **업적** ▸ 우측 상단 **리소스 가져오기**

버튼이 안 보이면 구성이 아직 게시되지 않았거나 업적이 0개인 경우입니다. **직접 작성해도 완전히 동일하게 동작합니다** — 플러그인의 `ParseResources()`는 `<resources>` 안의 `<string>`만 훑어서 `app_id`가 있으면 성공으로 처리하기 때문입니다.

```xml
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <string name="app_id">195283170109</string>
    <string name="package_name">com.esestudio.chaoschess</string>
</resources>
```

- `app_id` — **필수**. 이 키가 없으면 Setup이 실패합니다
- `package_name` — 번들 ID 설정용
- 그 외 키(업적·리더보드 ID) — `GPGSIds.cs` 상수로 생성됩니다. 없어도 무방

> **Client ID를 넣을 때 주의**: 값이 `.googleusercontent.com`으로 끝나지 않거나, 하이픈 앞 숫자가 `app_id`와 다르면 **Setup 전체가 실패**합니다. 필요 없으면 비워두는 게 안전합니다.

---

## 8. 결과 검증

Setup이 성공하면 아래 4곳이 바뀝니다. **직접 열어서 확인하세요.**

### `Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml`

```xml
<meta-data android:name="com.google.android.gms.games.APP_ID"
    android:value="\u003195283170109" />
```

`\u003` 접두사는 **오타가 아니라 플러그인 템플릿 규격**입니다. `\u0031`이 유니코드 이스케이프로 해석되어 문자 `"1"`이 되고 뒤의 `95283170109`가 이어붙어 최종 `"195283170109"`가 됩니다.

> **왜 이런 짓을 하나**: `android:value`에 숫자만 쓰면 Android가 **정수로 타입 추론**해버려서, 문자열을 기대하는 Games SDK가 APP_ID를 읽지 못합니다. 첫 글자를 이스케이프해 문자열임을 강제하는 트릭입니다. **숫자를 그대로 넣으면 안 됩니다.**

바로 아랫줄의 `unityVersion` 값이 `\u0032.1.0`(= `"2.1.0"`)인 것도 같은 원리입니다.

### `Assets/GooglePlayGames/com.google.play.games/Runtime/Scripts/GameInfo.cs`

```csharp
public const string ApplicationId = "195283170109";
```

### `ProjectSettings/GooglePlayGameSettings.txt`

```
proj.AppId=195283170109
and.BundleId=com.esestudio.chaoschess
android.SetupDone=true
```

`android.SetupDone`이 `true`가 아니면 빌드 직후 *"Google Play Games not configured!"* 경고 팝업이 뜹니다(`GPGSPostBuild.cs`).

### `Assets/GPGSIds.cs`

업적·리더보드를 등록한 경우에만 생성됩니다. 없어도 로그인 자체에는 지장 없습니다.

---

## 9. 로그인 코드

`RuntimeInitializeOnLoadMethod`로 씬 배치 없이 자동 생성되는 싱글톤 형태를 권장합니다.

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void Bootstrap()
{
    if (Instance != null) return;
    new GameObject(nameof(GooglePlayAuthManager)).AddComponent<GooglePlayAuthManager>();
}

private void TryAutoSignIn()
{
#if UNITY_ANDROID && !UNITY_EDITOR
    PlayGamesPlatform.Activate();
    PlayGamesPlatform.Instance.Authenticate(ProcessAuthentication);
#endif
}

public void SignInManually()   // 자동 로그인 실패 시 버튼으로 재시도
{
#if UNITY_ANDROID && !UNITY_EDITOR
    PlayGamesPlatform.Instance.ManuallyAuthenticate(ProcessAuthentication);
#endif
}
```

**설계 시 주의점 3가지**

1. **v2에서는 앱 시작 시 SDK가 스스로 사인인을 시도**합니다. 게임 코드는 `Authenticate()`로 그 결과를 받아오기만 하면 됩니다. 실패했을 때만 `ManuallyAuthenticate()`로 재시도하세요.
2. **UI는 이벤트 구독만으로 부족합니다.** 자동 로그인이 UI 활성화보다 먼저 끝나면 이벤트를 놓칩니다. `OnEnable()`에서 현재 로그인 상태를 **직접 한 번 읽어** 갱신해야 합니다.
3. **응답 대기 상태를 노출하세요.** 대기 중 버튼 연타를 막지 않으면 `ManuallyAuthenticate`가 중복 호출되어 콜백이 겹칩니다.

이 저장소의 실제 구현은 `Assets/Script/GooglePlayAuthManager.cs`와 `Assets/Script/UI/GooglePlayLoginUI.cs`를 참고하세요.

---

## 10. 빌드 & 테스트

**에디터에서는 절대 동작하지 않습니다.** 코드가 `#if UNITY_ANDROID && !UNITY_EDITOR`로 막혀 있고, 네이티브 SDK도 없습니다.

1. **릴리즈 키스토어로 서명**해 빌드 (디버그 키로 빌드하면 SHA-1이 안 맞아 무조건 실패)
2. 실기기에 설치
3. 로그 확인

```bash
adb logcat -s Unity:V | findstr GPGS
```

로그인 성공 로그가 찍히면 완료입니다.

---

## 11. 자주 겪는 실패 원인

| 증상 | 원인 | 해결 |
| --- | --- | --- |
| `SignInStatus.InternalError` | 테스터 미등록 | 4단계 |
| 〃 | 자격증명 게시 안 함 | 3단계 5번 |
| 〃 | SHA-1 불일치 (디버그 키 빌드 / Play 앱 서명 키 미등록) | 2단계 |
| 앱 시작 즉시 GPGS 초기화 실패 | 매니페스트 APP_ID 비어 있음 | 7~8단계 |
| 〃 | APP_ID를 `\u003` 없이 숫자로만 기입 | 8단계 |
| Setup 버튼을 눌러도 반응 없음 | Resources Definition에 `app_id` 없음 | 7단계 |
| 〃 | Web client ID 형식/앞자리 불일치 | 7단계 |
| 빌드 후 "not configured" 팝업 | `android.SetupDone` 미설정 | 8단계 |
| gradle 설정이 빌드에 반영 안 됨 | 커스텀 템플릿 체크박스 누락 | 6단계 |
| 에디터에서 로그인 버튼 무반응 | **정상 동작** | 10단계 |

---

## 12. 클라우드 세이브 (Saved Games)

로그인이 붙으면 세이브를 **구글 계정에 귀속**시킬 수 있습니다. 기기를 바꾸거나 앱을 재설치해도 데이터가 따라옵니다.

### 12-1. 사전 작업 (안 하면 무조건 실패)

**Play Console ▸ Play Games 서비스 ▸ 설정 및 관리 ▸ 구성**에서 **저장된 게임(Saved Games)** 사용 설정 → **변경사항 게시**.

켜지 않으면 스냅샷 호출이 전부 `SavedGameRequestStatus.InternalError`로 떨어집니다. 3단계의 자격증명 게시와 마찬가지로 **게시까지 해야** 반영됩니다.

### 12-2. 구조

| 파일 | 역할 |
| --- | --- |
| `Assets/Script/Save/CloudSaveManager.cs` | 스냅샷 동기화 전담 싱글톤. `RuntimeInitializeOnLoadMethod`로 자동 생성 |
| `SaveManager.cs` | `RunSavePath` static 공개, `Save()`/`DeleteSave()`에서 업로드 요청 |
| `CollectionManager.cs` | `CollectionSavePath` static 공개, `ReloadFromDisk()` 추가 |
| `GooglePlayAuthManager.cs` | `EnsureInstance()` 추가 |
| `ContinueButtonController.cs` | 동기화 완료 이벤트 구독 |

### 12-3. 동작 흐름

```
앱 실행
  → GooglePlayAuthManager 자동 로그인 성공
  → CloudSaveManager.SyncFromCloud()
  → OpenWithAutomaticConflictResolution(ReadNetworkOnly, UseMostRecentlySaved)
  → ReadBinaryData()
  → 클라우드 봉투의 savedAtUnixMs vs 로컬 파일 mtime 비교
  → 클라우드가 최신이면 로컬 덮어쓰기 / 로컬이 최신이면 업로드
```

이후 `SaveManager.Save()`, `SaveManager.DeleteSave()`, `CollectionManager.Save()`가 호출될 때마다 **2초 디바운스** 업로드가 걸립니다. 공식 문서가 "클라우드 쓰기는 비싸니 자주 하지 말라"고 명시하고 있어, 연속 저장을 한 번으로 합칩니다.

### 12-4. 왜 두 세이브를 한 봉투에 묶었나

로컬 세이브는 `run_save.json`(런 진행)과 `collection_save.json`(카드 도감) 두 개입니다. 이를 **각각 별도 스냅샷으로 올리면 한쪽만 업로드에 성공했을 때 두 파일의 시점이 어긋납니다.**

그래서 두 파일의 원문 JSON을 `CloudSaveEnvelope` 하나에 담아 **스냅샷 1개**로 커밋합니다. `JsonUtility`는 문자열 안의 JSON을 이스케이프해서 안전하게 중첩 직렬화합니다.

```csharp
[Serializable]
public class CloudSaveEnvelope
{
    public long savedAtUnixMs;      // 최신 판정 기준
    public string runJson;          // run_save.json 원문
    public string collectionJson;   // collection_save.json 원문
}
```

### 12-5. 병합 정책과 안전장치

병합은 **최신본 우선(most-recent-wins)** 입니다. 두 기기에서 각각 진행한 런을 합치지 않고 나중에 저장된 쪽이 이깁니다. 로그라이크 런은 부분 병합이 의미가 없기 때문입니다.

안전장치 3가지:

1. **클라우드가 비어 있으면 로컬을 절대 지우지 않고** 로컬을 올립니다. 최초 실행 시 데이터가 날아가는 걸 막습니다.
2. **런 진행 중에는 로컬을 덮어쓰지 않습니다.** 이미 메모리에 올라간 런 상태와 파일이 어긋나면 복구가 불가능합니다. 판정은 위험한 씬을 나열하는 블랙리스트(`MapScene`/`MainGameScene`/`RewardScene`/`ResultScene`)로 합니다 — "MainScene일 때만 안전" 같은 화이트리스트로 짜면 나중에 타이틀 앞에 씬을 추가하는 순간 동기화가 조용히 멈춥니다.
3. **`DeleteSave()`도 업로드합니다.** 런 종료 시 로컬만 지우면 다음 실행 때 클라우드에 남은 "끝난 런"을 도로 내려받습니다.

### 12-6. 설계 시 걸렸던 지점

- **부트스트랩 순서** — `GooglePlayAuthManager`와 `CloudSaveManager` 둘 다 `BeforeSceneLoad`인데, 같은 타이밍의 `RuntimeInitializeOnLoadMethod`끼리는 **실행 순서가 보장되지 않습니다.** 그래서 `EnsureInstance()`로 먼저 깨어난 쪽이 상대를 생성해 순서를 확정합니다.
- **이벤트만 구독하면 놓칩니다** — 9단계의 주의점 2번과 같은 함정입니다. 자동 로그인이 구독보다 먼저 끝났을 수 있으므로 `Awake()`에서 `IsAuthenticated`를 직접 한 번 읽습니다.
- **컬렉션은 파일만 바꿔선 반영 안 됨** — `CollectionManager`는 `Awake()`에서 이미 메모리에 로드한 뒤입니다. 그래서 `ReloadFromDisk()`가 필요합니다.
- **이어하기 버튼 갱신** — 동기화는 네트워크 왕복이라 `Start()`보다 늦게 끝납니다. 1회 판정으로는 부족해 `OnCloudDataApplied` 이벤트로 다시 갱신합니다.

### 12-7. 알려진 한계

| 한계 | 내용 |
| --- | --- |
| 런 병합 불가 | 두 기기에서 각각 진행한 런은 한쪽이 사라집니다 |
| 기기 시계 의존 | 최신 판정이 `DateTime.UtcNow`와 파일 mtime 기반이라, 시계가 크게 어긋난 기기끼리는 순서가 뒤집힐 수 있습니다 |
| 오프라인 | 다운로드가 `ReadNetworkOnly`라 오프라인이면 실패하고 로컬을 그대로 씁니다 (의도된 동작) |

### 12-8. 추가 실패 원인

| 증상 | 원인 | 해결 |
| --- | --- | --- |
| 스냅샷 호출이 전부 `InternalError` | Play Console에서 저장된 게임 미설정/미게시 | 12-1 |
| 로그인은 되는데 동기화 로그가 없음 | 미로그인 상태로 판정됨 | `[CloudSave] 미로그인 상태라...` 로그 확인 |
| 클라우드 데이터가 적용 안 됨 | 런 진행 중이라 보류됨 | 정상 동작. 다음 실행 시 적용 |

---

## 13. 멀티플레이 · 프로필로 이어지는 것

클라우드 세이브를 붙였다고 멀티플레이가 가까워지지는 않습니다. 오해하기 쉬운 지점이라 정리해둡니다.

### 도움이 되지 않는 것

- **스냅샷은 내 서버가 읽을 수 없습니다.** 구글이 호스팅하는 계정별 blob이고 클라이언트만 접근 가능합니다. 멀티플레이에 필요한 "양쪽이 함께 보는 권위 있는 서버 상태"의 역할을 못 합니다.
- **GPGS의 실시간/턴제 멀티플레이 API는 폐지됐습니다.** 지금 Play Games 서비스로는 매치메이킹 자체가 불가능합니다. Photon · Unity Netcode · Mirror · 자체 서버 중에서 골라야 합니다.

### 이미 되는 것 (프로필)

`GooglePlayAuthManager`가 `GetUserDisplayName()` / `GetUserId()`로 닉네임과 고유 ID를 이미 받고 있습니다. 여기에 업적·리더보드를 붙이면 프로필 화면이 됩니다.

### 진짜로 이어지는 자산 (인증 레이어)

멀티플레이가 로드맵에 있다면 지금 할 수 있는 실질적 사전작업은 클라우드 세이브가 아니라 이쪽입니다.

- 플러그인에 `PlayGamesPlatform.Instance.RequestServerSideAccess(bool, Action<string>)`가 있습니다. 서버 인증 코드를 받아 내 백엔드가 구글에 검증을 요청하는 용도로, 멀티플레이 계정 시스템의 출발점입니다.
- 쓰려면 **Web 자격증명(Web client ID)** 이 필요합니다. 3단계에서 "닉네임·플레이어 ID만 쓸 거면 불필요"라고 적고 건너뛴 그 항목입니다. 계획이 있다면 지금 만들어두는 게 편합니다.
- **플레이어 ID를 클라이언트가 보낸 그대로 믿지 마세요.** 백엔드의 계정 키로 쓰되 서버에서 검증해야 합니다. 공식 문서도 명시적으로 경고하는 부분입니다.

---

## 참고

- [Play Games Services 공식 문서](https://developer.android.com/games/pgs/unity/unity-start) — **Unity용**. `games/pgs/android/`로 시작하는 페이지는 네이티브(Java/Kotlin) 게임용이라 이 프로젝트에는 해당하지 않습니다
- [Saved Games 개념 문서](https://developers.google.com/games/services/common/concepts/savedgames)
- [플러그인 저장소](https://github.com/playgameservices/play-games-plugin-for-unity)
