# UI 구조 분석

- **작성일:** 2026-08-25
- **범위:** `Assets/Script/UI/` 공통 화면 전환 프레임워크. 카드북·리워드 등 개별 화면의 세부 로직은 다루지 않는다.
- **계기:** 멀티플레이 연결 UI(`MatchConnectUI`, [Multiplayer-Design.md](Multiplayer-Design.md) 9-2)를 씬에 배치하기 전에, 기존 화면들이 어떤 틀로 만들어져 있는지 정리해 둔다.

---

## 1. 3단 구조: Canvas → Panel → Button

이 프로젝트의 UI는 세 계층으로 나뉜다.

| 계층 | 클래스 | 단위 | 예 |
|---|---|---|---|
| 전체 화면 | `ButtonCanvas` | 씬 안에서 서로 배타적으로 전환되는 큰 화면 | 타이틀, 맵 |
| 화면 내 패널 | `ButtonPanel` | 화면 위에 뜨는 팝업/서브 화면 | 각성 선택창, 승급 선택창, 결과 팝업 |
| 버튼 | `UIButton` | 위 둘을 조작하는 단일 재사용 컴포넌트 | 모든 클릭 가능한 버튼 |

`ButtonCanvas`와 `ButtonPanel`은 공통 조상 없이 각자 독립적으로 `Awake()`에서 `CanvasGroup`을 캐싱하고, DOTween으로 알파를 0↔1 페이드하는 거의 동일한 패턴을 따로 구현하고 있다(`ButtonCanvas`만 `ButtonParent`를 상속). 둘 다 `Start()`에서 기본 비활성 상태로 접혀 있다가 명시적으로 켜진다.

---

## 2. `ButtonParent` / `ButtonCanvas` — 전체 화면

[ButtonParent.cs](../Assets/Script/UI/ButtonParent.cs)가 최소 계약을 정의한다.

```csharp
public class ButtonParent : MonoBehaviour
{
    [SerializeField] protected bool isMainParent = false;

    protected virtual void Start()
    {
        if (isMainParent) EnableParent();
        else DisableParent();
    }

    public virtual void EnableParent() { }
    public virtual void DisableParent() { }
}
```

`isMainParent`를 체크한 캔버스 하나만 씬 시작 시 자동으로 열린다(`ButtonType.GoMain`이 찾는 대상이기도 하다).

[ButtonCanvas.cs](../Assets/Script/UI/ButtonCanvas.cs)가 실제 동작을 구현한다.

- `EnableParent()` — `Canvas.enabled = true`, 알파 0에서 페이드인, `OnCanvasEnabled` UnityEvent 발행, 자식의 `BasicUIAnimation`들을 순차 딜레이로 재생
- `DisableParent()` — `OnCanvasDisabled` 발행 후 `Canvas.enabled = false`
- `FadeIn()`/`FadeOut()` — DOTween 알파 트윈. `FadeIn()` 완료 시 캔버스를 실제로 끔(`enabled = false`)
- `MainCanvas` — `isMainParent` 값을 그대로 노출. `ButtonType.GoMain`이 씬에서 `ButtonCanvas`를 전부 찾아(`FindObjectsOfType`) 이 값이 true인 것을 찾는다

---

## 3. `ButtonPanel` — 화면 내 팝업/서브 화면

[ButtonPanel.cs](../Assets/Script/UI/ButtonPanel.cs)는 `ButtonCanvas`와 별개 클래스지만 구조가 거의 같다.

- `EnablePanel()` — 알파 0에서 페이드인, `OnPanelEnabled` UnityEvent 발행
- `DisablePanel()` — `OnPanelDisabled` 발행 후 페이드아웃, `canvasGroup.blocksRaycasts = false`로 클릭 통과 처리
- `baseEnabled` — 기본값 false. 켜 두면 씬 시작부터 열려 있는 패널

**게임 코드가 직접 여닫는 패턴**이 흔하다. `ButtonPanel`을 상속해 `Show()`/`Hide()`로 감싸는 게 관례다.

```csharp
// AwakenPanel.cs — 다른 코드가 콜백을 들고 패널을 여는 전형적인 예
public class AwakenPanel : ButtonPanel
{
    private Action onClick;

    public void Show(Action callback)
    {
        onClick = callback;
        EnablePanel();
    }

    public void OnClickAwaken()
    {
        DisablePanel();
        onClick?.Invoke();
    }
}
```

같은 패턴을 쓰는 다른 예: `EndGamePanel`(10절 참고, `GameManager`가 결과를 들고 `Show(GameResult)` 호출), `PromotionPanel`, `TimeReversalPanel`.

---

## 4. `UIButton` — 단일 재사용 버튼

[UIButton.cs](../Assets/Script/UI/UIButton.cs)는 `Button`을 상속하는 **하나의 클래스가 모든 버튼을 담당**한다. 동작은 인스펙터에서 고르는 `ButtonType` enum으로 분기한다.

