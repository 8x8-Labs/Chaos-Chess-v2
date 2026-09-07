# 구글 계정 연동 확장 설계 (클라우드 세이브 · 프로필 · 업적)

- **작성일:** 2026-08-09
- **상태:** 계획 (미착수 — 코드 변경 없음)
- **기준 커밋:** `c997a88` / Unity 6000.0.68f1
- **동기:** "구글 로그인이 기능적으로만 구현된 것 같다 — 클라우드 세이브·프로필로 발전시키고 싶다"

> 이 문서는 코드베이스 조사를 바탕으로 작성됐다. 아래 파일:라인 참조는 조사 시점 기준이며,
> 실제 착수 전에 현재 코드와 일치하는지 다시 확인할 것.

---

## 1. 현재 상태 조사 결과

| 항목 | 상태 | 근거 |
|---|---|---|
| 구글 로그인 | 구현 완료 | `GooglePlayAuthManager.cs` — GPGS v2, 자동 사인인 + 수동 재시도, `RuntimeInitializeOnLoadMethod`로 자동 생성 |
| 클라우드 세이브 | **이미 구현됨** | `Save/CloudSaveManager.cs` — GPGS Saved Games 스냅샷 1개(`chaoschess_save`)에 run/collection JSON을 봉투로 묶어 커밋 |
| 로그인 UI | 최소 구현 | `UI/GooglePlayLoginUI.cs` — 닉네임 표시 + 재시도 버튼뿐 |
| 동기화 상태 UI | **없음** | `CloudSaveManager.IsSyncing`(`:71`)을 참조하는 코드가 하나도 없음 |
| 프로필 | **없음** | 아바타 미취득, 누적 통계 자체가 존재하지 않음 |
| 업적 / 리더보드 | **없음** | 코드베이스에서 `ReportProgress`/`ReportScore` 호출 0건 |

**결론:** 클라우드 세이브는 "새로 만들 것"이 아니라 **구멍을 메울 것**이다.
그리고 프로필은 UI 문제가 아니라 **저장할 데이터가 없는 것**이 문제다.

---

## 2. 발견된 결함

### 2.1 [P0] 계정 전환 시 다른 계정 데이터를 덮어씀

`ResolveAndApply()`(`CloudSaveManager.cs:211`)는 **로컬 파일의 mtime만** 보고 최신 여부를 판정한다
(`GetLocalTimestampUnixMs()` `:400`). 그 로컬 데이터가 **어느 계정 것인지는 따지지 않는다.**

재현 경로:

```
계정 A로 플레이 (로컬 파일 mtime = 오늘)
  → 로그아웃 후 계정 B로 로그인
  → SyncFromCloud() → 계정 B의 스냅샷 수신 (savedAtUnixMs = 지난주)
  → :215 cloud.savedAtUnixMs <= localTime → "로컬이 최신" 판정
  → RequestUpload()
  → 계정 B의 클라우드가 계정 A의 런/컬렉션으로 덮어써짐 ❌
```

같은 결함의 두 번째 변종 — **비로그인 플레이 후 첫 로그인**:
로그인 전 저장이 있으면 `_uploadRequestedWhileSignedOut`(`:76`)이 서면
`HandleAuthenticationChanged()`(`:127`)가 **다운로드를 아예 건너뛰고** 곧바로 업로드한다.
이미 세이브가 있는 계정으로 로그인해도 그 계정 데이터가 조용히 사라진다.

**수정안 — 마지막 로그인 계정 ID 기록**

```csharp
// 기기 로컬 값이므로 봉투에 넣지 않는다. PlayerPrefs로 충분.
private const string LastUserIdKey = "cloudsave_last_user_id";
```

로그인 성공 시 분기:

| 이전 ID | 현재 ID | 처리 |
|---|---|---|
| 없음(최초) | X | 클라우드에 데이터가 있으면 **클라우드 우선 적용**. 단 로컬을 `*.local.bak`으로 보존 |
| X | X (동일) | 기존 동작 유지 (mtime 비교) |
| X | Y (전환) | **mtime 무시하고 무조건 다운로드.** 로컬은 `*.<이전ID>.bak`으로 보존 |

- `_uploadRequestedWhileSignedOut` 조기 업로드 경로는 **ID가 동일할 때만** 허용한다.
- 적용 성공 시 `PlayerPrefs`에 현재 `UserId` 기록 후 `Save()`.
- 백업 파일은 `SafeFile`의 `.tmp`/`.bak` 복구 후보와 **확장자가 겹치지 않게** 할 것
  (`SafeFile.TryRead`가 백업을 본 파일로 오인 복구하면 안 된다).

### 2.2 [P0] 컬렉션이 최신본 우선이라 발견 기록이 사라짐

`CloudSaveEnvelope`(`:21`)는 run과 collection을 **한 봉투로 묶어** 통째로 최신본 우선 처리한다.
런 상태는 그게 맞다 — 두 기기의 런을 합치는 건 의미가 없다.

