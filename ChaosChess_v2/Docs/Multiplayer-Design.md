# 멀티플레이 대전 모드 설계 문서

- **작성일:** 2026-08-09 (최종 갱신 2026-08-16)
- **상태:** **0·1·2단계 구현 완료 / 3단계 진행 중**
- **작업 브랜치:** `feature/multiplayer-step0` · **추적 이슈:** #305
- **환경:** Unity 6000.0.68f1
- **목표:** 구글 로그인 → 매칭 → 카드 N장 랜덤 배분 → 대국 → Elo 레이팅 반영

> 3~6절은 착수 전 조사를 바탕으로 쓴 설계 근거다. 구현이 진행되면서 일부 파일:라인
> 참조는 더 이상 맞지 않는다. 현재 구현 상태는 **10절**을, 다음 할 일은 **11절**을 볼 것.

---

## 1. 현재 상태 조사 결과

| 항목 | 상태 | 근거 |
|---|---|---|
| 구글 로그인 | **이미 구현됨** | `Assets/Script/GooglePlayAuthManager.cs` — GPGS v2, Android 전용, `RuntimeInitializeOnLoadMethod`로 자동 생성 |
| 클라우드 저장 | 이미 구현됨 | `Assets/Script/Save/CloudSaveManager.cs` — GPGS Saved Games 스냅샷, most-recent-wins |
| 네트워킹 | **없음** | `Packages/manifest.json`에 Netcode / Relay / Lobby / UGS 패키지 전무. `com.unity.multiplayer.center`는 셋업 가이드 UI일 뿐 실제 기능 없음 |
| 백엔드 | **없음** | 서버 코드·DB 없음 |
| 매치 로직 | 결정론적 | 상태가 FEN + UCI 문자열로 완전히 표현됨 → 멀티에 매우 유리 |

**결론:** "구글 로그인"은 사실상 완료 상태. 실제 작업은 다음 세 가지다.

1. 백엔드 (인증 검증 / 매칭 / 레이팅)
2. 매치 동기화 모델
3. 서버 권위 레이팅 로직

---

## 2. 핵심 결정: 동기화 모델은 "커맨드 릴레이(로크스텝)"

### 하지 말 것 — 상태 동기화

NGO로 기물마다 `NetworkObject`를 붙이는 방식은 이 프로젝트에서 거의 확실히 실패한다.
카드 이펙터가 **기물 타입·타일 규칙·턴 구조 자체**를 런타임에 바꾸기 때문에,
복제해야 할 상태가 사실상 게임 전체가 된다.

### 채택 — 커맨드 릴레이

양쪽이 동일한 초기 상태(FEN + 시드 + 카드 목록)에서 출발하고,
네트워크로는 **행동만** 주고받는다.

```jsonc
{ "turn": 14, "kind": "Move",   "uci": "e2e4" }
{ "turn": 15, "kind": "Card",   "cardId": "arena_01", "targets": ["d4", "f6"] }
{ "turn": 16, "kind": "Resign" }
```

**이점**

- 한 턴당 수십 바이트 → 릴레이 비용 무시 가능
- 재접속 시 이동 로그 재생만으로 복구
- 리플레이 기능이 공짜로 따라옴
- 서버 측 검증(6단계)으로 자연스럽게 확장 가능

**전제 조건**

- 양쪽 클라이언트의 **룰 판정이 완전히 일치**해야 한다 → 엔진 버전·variant 설정 고정 필수

---

## 3. 코드 접합점 (Seam)

### 3-1. 상대 착수 주입 — 이미 훅이 뚫려 있음

`Assets/Script/ChessSystem/GameManager.cs:582`

```csharp
if (aiTurnController != null &&
    aiTurnController.TryRequestTurn(this, BoardManager.Instance, RequestStockfishAIMove))
{
    return;
}
```

여기에 `RemoteTurnController`를 물려서 네트워크 응답을 기다렸다가
`BoardManager.Instance.ApplyUCIMove(uci)`를 호출하면 **코어 턴 루프는 거의 그대로** 간다.

- ⚠️ `Assets/Script/AIIntegration/Runtime/AiTurnController.cs`는 인터페이스가 아니라
  **구체 클래스**이고 `GameManager.cs:41`에서 `[SerializeField]`로 직접 참조된다.
  → `IAiTurnController`(또는 `ITurnProvider`)로 추상화하는 리팩터가 선행돼야 한다.