| ButtonType | 동작 |
|---|---|
| `None` | 클릭 사운드만 |
| `ChangeCanvas` | `disableCanvas.FadeIn()` → 0.2초 대기 → `enableCanvas.EnableParent()` (전체 화면 전환) |
| `ChangePanel` | 위와 동일하되 `ButtonPanel` 대상 (패널 전환) |
| `OpenPopup` | `enablePanel.EnablePanel()`만 (현재 화면 위에 팝업) |
| `ClosePopup` | `disablePanel.DisablePanel()`만. `disablePanel`이 비어 있으면 부모에서 자동으로 찾음 |
| `GoScene` | `SceneLoadManager.Instance.LoadScene(nextSceneName)` — **씬 자체를 교체** (페이드 캔버스가 아니라 유니티 씬 전환) |
| `GameStart` | `GameCycleManager.StartGame()` 호출 후 `changeCanvas()` |
| `PracticeStart` | `GameCycleManager.StartPractice(difficulty)` 호출 후 `practiceSceneName`("MainGameScene")으로 씬 전환 |
| `ContinueRun` | `GameCycleManager.ContinueRun()` — 씬 전환은 그 안에서 처리하므로 버튼은 호출만 함 |
| `EndGame` | 승패/파이널 층 여부를 보고 `ResultScene` 또는 `RewardScene`으로 전환 |
| `GoMain` | 씬의 `MainCanvas`(`isMainParent=true`)를 찾아 `ChangeCanvas`와 동일하게 전환 |
| `Quit` | (문자열 참고용, 별도 구현 없음 — 실제 종료 로직은 다른 곳) |
| `HostMultiplayer` | `GameCycleManager.StartMultiplayerAsHost()` 호출 후 `changePanel()` (멀티플레이 연결 UI 전용, 2026-08-25 추가) |
| `JoinMultiplayer` | `joinCodeInput` 필드 값을 읽어 `GameCycleManager.StartMultiplayerAsGuest(code)` 호출. 화면 전환 없음 |
| `CopyMultiplayerJoinCode` | `MatchSession.HostJoinCode`를 클립보드에 복사 |
| `CancelMultiplayerConnect` | `GameCycleManager.CancelMultiplayerConnect()` 호출 후 `changePanel()` |

> 새 `ButtonType` 값은 항상 enum 맨 뒤에 추가한다. 기존 값 사이에 끼워 넣으면 씬에 이미
> 저장된 버튼들의 `ButtonType`이 인덱스 밀림으로 조용히 바뀐다.

**중요한 구분 두 가지**

1. **`ChangeCanvas`/`ChangePanel`은 같은 씬 안에서의 페이드 전환**이고, **`GoScene`은 실제 유니티 씬 로드**다(`SceneLoadManager`가 로딩 오버레이 + BGM 페이드까지 챙긴다). 맵 노드 클릭 → `MainGameScene` 진입이 후자다.
2. **`GameStart`/`PracticeStart`/`ContinueRun`처럼 게임 로직 호출이 필요한 버튼은 `UIButton.OnClicked()`의 switch문에 하드코딩**돼 있다. 범용 `ChangeCanvas`/`ChangePanel`으로는 "화면 전환 + 임의의 게임 로직 실행"을 한 번에 못 하므로, 이 프로젝트는 그런 경우 새 `ButtonType`을 추가하는 쪽을 택해 왔다.

---

## 5. 커스텀 로직을 패널에 거는 두 가지 방법

이 코드베이스에서 실제로 쓰는 두 갈래:

- **UnityEvent 훅**: `ButtonCanvas.OnCanvasEnabled/OnCanvasDisabled`, `ButtonPanel.OnPanelEnabled/OnPanelDisabled`를 인스펙터에서 원하는 메서드에 연결. 화면이 열리고 닫히는 시점에 사이드이펙트를 걸 때 씀
- **`ButtonPanel` 상속 + 게임 코드가 직접 `Show()`/`Hide()` 호출**: `AwakenPanel`, `EndGamePanel`처럼 결과 데이터를 들고 여닫아야 할 때. 버튼 클릭이 아니라 게임 상태 변화가 트리거인 경우 이쪽

`ButtonType`에 새 값을 추가하는 것(4절 마지막 항목)은 "버튼 클릭 자체가 게임 로직을 시작시켜야 할 때"(`GameStart`류) 쓰는 세 번째 갈래다.

---

## 6. 씬 단위 흐름

`UIButton`의 씬 이름 필드 기본값들이 전체 흐름을 보여준다.

```
MainScene ──GameStart──▶ StartRewardCanvas ──GoScene──▶ MapScene ──노드 클릭──▶ MainGameScene
MainScene ──PracticeStart/ContinueRun──▶ MainGameScene
MainGameScene ──EndGame──▶ RewardScene (중간 승리) / ResultScene (런 종료)
RewardScene/ResultScene ──GoScene──▶ MainScene
```

**맵 화면은 `MainScene`의 캔버스가 아니라 별도의 `MapScene`이다.** `MapManager`는 `MainScene`에 있는 싱글턴이지만 `MapUI`는 `MapScene`에 있다. `MainScene`에서는 `StartRewardCanvas`의 `NextBtn`(`ButtonType.GoScene`, `nextSceneName = MapScene`)이 맵으로 넘어가는 유일한 통로다. 맵의 각 노드는 `UIButton`(`ButtonType.GoScene`, `nextSceneName`을 `MapUI.SetNextScene()`으로 주입)을 통해 `MainGameScene`으로 씬 전환한다 — 캔버스/패널 페이드가 아니라 진짜 씬 로드다.