하지만 `CollectionSaveData.discoveredCardNames`(`CollectionSaveData.cs:7`)는 본질적으로 **누적 집합**이다.
기기 B에서 잠깐 플레이하면 기기 A에서 발견한 카드 기록이 통째로 날아간다.

**수정안 — 두 데이터의 병합 정책을 분리한다**

| 데이터 | 정책 |
|---|---|
| `runJson` | 최신본 우선 (현행 유지) |
| `collectionJson` | **항상 합집합(union)** — 타임스탬프 비교와 무관 |
| `profileJson` (신규, 4장) | 필드별 최대값 병합 |

핵심: union은 **어느 쪽이 최신으로 판정되든 실행돼야 한다.**
지금처럼 "로컬이 최신 → 즉시 업로드"로 조기 리턴하면(`:215-219`) 클라우드에만 있던 발견 기록이 유실된다.
→ `ResolveAndApply()`를 "① 컬렉션·프로필 병합 → ② 런만 타임스탬프 비교" 순서로 재배치한다.

- union은 항목을 추가만 하므로 런 진행 중에도 안전하다.
  `IsSafeToOverwriteLocal()`(`:246`) 가드는 **런에만** 적용하고 컬렉션은 통과시킨다.
- 메모리 반영은 기존 `CollectionManager.ReloadFromDisk()`(`CollectionManager.cs:65`)를 그대로 쓴다.
- 트레이드오프: 한 번 발견한 카드는 영구히 남는다(초기화 불가). 발견 로그의 성격상 수용한다.

### 2.3 [P1] 동기화 결과가 사용자에게 전혀 보이지 않음

모든 실패 경로가 `Debug.LogWarning`으로 끝난다 (`:173`, `:186`, `:338`, `:359`).
런 진행 중이라 적용을 보류하는 경우(`:225`)도 사용자는 알 수 없다.
`IsSyncing`은 public이지만 구독자가 없고, `OnCloudDataApplied`는 `ContinueButtonController.cs:19` 한 곳만 쓴다.

**수정안** — `CloudSaveManager`에 상태 열거형과 이벤트를 추가하고 타이틀에 인디케이터를 붙인다.

```csharp
public enum CloudSyncState { Idle, Syncing, Synced, Failed, DeferredInRun }
public event Action<CloudSyncState> OnSyncStateChanged;
```

---

## 3. 신규 — 프로필

### 3.1 선행 조건: 누적 통계가 존재하지 않는다

현재 통계는 `PlayerState`의 승/무/패뿐인데(`PlayerState.cs:61-63`),
`InitializeRun()`(`:86-96`)이 **런 시작마다 0으로 리셋**한다.
`RunSaveData.winCount`(`RunSaveData.cs:57`)도 런 스코프다.
즉 **평생 누적 데이터가 코드베이스 어디에도 없다.** 프로필 UI보다 이게 먼저다.

### 3.2 `ProfileSaveData` 신설

```csharp
[Serializable]
public class ProfileSaveData
{
    public int version = 1;            // 이후 필드 추가 시 마이그레이션 분기용

    public int totalRuns;              // 시작한 런 수
    public int clearedRuns;            // 최종 층까지 클리어한 런 수
    public int bestFloor;              // 최고 도달 층
    public int bestDefeatedElo;        // 격파한 상대 중 최고 Elo

    public int totalWins;
    public int totalDraws;
    public int totalLosses;

    public long totalPlaySeconds;
    public long firstPlayedUnixMs;

    // JsonUtility는 Dictionary를 직렬화하지 못한다 → 인덱스가 대응하는 병렬 리스트로 저장
    public List<string> cardUseNames  = new();
    public List<int>    cardUseCounts = new();
}
```

- 저장 경로: `Application.persistentDataPath/profile_save.json`
- 관리 클래스: `ProfileManager` — `CollectionManager`와 동일한 형태(싱글톤 + static 경로 + `SafeFile` + 변경 시 `CloudSaveManager.Instance?.RequestUpload()`)
- 갱신 지점
  - `PlayerState.SetGameResult()`(`:69`) → 누적 승/무/패, `bestDefeatedElo`
  - `GameCycleManager.StartGame()` → `totalRuns++`
  - `MapManager` 층 이동 → `bestFloor`
- **`GameMode.Practice`는 집계에서 제외한다.** 연습으로 업적·리더보드를 올릴 수 있으면 안 된다.

### 3.3 봉투 확장

`CloudSaveEnvelope`에 `public string profileJson = string.Empty;`를 추가한다.
`JsonUtility`는 **JSON에 없는 필드를 기본값으로 두고, 모르는 필드는 무시**하므로
구버전 봉투를 읽어도 예외 없이 빈 문자열이 되고, 신버전 봉투를 구버전 앱이 읽어도 깨지지 않는다.
→ 별도 마이그레이션 코드 불필요.

### 3.4 아바타

`GooglePlayAuthManager`는 지금 이름과 ID만 캐싱한다(`:109-110`).
`PlayGamesPlatform.Instance.GetUserImageUrl()`을 추가로 캐싱하고,
`UnityWebRequestTexture`로 1회 로드해 `Sprite`로 보관한다(세션 캐시로 충분, 디스크 캐시 불필요).