### 3-2. 플레이어 = 백 하드코딩 — 최대 작업량

`Assets/Script/ChessSystem/GameManager.cs:35`

```csharp
public bool IsPlayerTurn => (curTurn % 2 == 1);
```

흑을 맡는 쪽이 존재하는 순간 깨진다. `LocalColor` 개념을 도입해
`IsLocalTurn => turnColor == LocalColor`로 바꾸고, **아래를 전부 같은 기준으로 통일**해야 한다.

- 입력 게이트 — `GameManager.cs:245` (`if (!IsPlayerTurn && AiAutoMoveEnabled) return;`)
- 보드 UI 방향 (흑 시점 회전) — `BoardUI.cs`
- 카드 대상 판정(아군/적군) — `Card/Effector/*`, `Card/Selector/*`
- 체크 상태 표시 — `GameManager.cs:714` (`UpdatePlayerCheckState`)

> **이 항목이 전체 작업에서 가장 품이 많이 든다.** 네트워크보다 먼저 해야 한다.

### 3-3. 카드 사용 송·수신

`Assets/Script/GameCycle/CardRandomizerManager.cs:34` — `ExecuteCard(CardDataSO, Action)`가
카드 실행 단일 진입점이라 송신/수신 훅을 걸기 좋다.

### 3-4. Fairy Stockfish의 역할 변경

멀티에서는 **AI 착수용으로는 끄고, 룰 오라클로는 계속 쓴다.**

- 끄기: `AiAutoMoveEnabled = false` (`GameManager.cs:40`, 이미 존재)
- 계속 사용: 합법수 조회(`GetLegalMovesAsync`), 체크/체크메이트 판정(`EvaluateGameState`)
- 매치 시작 핸드셰이크에서 `AIIntegration/ChaosChessAiVersion.cs`의 버전을 서로 검증할 것

---

## 4. 백엔드 — Unity Gaming Services(UGS) 권장

| 서비스 | 용도 |
|---|---|
| **Authentication** | GPGS `RequestServerSideAccess`로 받은 server auth code → `SignInWithGooglePlayGamesAsync`. 기존 로그인 코드 유지한 채 위에 얹는다 |
| **Lobby** 또는 **Matchmaker** | 매칭. Matchmaker가 레이팅 밴드 매칭을 지원 |
| **Relay** | NAT 통과, 두 클라이언트 연결 |
| **Cloud Code** | 레이팅 계산 (서버 권위) |
| **Leaderboards** | 랭킹 |
| **Cloud Save** | 레이팅/전적 저장 |

**저장소 분리 권장:** 런 세이브는 기존 GPGS Saved Games 유지 / 랭크 데이터는 UGS Cloud Save.
둘을 섞으면 `CloudSaveManager`의 most-recent-wins 정책과 충돌한다.

**대안**

- Firebase (Auth + Firestore + Functions + 자체 WebSocket 릴레이) — 릴레이·매칭 직접 구현 필요, 손이 더 감
- 자체 서버 (Node / ASP.NET) — 현재 규모에선 불필요

### 보안 원칙

> 클라이언트가 보낸 `UserId`를 **절대 그대로 믿지 않는다.**
> 반드시 서버에서 auth code를 검증한 신원으로만 레이팅을 갱신한다.

---

## 5. 카드 N장 랜덤 배분

### 문제

`Assets/Script/GameCycle/CardRandomizerManager.cs`의 `Shuffle()`(175행)과
`GetRandomCardsFrom*` / `TryGetRandomCardByTier`가 전부 `UnityEngine.Random`(전역 상태)을 쓴다.
→ **두 클라이언트에서 결과가 일치할 수 없다.**

### 해법

| 방식 | 설명 | 평가 |
|---|---|---|
| **A. 서버가 카드 ID 목록을 직접 결정해 내려줌** | 매치 생성 시 서버가 뽑아서 전달 | **권장.** 치팅 여지 없음, 밸런스·카드 풀 제한을 서버에서 조절 가능 |
| B. 서버가 시드만 내려주고 클라가 재현 | `System.Random(seed)` 기반 결정론 셔플 | 구현은 쉽지만, 클라가 시드를 알면 상대 카드까지 계산 가능 |