> 그래서 `GameCycleManager`가 `Ready`에서 부르는 `MapUI.Instance?.Rebuild()`는 아직 `MainScene`에 있는 동안 실행되어 대개 no-op이다(`MapUI`가 아직 로드되지 않음). 실제 방어는 `MapScene` 로드 후 `MapUI`가 노드를 그릴 때 `IsMultiplayerHandshakePending()`으로 걸러 주는 쪽이 담당한다. [MapUI.cs](../Assets/Script/Map/MapUI.cs) 리팩터링 기록은 [Multiplayer-Design.md](Multiplayer-Design.md) 11-8을 참고.

---

## 7. `MatchConnectUI` + 전용 `ButtonType` (B안)

연결 화면 네 개(진입/호스트 대기/게스트 대기/실패)는 전부 **전체 화면**이므로 `ButtonPanel`이 아니라 2절의 `ButtonCanvas`로 둔다. 클릭과 상태 구독은 완전히 분리한다.

- **클릭으로 시작하는 동작**(호스트 열기/게스트 접속/코드 복사/취소)은 [UIButton.cs](../Assets/Script/UI/UIButton.cs)에 4절 표의 `HostMultiplayer`/`JoinMultiplayer`/`CopyMultiplayerJoinCode`/`CancelMultiplayerConnect`로 직접 있다. `GameStart`처럼 게임 로직 호출이 필요한 버튼과 같은 자리(하드코딩된 `ButtonType`)에 둔 것이다. `HostMultiplayer`/`CancelMultiplayerConnect`는 `GameStart`와 같은 방식으로 `disableCanvas`/`enableCanvas`를 쓴다.
- **버튼이 아닌 상태 변화**(`Ready`/`Failed`)는 [MatchConnectUI.cs](../Assets/Script/Multiplayer/MatchConnectUI.cs)가 `MatchSession.StateChanged`를 구독해서 처리한다. 클릭 핸들러는 없고, 캔버스 여닫기·상태 텍스트 갱신만 한다.

**씬에서 연결할 것**

| 버튼/화면 | 설정 |
|---|---|
| `entryCanvas` | `isMainParent = true` (씬 시작 시 자동으로 열림). 나머지 세 화면은 `false` |
| "방 만들기" 버튼 | `UIButton(HostMultiplayer, disableCanvas=entryCanvas, enableCanvas=hostWaitingCanvas)` — 클릭 즉시 호스팅 시작 + 화면 전환 |
| "코드로 참가" 버튼 | `UIButton(ChangeCanvas, disableCanvas=entryCanvas, enableCanvas=guestWaitingCanvas)` (입력이 필요해 접속은 아직 안 함) |
| `guestWaitingCanvas` 안 "참가" 버튼 | `UIButton(JoinMultiplayer, joinCodeInput=해당 TMP_InputField)` |
| 코드 복사 버튼 | `UIButton(CopyMultiplayerJoinCode)` |
| 대기 화면 "취소" / 실패 화면 "되돌아가기" | `UIButton(CancelMultiplayerConnect, disableCanvas=해당 화면, enableCanvas=entryCanvas)` |
| `MatchConnectUI.matchReadyCanvas` | 합의가 끝나면 열 화면. 단일 플레이에서 `GameStart` 버튼이 여는 화면(`StartRewardCanvas`)과 같은 자리다 |

`MatchConnectUI`는 씬의 `hostWaitingCanvas`/`guestWaitingCanvas`/`failureCanvas`/`matchReadyCanvas`와 상태 텍스트들만 인스펙터에 연결하면 된다(entryCanvas는 몰라도 됨 — 얘는 여닫히는 대상이 아니라 항상 되돌아가는 목적지일 뿐).

> **`matchReadyCanvas`를 빼먹으면 합의가 끝난 뒤 빈 화면이 된다.** 단일 플레이는 `GameStart` 버튼의 `changeCanvas()`가 클릭 즉시 다음 화면을 열지만, 멀티는 클릭 시점에 아직 합의가 안 끝나 대기 화면으로 간다. `GameCycleManager`가 `Ready`에서 부르는 `MapManager.Init()`은 맵 **데이터**만 다시 만들 뿐 화면을 넘기지 않으므로(맵 화면은 `MainScene`의 캔버스가 아니라 별도 `MapScene`이고, 그 진입은 `StartRewardCanvas`의 `GoScene` 버튼이 맡는다), 화면 전환은 `MatchConnectUI`가 처리한다.

> `UIButton.Start()`는 `buttonType`이 `ChangeCanvas`가 아니면 `disableCanvas`를 부모의 `ButtonCanvas`로 자동 채운다(`GameStart` 등 기존 타입과 동일한 기존 동작). `HostMultiplayer`/`CancelMultiplayerConnect` 버튼은 실제로 끌 화면의 자식으로 두면 인스펙터 값과 자동 채움이 일치한다.