### 3.5 프로필 화면 구성

```
[아바타]  닉네임
          최고 도달 층 12F · 클리어 3회
          누적 47승 5무 22패 (승률 63%)
          카드 도감 38 / 54
          최다 사용 카드: 화염 (24회)
          [업적 보기] [리더보드]
```

---

## 4. 신규 — 업적 · 리더보드

로그인이 이미 붙어 있어 **Play Console 설정 + 코드 수십 줄**이면 끝난다. 투입 대비 효과가 가장 크다.

**업적 후보** — 필요한 데이터는 3장을 마치면 전부 확보된다.

| 업적 | 소스 |
|---|---|
| 카드 10 / 30 / 54종 발견 | `CollectionSaveData.discoveredCardNames.Count` |
| 첫 보스 클리어 / 최종 층 클리어 | `clearedRuns` |
| Elo 1800 이상 격파 | `bestDefeatedElo` |
| 아레나 전멸 승리 | `ArenaManager` 결과 |
| 무패 런 클리어 | `totalLosses` 런 스코프 비교 |

**리더보드 후보:** 최고 도달 층 / 격파한 최고 Elo / 누적 승수

**구현 메모**

- Play Console에서 ID 발급 → GPGS 플러그인 Android setup에 리소스 정의를 붙여넣으면 `GPGSIds` 클래스가 생성된다.
- 모든 호출은 래퍼 하나(`PlayServicesReporter`)로 감싼다. 미로그인·비안드로이드에서 조용히 no-op 해야 한다 —
  지금 코드가 `#if UNITY_ANDROID && !UNITY_EDITOR`로 분기하는 방식과 동일하게 맞춘다.
- 업적/리더보드 보고는 **멱등(idempotent)** 하므로, 오프라인 실패에 대비해
  앱 시작 시 `ProfileSaveData` 기준으로 전량 재보고해도 안전하다. 재시도 큐를 따로 만들지 말 것.

---

## 5. 단계별 계획

| 단계 | 내용 | 선행 | 위험도 |
|---|---|---|---|
| **0** | 실기 세이브 백업, Play Console에 업적/리더보드 ID 발급 | — | 없음 |
| **1** | 2.1 계정 격리 + 2.2 컬렉션 union | 0 | **높음** (세이브 경로 직접 수정) |
| **2** | `ProfileSaveData` + `ProfileManager` + 봉투에 `profileJson` 추가 | 1 | 중 |
| **3** | 아바타 취득 + 프로필 UI | 2 | 낮음 |
| **4** | 업적 · 리더보드 보고 | 2 | 낮음 |
| **5** | 동기화 상태 UI (2.3) | 1 | 낮음 |

**1단계를 먼저 하는 이유:** 실제 데이터 유실 경로이고, 2단계 이후에 하면
프로필 데이터까지 같은 방식으로 날아갈 수 있다. 출시 전 필수.

---

## 6. 검증 방법

**에디터에서는 검증할 수 없다.** `CloudSaveManager`의 GPGS 호출은 전부
`#if UNITY_ANDROID && !UNITY_EDITOR` 안에 있어 에디터 빌드에서는 컴파일 자체가 제외된다.

→ **설계 시 대응:** 순수 로직(봉투 병합, union, 타임스탬프 판정, 계정 전환 분기)을
플랫폼 분기 **밖의 `static` 메서드**로 분리한다. 그러면 에디터에서 입력값을 직접 넣어 검증할 수 있고,
플랫폼 분기 안에는 GPGS 호출과 콜백만 남는다. 이건 리팩터링이 아니라 1단계 구현 방식의 문제다.

**실기 체크리스트 (계정 2개 필요)**

1. 계정 A로 플레이 → 앱 재시작 → 진행도 유지되는가
2. 계정 A → 계정 B 전환 → **B의 데이터가 나오고, A의 클라우드가 그대로인가** (2.1)
3. 기기 A에서 카드 X 발견 / 기기 B에서 카드 Y 발견 → 동기화 후 **양쪽 모두 X·Y를 갖는가** (2.2)
4. 비행기 모드로 플레이 → 온라인 복귀 → 업로드되는가
5. 런 진행 중 동기화 → 로컬이 덮어써지지 않고 보류 안내가 뜨는가
6. 앱 삭제 후 재설치 → 로그인 → 진행도 복구되는가

---

## 7. 범위 밖

- **멀티플레이** — `Docs/Multiplayer-Design.md` 참조. 이 문서는 싱글 계정 기능만 다룬다.
- **iOS / PC** — GPGS는 안드로이드 전용. 크로스 플랫폼이 필요해지면 Saved Games가 아니라
  UGS Cloud Save 같은 플랫폼 중립 백엔드로 갈아타야 하며, 그때 이 문서의 봉투 구조는 그대로 재사용 가능하다.
- **서버 권위 검증** — 통계는 클라이언트 신뢰 방식이다. 리더보드 조작을 막으려면 백엔드가 필요하고,
  그건 멀티플레이 문서의 백엔드 작업과 함께 다뤄야 한다.