> **2026-08-16 결정 — A안 채택, RNG 주입은 불필요.**
> 위 표에서 "어느 쪽이든 RNG 주입이 필요하다"고 적었지만 A안에서는 아니다.
> 정하는 쪽이 자기 난수로 뽑아 목록을 보내고 **받는 쪽은 아예 뽑지 않으므로**,
> `CardRandomizerManager`의 `UnityEngine.Random`은 그대로 두어도 된다.
> 시드 재현이 필요한 것은 B안뿐이다.
>
> **아직 서버가 없으므로 3단계에서는 호스트가 정한다.** Cloud Code는 5단계다.
> 호스트가 자기 유리하게 뽑을 수 있다는 점은 감수하고, 6단계 서버 검증에서 대체한다.
>
> 카드는 매치 중 주기적으로도 뽑히므로 한 장씩 협상하지 않고
> **미리 섞은 카드 큐**를 각자에게 통째로 내려준다(`MatchSetup.CardQueue`).
> 큐에는 받는 쪽 몫만 담는다 — 상대 몫까지 실으면 B안과 같은 문제가 생긴다.

---

## 6. 레이팅 (Elo)

### 반드시 서버(Cloud Code)에서 계산한다

클라 계산 + 결과 업로드 방식은 100% 뚫린다.

### 공식

```
E = 1 / (1 + 10^((R_opp - R) / 400))
R' = R + K * (S - E)
S = 승 1.0 / 무 0.5 / 패 0.0
```

### 파라미터

- 초기 레이팅: **1200**
- K값: 30판 미만 **40** → 이후 **20** → 2400 이상 **10** (체스 관례)
- 티어 구간: 200점 단위

### 결과 확정 규칙

- 양쪽 클라가 보고한 결과가 **일치할 때만** 반영
- 불일치 시 무효 처리 + 어뷰징 플래그 기록
- 한쪽 이탈 시 **30초 유예** 후 이탈자 패배 처리

### 데이터 스키마 (예시)

```jsonc
// players/{uid}
{ "rating": 1200, "games": 0, "win": 0, "draw": 0, "lose": 0, "lastMatchId": "" }

// matches/{matchId}
{ "white": "uid", "black": "uid", "result": "WhiteWin",
  "moveLog": ["e2e4", "..."], "deltaW": 12, "deltaB": -12, "cards": ["..."] }
```

### 매칭 정책

레이팅 밴드 확장: **±50에서 시작 → 10초마다 ±50씩 확대 → 상한 ±400**

---

## 7. 진행 순서 (로드맵)

| 단계 | 내용 | 상태 | 이 순서인 이유 |
|---|---|---|---|
| **0** | `GameMode.Multiplayer` 추가 + **`LocalColor` 도입(흑 플레이 가능화)** + 턴 프로바이더 인터페이스 추상화 | ✅ 완료 | 네트워크 없이 검증 가능. 여기서 버그를 다 잡고 가야 함. **이걸 안 하면 나중에 전부 다시 손대야 한다** |
| **1** | `IMatchTransport` 추상화 + 로컬 루프백 구현 | ✅ 완료 | 에디터 1개에서 양쪽 시뮬레이션 |
| **2** | UGS Auth + Relay 실제 2인 연결 | ✅ 완료 | 여기서 처음 진짜 통신 |
| **3** | 서버 시드/카드 배분 + 매치 시작 핸드셰이크(엔진 버전·카드 DB 해시 검증) | 🔶 **진행 중** | 3a 이음매 완료 / 3b 핸드셰이크·3c 연결 시점 이동 남음 |
| **4** | 체스 시계 + 이탈/재접속 처리 | ⬜ | |
| **5** | Cloud Code 레이팅 + Leaderboards + 티어 UI | ⬜ | |
| **6** | (선택) 서버 측 수 검증 | ⬜ | 랭크가 실질적 의미를 갖기 시작하면 |

### 추가 필요 패키지

```
com.unity.services.authentication
com.unity.services.multiplayer     // Relay + Lobby + Matchmaker 통합
com.unity.transport                // Relay 위에서 직접 바이트를 흘린다
com.unity.services.cloudcode
com.unity.services.leaderboards
com.unity.multiplayer.playmode     // MPPM 테스트용
```

> **2026-08 갱신 — 패키지 통합.**
> Unity 6에서 `com.unity.services.relay` / `lobby` / `matchmaker` 세 개가 deprecated 되고
> **`com.unity.services.multiplayer`(Multiplayer Services SDK)** 하나로 합쳐졌다.
> `com.unity.services.authentication`은 그대로 별도 패키지다.
>
> - ⚠️ **구 `com.unity.services.relay`를 같이 설치하면 ambiguous reference 컴파일 에러가 난다.**
>   통합 패키지만 넣을 것.
> - 설계에는 영향이 없다. UTP는 netcode-agnostic이라 **NGO 없이 Relay + UTP** 조합이 그대로 지원된다.
>   커맨드 릴레이 방식과 `IMatchTransport` 구조는 손댈 필요가 없다.
> - 바뀌는 것은 `RelayMatchTransport` 안쪽 API 두 군데뿐이다.
>   `new RelayServerData(alloc, type)` → `AllocationUtils.ToRelayServerData(alloc, type)`,
>   `Relay.Instance` → `RelayService.Instance`.
> - Lobby가 통합 패키지에 이미 들어 있으므로 3단계 매칭에서 패키지를 더 넣을 필요가 없다.
>
> 참고: [마이그레이션 가이드](https://docs.unity.com/ugs/en-us/manual/mps-sdk/manual/migration-path) ·
> [Relay + UTP](https://docs.unity.com/en-us/relay/relay-and-utp)

---

## 8. 알려진 함정

### 8-1. `ModifyELO`는 플레이어 레이팅이 아니다

`GameManager.cs:234`의 `ModifyELO`는 `MapManager.Instance.curMap.ELO`,
즉 **AI 난이도**를 조정한다. 플레이어 레이팅과 완전히 다른 개념이므로 이름 충돌에 주의.
멀티 매치에서는 `MapManager` / `PlayerState` 의존을 타지 않도록 분기해야 한다
(둘 다 로그라이크 런 전용 상태).

### 8-2. 비동기 콜백 경합

`NextTurn`이 `GetLegalMovesAsync`를 중첩 호출한다 (`GameManager.cs:353`, `372`).
여기에 네트워크 지연이 끼면 경합이 발생하기 쉽다.

- 원격 수 적용은 반드시 `UnityMainThreadDispatcher`를 경유
- 메시지에 **턴 번호**를 넣어 순서를 강제하고, 중복/역순 메시지는 폐기

### 8-3. 플랫폼별 엔진 일관성

GPGS를 쓰므로 타깃은 Android인데 `Assets/StreamingAssets/fairy-stockfish.exe`는
Windows 바이너리다. 룰 판정이 양 플랫폼에서 동일하게 나오는 것이 로크스텝의 전제이므로,
`Assets/Plugins/ChaosChess.AI`의 플랫폼 커버리지를 착수 전에 확인할 것.

---

## 9. 다음 액션

3b(핸드셰이크 송수신)와 3c(연결 시점 이동)로 진행한다. 준비 사항은 11절에 정리했다.

---

## 10. 구현 현황 (0·1·2단계 + 3a)

### 0단계 — "플레이어 = 백" 전제 제거

| 항목 | 결과 |
|---|---|
| 카드 대상 판정 | `PieceColor` 절대 색상 → `CardTargetRelation`(`Self`/`Opponent`/`Any`)로 전환. `[FormerlySerializedAs]`로 `.asset` 값 승계 |
| 플레이어 진영 | `GameCycleManager.PlayerColor` → `GameManager`가 매치 시작 시 주입. `EnemyColor`는 파생 프로퍼티 |
| 턴 판정 | `IsPlayerTurn => turnColor == PlayerColor` |
| 보드 시점 | `BoardManager.ApplyBoardView()` — Grid 180도 회전 + 타일맵 `orientationMatrix`로 그림 방향 보정 |
| 턴 주체 | `TurnProvider` 추상 클래스. `AiTurnController`가 상속 |

**함께 고친 버그**

- AI가 하극상 사용 시 자기 퀸을 약화시키던 문제
- 플레이어가 흑일 때 항복 승패가 뒤집히던 문제
- 흑 플레이 시 백(AI) 선수가 첫 수를 두지 않아 대국이 시작되지 않던 문제

### 1단계 — 전송 계층 추상화

| 클래스 | 역할 |
|---|---|
| `MatchMessage` | `Move`/`Card`/`Resign` + `Sequence`(순서 강제) + `Turn` |
| `IMatchTransport` | 게임 로직이 아는 유일한 전송 계약 |
| `LoopbackMatchTransport` | Fairy Stockfish가 상대역을 맡는 검증용 구현 |
| `RemoteTurnProvider` | 수신 → `ApplyUCIMove`. 중복·역순 메시지 폐기 |
| `RemoteCardExecutor` | `AiCardId`로 카드를 찾아 선택 UI 없이 `ICard.Execute` 호출 |
| `RemoteWaitingIndicator` | 상대 차례 동안 "..." 표시 |

**설계 요점**

- 선택 UI(`PieceSelector`/`TileSelector`)는 카드를 쓰는 쪽의 로컬 UI다. 수신 측은 열지 않고
  대상을 복원해 `ICard.Execute`를 직접 부른다 — AI가 카드를 쓸 때와 같은 경로다.
- 카드 송신은 **효과 적용 전**에 한다. 가스라이팅처럼 카드가 직접 `NextTurn`을 부르면
  적용 후에는 턴 번호가 이미 넘어가 있다.
- 승격은 `MoveSelected`에서 이동만 하고 멈추므로, UCI 5번째 문자가 정해질 때까지
  착수 송신을 보류했다가 `HandlePromotion`에서 보낸다.
- 모드가 프로바이더를 고른다(`GameMode.Multiplayer` + `TurnProvider.IsRemote`).
  한 씬에 AI용·원격용을 함께 둬도 안전하다.

**함께 고친 버그**

- 턴 번호로 중복을 거르면 한 턴에 카드와 착수가 이어질 때 두 메시지의 턴이 같아
  나중에 온 착수가 폐기된다. 송신자가 매기는 `Sequence` 기준으로 변경.

### 2단계 — 실제 2인 연결

| 클래스 | 역할 |
|---|---|
| `RelayMatchTransport` | NGO 없이 Relay 위에서 UTP로 바이트만 주고받는다. JSON + 길이 프리픽스, Reliable Sequenced 파이프라인 |
| `MatchTransportKind` | 인스펙터에서 Loopback / Relay를 고른다 |
| `MppmMatchRole` | MPPM 두 인스턴스에 역할·진영을 자동 배정하고 join code를 임시 파일로 전달 (에디터 전용) |
| `MatchProfileView` | 흑을 잡으면 헤더 프로필 표시를 맞바꾼다 |

**설계 요점**

- 수신은 `Tick()`에서 드라이버를 돌려 꺼낸다. `Update`에서 도니 항상 메인 스레드라
  `UnityMainThreadDispatcher` 없이 8-2의 계약을 만족한다.
- `IMatchTransport.StartMatch`는 동기 시그니처인데 Relay 연결은 async다.
  `Idle/Connecting/Connected/Failed` 상태로 재진입을 막는다.
- **지연 연결은 루프백에서만 통한다.** 백을 잡은 쪽이 첫 수를 두기 전까지 방이 열리지 않아
  상대가 join code를 받을 수 없다. 매치 시작 시점에 미리 연다.
- 수신 메시지는 큐에 줄 세운다. 카드와 착수가 한 턴에 연달아 오는데 같은 프레임에 적용하면
  판이 두 번 튀어 알아볼 수 없다. 카드 적용 후 뒤따르는 착수를 잠시 미룬다.

**함께 고친 버그**

- **전역 카드가 상대에게 전달되지 않았다.** 기물/타일 카드는 선택 UI가 `NotifyLocalCard`를
  부르지만 전역 카드는 `CardDescPanel`에서 곧바로 실행돼 송신 훅이 없었다. 돌격·판 섞기·
  시간 역행 등 11장이 내 화면에서만 적용됐다.
- **턴 갱신 전에 도착한 원격 착수가 조용히 버려졌다.** `NextTurn`은 턴 번호를 즉시 올리지만
  이동 가능 위치는 엔진 응답 후에 갱신된다. 그 사이에 도착한 착수는 이전 턴 기준으로 판정돼
  `MovePiece`가 실패하는데, `ApplyUCIMove`가 반환값을 무시하고 턴을 넘겼다.
  → `GameManager.IsTurnStateReady`로 갱신 완료를 알리고 원격 수신부가 그때까지 대기.
  `ApplyUCIMove`의 두 실패 경로도 에러 로그로 드러냈다.
- **상대 카드에 아무 안내가 없었다.** 효과만 적용되니 기물이 제멋대로 움직이는 것처럼 보였다.
  적용 전에 토스트로 알린다(`RemoteCardExecutor.CardApplying`으로 더 큰 연출도 붙일 수 있다).
- **흑을 잡으면 헤더 프로필이 실제 진영과 어긋났다.** 슬롯 색이 씬에 고정돼 있었다.

### 3a — 초기 상태 합의 이음매

| 클래스 | 역할 |
|---|---|
| `MatchSetup` | 엔진 버전 · 카드 DB 해시 · 초기 FEN · 카드 큐. `TryValidate()`로 시작 시점 검증 |
| `GameCycleManager.MatchSetup` | 씬을 넘어 초기 상태를 나른다. `StartGame()`에서 초기화 |

주입 지점은 셋이고, `MatchSetup`이 없으면 전부 기존 동작으로 떨어져 단일 플레이는 영향이 없다.

| 지점 | 동작 |
|---|---|
| `GameManager.LoadMapManager()` | 합의된 FEN 사용, `MapManager`를 타지 않음 |
| `CardRandomizer.GenerateCard()` | 합의된 큐에서 순서대로 꺼냄 (매치 중 주기적 뽑기 포함) |
| 엘리트 변형 / ELO | 멀티에서는 **적용하지 않음** |

> 엘리트 변형을 빼는 이유: 대상이 `EnemyColor` 기준이라 두 클라이언트가 서로 반대 진영을
> 변형시킨다. 절대 색을 따로 실어도 되지만, 엘리트와 ELO는 런 전용 개념이라 원격 대전이
> 의존해서는 안 된다(8-1).

---

## 11. 3단계 남은 작업

### 3b — 핸드셰이크 송수신

호스트가 `MatchSetup`을 만들어 보내고 게스트가 `TryValidate()` 후 적용한다.

- 매치 이전 단계 메시지를 `MatchMessage`에 종류로 추가할지, 별도 경로로 태울지 정할 것.
  `MatchMessage`는 "한 턴의 행동"이라는 의미라 초기 상태를 섞으면 어색하다.
- 호스트가 카드 큐를 두 벌 뽑되 **게스트에게는 게스트 몫만** 보낸다.
- 검증 실패(엔진 버전·카드 DB 해시 불일치) 시 매치를 열지 않고 사유를 표시한다.

### 3c — 연결 시점 이동

지금은 순서가 뒤집혀 있다.

```
GameManager.Awake()        PlayerColor 읽음
GameManager.Start()        ApplyBoardView → LoadMapManager()   ← 보드가 이미 깔림
RemoteTurnProvider.Start()  이제서야 연결 시작
```

핸드셰이크 결과를 쓰려면 **보드 초기화 전에 연결이 끝나 있어야** 한다.
연결·핸드셰이크를 매치 씬 진입 전으로 옮긴다(`연결 → 색 배정 → 씬 로드`).
5단계 매칭이 붙으면 어차피 이 순서가 되므로 지금 맞춰 둔다.

### 착수 전 체크리스트

- [x] UGS 대시보드에서 프로젝트 생성 및 에디터 연결
- [x] 패키지 설치 — `com.unity.services.authentication` 3.7.4,
      `com.unity.services.multiplayer` 2.3.0, `com.unity.multiplayer.playmode` 1.6.3
      (`com.unity.transport` 2.7.3은 의존성으로 함께 들어옴)
- [x] `RelayMatchTransport : IMatchTransport` 구현 — 게임 로직 변경 없음
- [ ] **Android 엔진 커버리지 — 실기기 확인** (8-3 함정). 정적으로는 해소됐다.
      `Assets/Plugins/Android/libs/fairystockfish-release.aar`가 있고 `FairyStockfishBridge`가
      `#if UNITY_ANDROID` 분기로 JNI(`com.example.chessaiv2.FairyStockfish`)를 쓰며,
      양쪽 다 `InitEngine("chaoschess")`로 같은 variant를 넘긴다.
      **남은 확인:** PC는 `VariantPath`로 `variants.ini` 경로를 명시하는데 Android 경로에는 그 호출이 없다.
      APK 안의 StreamingAssets는 파일 경로로 못 여니, chaoschess 변형이 실기기에서 적용되는지 봐야 한다.
      **3b의 엔진 버전 검증을 붙이기 전에 확인할 것**
- [ ] `com.unity.transport`를 manifest에 명시 — 직접 `using` 하므로 MPS 의존성 변경에 대비
- [ ] 체크메이트 종료 시 양쪽 승패 일치 검증 (관련 UI 작업 후)

### 테스트 방법

MPPM으로 가상 플레이어 두 개를 띄운다. **`Assets/Scenes/MainScene.unity`에서 Play한다** —
`GameCycleManager`가 이 씬에만 있고, 여기서 모드와 진영이 정해진 뒤 `DontDestroyOnLoad`로
`MainGameScene`까지 넘어간다. `MainGameScene`을 직접 Play하면 모드가 `Run`으로 떨어져
`AiTurnController`가 잡힌다.

MPPM 가상 플레이어는 씬 에셋을 공유해서 인스펙터 값으로는 두 인스턴스를 구분할 수 없다.
`MppmMatchRole`이 `CurrentPlayer.IsMainEditor`로 갈라낸다.

| 인스턴스 | 역할 | 진영 |
|---|---|---|
| 메인 에디터 | Host | 백 |
| 클론(Player 2) | Guest | 흑 |

join code는 UI가 없으므로 호스트가 OS 임시 폴더에 파일로 남기고 게스트가 읽는다.

**설정**

- `MainScene` → `GameCycleManager.debugMultiplayerMode` 체크
- `MainGameScene` → `RemoteTurnProvider.transportKind = Relay`

**주의: 1층 일반 노드로만 테스트할 것.**
`MapManager.SelectFEN()`은 보스 층(2·5층)에서만 FEN을 랜덤으로 고르고 그 외에는 `DefaultFEN`을
돌려준다. 맵 그래프는 클라이언트마다 랜덤이라 같은 노드를 고를 수 없으므로, 보스 층에 가면
양쪽 초기 판이 달라져 로크스텝이 깨진다. 카드 풀이 양쪽에서 다른 것도 같은 이유다.

**3a로 이음매는 만들었지만 아직 채우는 쪽(3b)이 없어 이 제약은 그대로다.**
3b가 붙으면 초기 판과 카드 큐를 호스트가 내려주므로 노드를 맞출 필요가 없어진다.

---

## 12. 정리해야 할 임시 코드

| 대상 | 내용 |
|---|---|
| `MppmMatchRole` | MPPM 두 인스턴스에 역할·진영을 자동 배정하고 join code를 임시 파일로 주고받는 **에디터 전용 테스트 보조**. 연결 UI가 붙으면 파일째 제거 |
| `RemoteTurnProvider.relayRole` / `relayJoinCode` | 에디터 밖(빌드) 폴백용 인스펙터 값. 연결 UI가 붙으면 제거 |
| `GameCycleManager.DefaultPlayerColor`의 멀티 분기 | 역할에서 진영을 파생시키는 우회. 매칭이 붙으면 서버가 배정한 색을 `SetPlayerColor`로 넣는다 |
| 호스트 권위 (`MatchSetup`을 호스트가 결정) | 서버가 없어서 택한 임시 구조. 호스트가 자기 유리하게 카드를 뽑을 수 있다. 6단계 서버 검증에서 대체 |
| `MatchTransportKind`가 씬에 `Relay`로 고정 | 2단계 검증 설정. 브랜치를 받으면 곧바로 Relay로 진입한다 |
| `MatchMessage.CreateResign` | 수신부만 있고 **송신하는 곳이 없다.** 항복 UI 자체가 아직 없어 죽은 경로다. 4단계에서 이탈 처리와 함께 붙일 것 |
| `GameCycleManager.debugPlayAsBlack` | 흑 플레이 검증용. 실제 매칭이 붙으면 제거 |
| `GameCycleManager.debugMultiplayerMode` | 멀티 모드 진입용. 매칭이 붙으면 제거 |
| `GameManager.AiAutoMoveEnabled` | 이름과 역할이 어긋났다. "상대 턴 자동 진행"과 "로컬 입력 제한"이 한 변수에 묶여 있어, 카드 랩에서 원격 테스트를 하려면 분리해야 한다 |
| `CardDataSO`의 `[FormerlySerializedAs]` | 카드 `.asset` 53개가 전부 재직렬화되기 전에는 제거 금지. 떼면 아직 저장 안 된 카드의 대상 진영이 `Self`로 리셋된다 |
