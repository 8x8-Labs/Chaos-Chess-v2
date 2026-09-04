# 멀티플레이 대전 모드 설계 문서

- **작성일:** 2026-08-09 (최종 갱신 2026-09-04)
- **상태:** **3.5단계 연결 UI 구현·검증 완료 / 다음은 프로필 표시 → 4단계(시계·이탈)**
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
| **3** | 서버 시드/카드 배분 + 매치 시작 핸드셰이크(엔진 버전·카드 DB 해시 검증) | ✅ **구현·검증 완료** | 3a·3b·3c 전부 구현·검증됨. 현황은 10절, 검증 기록은 11-8 |
| **3.5** | **연결 UI + 프로필 표시** | 🔶 진행 중 | 연결 UI는 ✅ 구현·검증 완료(2026-09-04). 프로필 표시는 ⬜. 원래 5단계에 묶여 있었으나 앞당김, 아래 참고 |
| **4** | 체스 시계 + 이탈/재접속 처리 | ⬜ | |
| **5** | Cloud Code 레이팅 + Leaderboards + 티어 UI | ⬜ | |
| **6** | (선택) 서버 측 수 검증 | ⬜ | 랭크가 실질적 의미를 갖기 시작하면 |

> **2026-08-19 — 3.5단계를 새로 끼워 넣었다.**
> 연결 UI와 프로필 표시는 원래 5단계(매칭)의 일부였다. 그런데 지금은 join code를 임시 파일로
> 주고받는 MPPM 전용 경로뿐이라 **에디터 밖에서는 대전을 시작할 방법 자체가 없다.**
> 매칭 서버를 붙이기 전에 사람이 방을 만들고 들어갈 수단이 먼저 필요하다.
> 3b의 핸드셰이크가 상태(`MatchSessionState`)와 실패 사유를 이미 내놓으므로 얹을 자리도 마련됐다.

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

> **2026-08-17 해소.** Android는 `fairystockfish-release.aar`를 JNI로 쓰고,
> `chaoschess` 변형이 그 라이브러리에 내장돼 있다. PC처럼 `variants.ini`를 파일 경로로
> 읽힐 필요가 없으므로 APK 안의 StreamingAssets 문제도 발생하지 않는다.

### 8-4. 드라이버가 멈추면 Relay가 끊는다

`NetworkDriver`는 `MatchSession.Update`에서만 돈다. **메인 스레드가 멈추면 Relay 핑도 멈추고**,
Relay는 조용한 쪽을 잘라낸다.

```
Received error message from Relay: player timed out due to inactivity.
Relay allocation is invalid.
[Relay] 연결되지 않아 전송하지 못했습니다: #1 [T1] Move d2d4
```

**3c가 새로 드러낸 문제다.** 이전에는 연결이 매치 씬 안에서, 즉 무거운 초기화가 **끝난 뒤에**
열렸다. 연결을 씬 밖으로 옮기면서 그 구간이 연결 안으로 들어왔다.

멈추는 원인은 둘이었다.

#### (1) 창 포커스 — MPPM의 함정

`Run In Background`가 꺼져 있으면 포커스를 잃은 인스턴스는 플레이어 루프가 멈춘다.
**MPPM 테스트는 두 창을 번갈아 조작해야 하므로 한쪽은 항상 멈춰 있다.**
창을 10초 넘게 만지면 그쪽 연결이 죽는다.

**대응 — 세션이 열려 있는 동안만 런타임에 켠다.**
`MatchSession.Begin()`이 `Application.runInBackground = true`로 바꾸고,
`EndMatch()`에서 원래 값으로 되돌린다.

> **프로젝트 설정(Player → Run In Background)은 켜지 않는다.**
> `ProjectSettings.asset`의 `runInBackground`는 플랫폼 공통 값이라 Android 빌드까지 따라간다.
> 모바일이 백그라운드에서 계속 도는 것은 배터리 낭비이고, 애초에 OS가 앱을 재우므로 얻는 것도 없다.
> 필요한 것은 "원격 대전 중인 데스크톱 창"뿐이라 범위를 거기에 맞춘다.

모바일에서 대국 중 앱이 백그라운드로 가면 OS가 재우므로 연결은 어차피 끊긴다.
그쪽은 이 설정으로 풀 문제가 아니라 **4단계의 이탈·재접속 처리**가 받아야 한다.

#### (2) 엔진의 동기 호출

`FairyStockfishBridge`의 초기화와 조회는 **메인 스레드를 블로킹한다.**

| 호출 | 최대 대기 |
|---|---|
| `InitEngine` — 프로세스 기동 + `readyok` | 5000ms |
| `GetLegalMoves` — `isready` + `go perft 1` | 3000 + 8000ms |
| `IsInCheck` — `isready` + `Checkers:` | 3000 + 3000ms |

전부 `Thread.Sleep(10)` 루프다. `GameManager.Start()`가 매치 씬 첫 프레임에 이것들을
연달아 부르므로, 그 프레임 동안 드라이버가 멈춘다.

**대응**

- `InitEngine`에 **재사용 가드**를 넣었다. 같은 변형으로 살아 있으면 다시 띄우지 않는다.
  (매치마다 새 프로세스를 띄우고 이전 것을 정리하지 않던 누수도 함께 사라진다.)
- `MatchSession.Begin()`이 **연결보다 먼저** 엔진을 깨운다. 매치 씬의 `InitEngine`은
  재사용으로 떨어져 멈추지 않는다.
- `WaitForOutput`이 500ms를 넘기거나 타임아웃까지 가면 경고를 남긴다.
  전에는 타임아웃이 조용히 부분 결과를 돌려줘 흔적이 없었다.

> 근본 해결은 엔진 조회를 전부 비동기로 바꾸는 것이다. 매치 계층 전반을 건드리는 일이라
> 여기서는 하지 않았다. 4단계(시계·이탈 처리)에서 재접속을 붙일 때 다시 볼 것.

#### 진단

`MatchSession`이 프레임 간격을 재서 2초 이상 끊기면 씬 이름·세션 상태와 함께 에러를 남긴다.
Relay 오류는 끊긴 **결과**만 보여주므로, 원인을 보려면 이 로그를 먼저 본다.

| 로그 | 읽는 법 |
|---|---|
| 스톨만 뜨고 `[Fairy]` 경고 없음 | 포커스 전환이거나 씬 활성화 프레임 |
| 직전에 `[Fairy] '...' 대기에 N ms` | 엔진 동기 호출이 원인 |
| `[Fairy] PC 프로세스 초기화 성공`이 매치마다 반복 | 재사용 가드가 안 먹고 있다 |

---

## 9. 다음 액션

3단계(3a·3b·3c)는 구현과 검증이 모두 끝났다(2026-08-25).
**연결 UI(3.5단계 전반부)도 구현·검증이 끝났다(2026-09-04).** 다음은 프로필 표시다.

### 9-1. 검증 (완료 — 2026-08-25)

11-8 시나리오 중 정상 대국·카드 DB 불일치·체크메이트 승패 표시 세 가지를 MPPM으로 확인했다.
**검증 과정에서 버그 두 개를 새로 찾아 고쳤다.** 자세한 내용은 11-8 하단 참고.

- 체크메이트/항복 결과가 흑 플레이어(멀티 게스트 포함)에게 뒤집혀 표시되던 문제
- 핸드셰이크가 끝나기 전에 맵 노드를 눌러 대국 씬에 들어갈 수 있던 문제

### 9-2. 연결 UI (3.5단계)

지금은 방을 만들고 들어가는 수단이 MPPM 전용 임시 파일뿐이라 **에디터 밖에서는 대전을
시작할 수 없다.** 매칭 서버(5단계)를 붙이기 전에 사람이 직접 방을 여닫을 UI가 필요하다.

| 화면 | 내용 |
|---|---|
| 진입 | "방 만들기" / "코드로 참가" 선택 |
| 호스트 | 발급된 join code 표시(`MatchSession.HostJoinCode`) + 복사 |
| 게스트 | join code 입력 |
| 대기 | `MatchSessionState`에 따른 진행 표시 (`Connecting` / `Handshaking`) |
| 실패 | `MatchSession.FailureReason` 표시 후 되돌아가기 |

**필요한 코드 쪽 정리**

- [x] `MatchSessionConfig.Role` / `JoinCode`(구 `FallbackRole`/`FallbackJoinCode`) — UI가 정한 값을 받는 실제 입력 통로로 이름을 바꿨다. `GameCycleManager`의 대응 필드도 `multiplayerRole`/`multiplayerJoinCode`로 개명. `ExplicitRole` 플래그로 "UI가 정함"과 "인스펙터 기본값(디버그/빌드 폴백)"을 구분한다.
- [x] `MppmMatchRole`은 에디터 테스트 보조로만 남기고, `ExplicitRole`이 true면(연결 UI 경유) 우선순위와 join-code 파일 교환 경로를 모두 건너뛰도록 뒤집었다.
- [ ] `GameCycleManager.debugMultiplayerMode` / `debugPlayAsBlack`은 아직 안 지웠다. UI 없이 인스펙터 토글만으로 빠르게 테스트할 때 계속 쓸모가 있어 의도적으로 남겨 둠. `StartMultiplayerAsHost/AsGuest`는 이 토글과 무관하게 동작한다.

> ⚠️ **씬·프리팹 작업이 필요하다.** Canvas·버튼·InputField 배치는 에디터에서 직접 해야 하고,
> 코드 쪽은 상태 구독과 입력 처리까지만 맡는다. 분업이 전제다.

#### 구현 결과 (2026-09-04 — 완료)

`MainScene`에 캔버스 4개를 얹었다. UI 프레임워크 쪽 상세와 배치 표는 [UI-System.md](UI-System.md) 7절.

| 캔버스 | 역할 | 버튼 |
|---|---|---|
| `MultiCanvas` | 진입 (`MatchConnectUI`도 여기 붙어 있다) | `HostMultiplayer`→`HostCanvas` / `ChangeCanvas`→`GuestCanvas` / 뒤로 |
| `HostCanvas` | 호스트 대기 — join code 표시 | `CopyMultiplayerJoinCode` / `CancelMultiplayerConnect` |
| `GuestCanvas` | 게스트 입력+대기 — `TMP_InputField` | `JoinMultiplayer`(`joinCodeInput` 연결) / `CancelMultiplayerConnect` |
| `FailCanvas` | 실패 사유 표시 | `CancelMultiplayerConnect` |

흐름은 `MenuCanvas → MultiCanvas → Host/GuestCanvas → (Ready) StartRewardCanvas → MapScene`.
`Ready`에서 화면을 넘기는 것은 `MatchConnectUI.matchReadyCanvas`가 맡는다 — `GameCycleManager`는
맵 **데이터**만 다시 만들 뿐 화면을 넘기지 않고, 맵은 `MainScene`의 캔버스가 아니라 별도 `MapScene`이다.

**배치 중 걸린 것 (같은 실수 방지용)**

- 장식 이미지(`Outline`, `LetterBox`)가 `TMP_InputField`의 **형제**로 위에 덮여 있어 입력이 안 먹었다.
  자식이면 이벤트가 부모로 올라가 버튼은 정상 동작하지만, 형제면 아무도 처리하지 않고 끝난다.
  → 장식 이미지의 `Raycast Target`을 끈다.
- `debugMultiplayerMode`가 켜져 있으면 단일 플레이 `GameStart`까지 멀티로 빠진다. UI로 진입하는
  지금은 꺼 두어야 한다(`StartMultiplayerAsHost/AsGuest`는 이 토글과 무관하게 동작한다).

**남은 표시 다듬기 (동작에는 지장 없음)**

- 대기 화면에 상태 표시 전용 텍스트가 없어 `hostStatusText`/`guestStatusText`가 버튼 라벨을
  가리키고 있다. 연결이 시작되면 "클릭하여 복사하기" 같은 안내 문구가 상태 문구로 덮인다.
- `hostJoinCodeText`는 매 프레임 코드 값으로 통째 치환되므로 "코드번호 | " 접두사가 사라진다.

### 9-3. 프로필 취득·표시

`MatchProfile`은 3b에서 이미 왕복에 실려 오간다. 지금은 이름만, 그것도 GPGS가 있을 때만 채워진다.

| 작업 | 내용 |
|---|---|
| 아바타 URL 취득 | `GooglePlayAuthManager`에 `GetUserImageUrl()` 캐싱 추가 |
| 아바타 로드 | URL → `UnityWebRequestTexture` → `Sprite`, 세션 캐시 |
| 표시 | `MatchProfileView`에 이름·이미지 슬롯 추가, `MatchSession.RemoteProfile` 구독 |

**제약**

- GPGS는 `#if UNITY_ANDROID && !UNITY_EDITOR` 안에만 있어 **에디터에서는 프로필을 가져올 수 없다.**
  실제 확인은 실기기 2대로만 가능하고, 그전까지는 `MatchProfile.CreateFallback`이 쓰인다.
- 아바타 URL이 상대 기기에서 열리는지는 정적으로 확인할 수 없다. 실기 확인 항목.
- `Docs/Google-Account-Features.md` 3.4가 같은 작업을 다룬다. 그 문서는 7절에서 멀티플레이를
  범위 밖으로 두고 있으므로, **아바타 취득은 그쪽, 교환·표시는 이쪽**으로 갈라 둔다.

---

## 10. 구현 현황 (0·1·2·3단계)

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
  > **예외가 하나 있었다 — 여러 단계로 대상을 고르는 카드.** 텔레포트는 `Execute`가 1단계에서
  > `LoadTileSelector()`를 불러 스스로 UI를 연다. 수신 측이 그 `Execute`를 부르면 **상대 화면에
  > 선택 UI가 열린다.** `CardEffectArgs.TargetsPreselected`로 UI를 건너뛰게 했다. 아래 참조.
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

### 3b — 초기 상태 합의 (제어 채널 + 핸드셰이크)

| 클래스 | 역할 |
|---|---|
| `MatchChannel` / `MatchControlMessage` | 채널 상수 + `Setup`/`SetupAck`. 행동과 섞지 않는다 |
| `MatchSetupFactory` | 호스트가 `MatchSetup` 두 벌을 만든다. 초기 FEN 상수, 카드 큐 64장 블록 셔플 |
| `MatchProfile` | 표시용 이름·아바타 URL. Setup/Ack 양쪽에 동봉해 왕복 한 번으로 교환 |

**설계 요점**

- **와이어에 채널 바이트를 둔다.** `[byte channel][ushort length][payload]`.
  행동은 `RemoteTurnProvider`가 `Sequence` 필터를 태워 받고 제어는 `MatchSession`이 받는다.
  한 타입에 섞으면 순서 체계가 다른 둘이 같은 필터를 타게 된다.
- **카드 큐는 두 벌을 따로 뽑는다.** 한 벌을 나눠 쓰거나 시드만 내려주면 상대 손패를 계산할 수 있다.
- **큐 생성은 블록 셔플.** 전체 id를 섞은 블록을 이어 붙인다. 매번 독립적으로 뽑으면 같은 카드가
  연달아 나오는데, 정하는 쪽은 받는 쪽 손패를 몰라 기존 중복 회피를 할 수 없다.
- **프로필은 `MatchSetup`에 넣지 않는다.** 규칙 합의는 불일치 시 매치를 거부하는 대상인데
  프로필은 달라야 정상이다. 검증 대상과 표시 대상을 섞으면 안 된다.
- **실패는 몇 틱 미룬다.** 거부 `Ack`는 다음 드라이버 갱신에서야 나가는데 곧바로 `StopMatch`하면
  드라이버가 Dispose되어 **사유가 상대에게 도착하지 못한다.** 상대는 15초 타임아웃만 보게 된다.
- **`Fail`은 연결만 정리하고 상태는 `Failed`로 남긴다.** `EndMatch`처럼 `Idle`로 되돌리면
  표시할 사유가 사라진다.
- 진입은 `Ready`를 기다린다. `GameCycleManager`가 `StateChanged`를 구독해 합의 후 `MapManager.Init()`.
  루프백은 `Begin` 안에서 이미 `Ready`까지 가므로 기다리지 않는다.

> **테스트 제약이 풀렸다.** 초기 판과 카드 큐를 호스트가 내려주므로 **양쪽이 서로 다른 노드를
> 골라도 같은 판에서 시작한다.** 보스 층 제약도 사라진다.

---

### 3c — 연결 소유권을 씬 밖으로

| 클래스 | 역할 |
|---|---|
| `MatchSession` | `DontDestroyOnLoad` 싱글턴. 트랜스포트 생성·연결 시작·join code 대기·`Tick()` 펌핑·미구독 메시지 버퍼. 상태는 `Idle → Connecting → Handshaking → Ready → Failed` |
| `MatchSessionConfig` | 전송 종류·로컬 진영·빌드용 폴백 역할/join code. `GameCycleManager`가 채워 넘긴다 |

`RemoteTurnProvider`는 연결을 소유하지 않고 세션에서 통로를 빌려 **구독만** 한다.
메시지를 보드에 반영하는 일은 그대로 남는다.

| 책임 | 전 | 후 |
|---|---|---|
| 트랜스포트 생성·연결 시작 | `RemoteTurnProvider.Awake/Start` (매치 씬) | `MatchSession.Begin()` (MainScene, `GameCycleManager.StartGame`) |
| join code 대기 · `Tick()` | `RemoteTurnProvider.Update` | `MatchSession.Update` |
| 종료 | `RemoteTurnProvider.OnDestroy` (전송 직접 정리) | `MatchSession.EndMatch()` (프로바이더가 호출) |
| 인스펙터 설정 | `MainGameScene`의 `RemoteTurnProvider` | `MainScene`의 `GameCycleManager` |

**설계 요점**

- **구독자가 없는 동안 도착한 메시지를 버퍼링한다.** 씬 로드 중에는 `RemoteTurnProvider`가 없다.
  호스트가 백이면 첫 수를 게스트의 씬 로드 중에 보낼 수 있는데, 예전 구조에서는 그 메시지가
  그냥 사라졌다. 세션이 큐에 담아 두었다가 구독 시점에 순서대로 흘려준다.
- **구독을 세션이 중개한다.** `StopMatch()`가 `MessageReceived = null`로 구독을 통째로 날리던
  문제가 여기서 정리된다. 프로바이더는 트랜스포트가 아니라 세션에 붙는다.
- **세션은 런타임에 생성한다.** 씬에 오브젝트를 추가하지 않아도 되도록 `EnsureInstance()`가
  `DontDestroyOnLoad` 오브젝트를 만든다.
- 3c 단계에서는 **연결을 열어 두기만 하고 진입을 막지 않는다.** 핸드셰이크 완료를 기다렸다가
  맵을 초기화하는 것(11-6)은 3b에서 붙인다.

**함께 고친 문제**

- `MainGameScene`을 직접 Play하면 모드가 `Run`인데도 씬에 있는 `RemoteTurnProvider`가
  `Awake`에서 Relay 연결을 열었다. 이제 세션이 없으면 프로바이더는 아무것도 하지 않는다.
- **엔진 기동이 연결을 죽였다.** 연결을 씬 밖으로 옮기자 `GameManager.Start()`의 블로킹
  엔진 초기화가 연결된 구간 안으로 들어와 Relay가 이쪽을 비활성으로 끊었다. 상세는 8-4.
- **게스트가 join code를 영영 못 받았다.** `MppmMatchRole.SessionStartUtc`가 static 필드
  초기화 시점(= 그 인스턴스가 클래스를 처음 건드린 때)으로 잡혀 있었다. 방을 여는 시점이
  매치 씬에서 MainScene으로 당겨지면서, 호스트가 먼저 시작하면 게스트 쪽 기준 시각이 파일보다
  나중이 되어 **이번 판의 코드를 지난 판의 잔재로 오인해 무시했다.**
  `[RuntimeInitializeOnLoadMethod]`로 기준을 Play 시작 시점에 못박았다.

### 다단계 선택 카드 (텔레포트)

`TeleportCard.Execute`는 1단계에서 `LoadTileSelector()`를 불러 **자기가 선택 UI를 연다.**
원격 적용은 `Execute`를 직접 부르므로 상대 화면에 그 UI가 열렸고, `PieceSelector`와
`TileSelector`가 각각 알리는 바람에 **메시지도 두 번** 갔다. 받는 쪽은 카드 종류 하나로만
좌표를 해석하니 두 번째 메시지의 타일 좌표를 기물로 찾다가 실패했다.

| 지점 | 조치 |
|---|---|
| `CardEffectArgs.TargetsPreselected` | "대상이 이미 다 정해졌다"는 표시. `RemoteCardExecutor`가 켠다 |
| `TeleportCard.Execute` | 그 경우 UI를 건너뛰고 기물+목표 칸을 한 번에 처리 |
| `GameManager.NotifyLocalCard` | `Type == Piece && TileCount > 0`이면 1단계를 보류했다가 2단계에서 **기물 좌표 뒤에 타일 좌표를 이어 붙여** 한 번만 보낸다. 단계 구분은 `CardSelectionState.CurrentOwner`로 한다 |
| `RemoteCardExecutor.TryBuildArgs` | 앞 `RequiredPieceCount`개는 기물, 나머지는 타일로 가른다 |

`TeleportCard.pieceSelected`가 프리팹 컴포넌트에 남아 다음 사용까지 이어지던 것도 함께 고쳤다.

> **AI 경로는 확인하지 않았다.** AI가 텔레포트를 쓸 때도 `TargetsPreselected` 없이
> `Execute`를 부르면 같은 이유로 플레이어 화면에 선택 UI가 열린다. 원격 대전과 무관한
> 기존 단일 플레이 경로라 이번 범위에서 제외했다.

---

## 11. 3단계 설계 근거 (3b·3c)

> **3단계는 구현과 검증이 모두 끝났다(2026-08-25).** 아래는 그 근거와 결과를 남긴 기록이다.
> 검증 결과와 그 과정에서 찾은 버그는 11-8에 있고, 다음 할 일은 9절에 있다.

### 11-0. 순서를 뒤집는다 — 3c를 먼저

원래는 3b(핸드셰이크) → 3c(연결 시점 이동) 순서로 적었지만, **의존이 반대다.**

```
GameManager.Awake()         GameCycleManager.PlayerColor 읽음
GameManager.Start()         ApplyBoardView → LoadMapManager()   ← MatchSetup.InitialFen을 여기서 쓴다
Player.Start()              초기 카드 4장 지급                   ← MatchSetup.CardQueue를 여기서 쓴다
RemoteTurnProvider.Start()  이제서야 연결 시작                   ← 핸드셰이크가 시작조차 안 된 시점
```

게스트가 받은 `MatchSetup`은 **보드가 깔리기 전에 `GameCycleManager`에 들어가 있어야** 한다.
연결이 매치 씬 안에서 시작되는 한 3b는 성립할 수 없으므로, 소유권을 씬 밖으로 옮기는 3c를 먼저 하고
그 위에 3b를 얹는다. 5단계 매칭이 붙으면 어차피 `연결 → 색 배정 → 씬 로드` 순서가 되므로
지금 맞춰 두는 편이 낫다.

---

### 11-1. 소유권 이동 — `MatchSession` (3c)

연결의 수명이 **매치 씬보다 길어야** 하므로, 트랜스포트를 씬 오브젝트가 아니라
`GameCycleManager`와 같은 `DontDestroyOnLoad` 오브젝트가 들고 있게 한다.

| 책임 | 지금 | 바꾼 뒤 |
|---|---|---|
| 트랜스포트 생성 | `RemoteTurnProvider.Awake` | `MatchSession` |
| 연결 시작 | `RemoteTurnProvider.Start` (매치 씬) | `MatchSession.Begin()` (MainScene) |
| join code 대기 | `RemoteTurnProvider.Update` | `MatchSession` |
| `Tick()` 펌핑 | `RemoteTurnProvider.Update` | `MatchSession.Update` |
| 종료 | `RemoteTurnProvider.OnDestroy` | 매치 종료 시 `MatchSession` |
| 메시지 적용 | `RemoteTurnProvider` | 그대로 (`MatchSession.Transport`를 빌려 구독) |

```
MatchSessionState : Idle → Connecting → Handshaking → Ready → Failed
```

**주의할 점 두 가지.**

- **`Tick()`을 세션이 돌린다.** 지금은 `RemoteTurnProvider.Update`가 유일한 펌프라서
  매치 씬 밖에서는 수신이 멈춘다. 핸드셰이크는 매치 씬 밖에서 오가므로 세션이 돌려야 한다.
- **구독자가 없는 동안 도착한 Action 메시지를 버퍼링한다.** 씬 로드 중에는 `RemoteTurnProvider`가
  아직 없다. 호스트가 백이면 첫 수를 게스트보다 먼저 둘 수 있고, 그 메시지가 게스트의 씬 로드
  중에 도착하면 지금 구조에서는 그냥 사라진다. 세션이 큐에 담아 두었다가 구독 시점에 흘려준다.
  (`RelayMatchTransport.StopMatch`가 `MessageReceived = null`로 구독을 통째로 날리는 것도
  세션 소유로 바뀌면서 정리된다.)

---

### 11-2. 제어 메시지 채널 (3b)

**결정: `MatchMessage`에 섞지 않고 별도 타입 + 전송 프레임의 채널 바이트로 가른다.**
`MatchMessage`는 "한 턴의 행동"이고 `Sequence` 기반 중복 필터를 타는데, 핸드셰이크는
행동도 아니고 순서 체계도 다르며 수신자도 다르다(`MatchSession` vs `RemoteTurnProvider`).

**와이어 프레임**

```
[byte channel][ushort length][payload json]
   0 = Action  → MatchMessage
   1 = Control → MatchControlMessage
```

`MaxPayloadBytes` 1024 → **2048**. 카드 큐 64개 id + 해시 64자 + FEN까지 700B 안팎이라
1024도 아슬아슬하게 들어가지만 여유를 둔다. (제어 메시지를 `MatchMessage` 안에 JSON 문자열로
중첩하지 않는 이유이기도 하다 — `JsonUtility`가 따옴표를 이스케이프하면서 크기가 배로 뛴다.)

```csharp
public enum MatchControlKind { Setup = 0, SetupAck = 1 }

[Serializable]
public class MatchControlMessage
{
    public MatchControlKind Kind;
    public MatchSetup Setup;   // Setup일 때
    public bool Accepted;      // SetupAck일 때
    public string Reason;      // 거부 사유
}
```

**`IMatchTransport` 추가분**

```csharp
void SendControl(MatchControlMessage message);
event Action<MatchControlMessage> ControlReceived;
event Action Connected;        // 핸드셰이크를 시작할 시점 신호
```

`Connected`가 따로 필요한 이유: 지금은 연결 성립이 `RelayMatchTransport` 내부 상태로만 남고
밖으로 알려지지 않는다. `IsConnected`를 폴링해도 되지만 핸드셰이크 시작은 한 번뿐인 사건이라
이벤트가 맞다.

**루프백 구현.** 상대가 로컬 엔진이라 합의할 대상이 없다. `SendControl`은 no-op,
`Connected`는 `StartMatch` 직후 즉시 발행하고, `MatchSession`은 Loopback이면 핸드셰이크를
건너뛰고 바로 `Ready`로 간다. `MatchSetup`은 null로 남아 기존 단일 플레이 경로로 떨어진다.

---

### 11-3. 핸드셰이크 시퀀스

```
호스트                                          게스트
  │                                              │
  ├──────────── Relay 연결 성립 ──────────────────┤
  │                                              │
MatchSetupFactory.Build()                        │
  ├─ Control{Setup, 게스트 몫 큐} ───────────────►│
  │                                          TryValidate()
  │                                              │
  │◄──────────── Control{SetupAck, ok} ──────────┤
  │                                        SetMatchSetup(받은 것)
SetMatchSetup(호스트 몫)                          │
SetPlayerColor(White)                      SetPlayerColor(Black)
  │                                              │
  └──────────── 매치 씬 로드 ────────────────────┘
```

**Ack가 필요한 이유.** 불일치를 감지할 수 있는 쪽은 **게스트뿐**이다. Ack 없이 호스트가 먼저
씬을 열면 호스트만 대국 화면에 들어가 오지 않을 상대를 영영 기다린다.

**타임아웃.** `HandshakeTimeoutSeconds = 15f`. 연결 성립·Setup 수신·Ack 수신 각 구간에 건다.
초과하면 `Failed(사유)`.

**실패 처리.** 양쪽 `StopMatch()` → 씬을 로드하지 않고 사유를 표시 → MainScene에 머문다.
표시는 최소한으로 간다(로그 + 기존 팝업 재사용). 전용 연결 UI는 5단계 매칭의 몫이다.

| 실패 | 사유 문구 출처 |
|---|---|
| 엔진 버전 불일치 | `MatchSetup.TryValidate` |
| 카드 DB 해시 불일치 | `MatchSetup.TryValidate` |
| 초기 FEN 비어 있음 | `MatchSetup.TryValidate` |
| Relay 연결 실패 | `RelayMatchTransport.ConnectionFailed` |
| 15초 무응답 | `MatchSession` |

---

### 11-4. `MatchSetupFactory` — 호스트가 정하는 값

| 항목 | 규칙 |
|---|---|
| `InitialFen` | 멀티 전용 시작 FEN **상수**. 지금은 표준 초기 배치 하나. `MapManager.DefaultFEN`을 읽지 않는다 — 멀티가 런 전용 상태에 의존하면 안 된다(8-1) |
| `EngineVersion` | `ChaosChessAiVersion.Version` |
| `CardDatabaseHash` | `MatchSetup.ComputeCardDatabaseHash()` (3a에서 구현됨) |
| `CardQueue` | 아래 |

**카드 큐**

- **소스는 `CardRandomizerManager.AllCards` 중 `AiCardId`가 있는 카드 전부.**
  멀티에는 런 카드풀 개념이 없다. `StartGame()`이 `PlayerState.InitializeRun()`으로 `CardPool`을
  비우므로, 지금 멀티 테스트에서 카드가 나오는 **유일한 경로가 이 큐다.**
- 길이 `CardQueueLength = 64`. 초기 지급 4장(`DefaultMaxCardCount`) + 5턴(`DefaultCardInterval`)마다
  1장이므로 300턴 분량이다. 바닥나면 `CardRandomizer`가 경고만 남기고 더 주지 않는다(3a 구현).
- **생성은 블록 셔플.** 전체 id 목록을 셔플해 이어 붙인다. 한 블록(=DB 크기) 안에서는 중복이 없다.
  매번 독립적으로 뽑으면 같은 카드가 연달아 나온다 — 기존 `GetRandomCardsFromPool`은 손패와의
  중복을 피하지만, 호스트는 게스트의 손패 상태를 알 수 없어 같은 보장을 할 수 없다.
- **두 벌을 뽑아 호스트 몫은 로컬 `SetMatchSetup`, 게스트 몫만 전송.**
  상대가 무슨 카드를 쥐게 될지 미리 알 수 없게 하기 위해서다(5절이 시드 방식을 버린 이유).

---

### 11-5. 진영 배정

지금처럼 `MppmMatchRole`(호스트=백)에서 파생시키되, **호출 지점만 `MatchSession`으로 모은다.**
핸드셰이크가 끝난 뒤 세션이 `GameCycleManager.SetPlayerColor()`를 부른다.
5단계에서 서버 배정으로 바뀔 때 이 한 줄만 갈아끼우면 된다.

색을 `MatchSetup`에 실을 필요는 없다. 양쪽이 같은 규칙(역할 → 색)을 쓰기 때문이다.
서버가 색을 정하는 5단계에서 `MatchSetup`에 넣는다.

---

### 11-6. 진입 흐름

```
StartGame()
  CurrentMode = ResolveMode(Run)
  멀티면:
     MatchSession.Begin()                   // 연결 → 핸드셰이크
     Ready  → SetPlayerColor / SetMatchSetup → MapManager.Init()
     Failed → 사유 표시, 진입 취소
  아니면 지금 그대로
```

맵 노드 선택은 **그대로 둔다.** 초기 판을 `MatchSetup`이 정하므로 양쪽이 서로 다른 노드를 골라도
판이 갈리지 않는다 — 아래 테스트 제약(1층 일반 노드 한정)이 여기서 풀린다.
맵을 완전히 걷어내는 것은 5단계 매칭 UI의 몫이다.

---

### 11-7. 파일별 작업 목록

**신규**

| 파일 | 내용 | 상태 |
|---|---|---|
| `Multiplayer/MatchSession.cs` | 트랜스포트 소유, 연결·핸드셰이크 상태 기계, 씬 밖 `Tick()`, 미구독 메시지 버퍼 | ✅ |
| `Multiplayer/MatchControlMessage.cs` | `MatchControlKind` + `MatchControlMessage` + 채널 상수 | ✅ |
| `Multiplayer/MatchSetupFactory.cs` | 호스트가 `MatchSetup` 두 벌을 만든다 | ✅ |

**수정**

| 파일 | 내용 | 상태 |
|---|---|---|
| `IMatchTransport.cs` | `SendControl` / `ControlReceived` / `Connected` 추가 | ✅ |
| `RelayMatchTransport.cs` | 채널 바이트 프레이밍, `Connected` 발행, `MaxPayloadBytes` 2048 | ✅ |
| `LoopbackMatchTransport.cs` | 제어 no-op, `Connected` 즉시 발행 | ✅ |
| `RemoteTurnProvider.cs` | 트랜스포트 생성·연결·join code 대기·`Tick` 제거. 세션에서 빌려 구독만 한다 | ✅ |
| `GameCycleManager.cs` | `StartGame` 멀티 분기 + 핸드셰이크 완료 후 맵 초기화 | ✅ |
| `MppmMatchRole.cs` | 파일은 유지, 호출부만 `MatchSession`으로 이동 | ✅ |

**작업 순서**

1. ✅ `MatchSession`으로 소유권 이동(3c). 이 단계까지는 루프백·Relay 모두 **기존과 똑같이** 동작해야 한다.
2. ✅ 제어 채널 프레이밍 추가. 아직 아무도 제어 메시지를 안 보내므로 회귀만 확인한다.
3. ✅ `Setup`/`SetupAck` 왕복 + `MatchSetupFactory`(3b).
4. ✅ 실패 사유 표시 — 로그 + 토스트로 먼저 붙였고, 3.5단계에서 전용 실패 화면(`FailCanvas`)까지 얹었다(2026-09-04).
   토스트는 인스펙터 디버그 경로(연결 UI를 안 거치는 경우)에서 여전히 유일한 표시 수단이라 남겨 둔다.

각 단계가 독립적으로 검증 가능하도록 잘랐다. 3을 먼저 짜면 1의 회귀와 3의 버그가 섞인다.

---

### 11-8. 검증 시나리오

| 시나리오 | 기대 | 결과 |
|---|---|---|
| 정상 대국 | 양쪽 손패가 **같은 순서로** 나오고, 초기 판이 같다 | ✅ 2026-08-25 통과 |
| 카드 DB 불일치 | 한쪽 `AiCardId`를 임시로 바꿔 두면 Ack 거부 + 사유 표시, 씬 진입 안 함 | ✅ 2026-08-25 통과 |
| 체크메이트 승패 표시 | 백/흑 어느 쪽이든 자기 결과가 맞게 표시 | ✅ 2026-08-25 통과 (버그 발견·수정, 아래 참고) |
| **룸코드 UI 연결** | 호스트가 코드 발급·복사, 게스트가 붙여넣어 접속 → 양쪽 대국 진입 | ✅ 2026-09-04 통과 (MPPM 2인, Relay) |
| 게스트가 먼저 뜸 | join code 대기 후 정상 연결 | ⬜ 미검증 |
| 게스트가 안 옴 | 15초 뒤 타임아웃 실패 | ⬜ 미검증 |
| 서로 다른 노드 선택 | 같은 판에서 시작 (제약 해제 확인) | ⬜ 미검증 |
| 단일 플레이 회귀 | 런/연습이 `MatchSetup` 없이 기존대로 동작 | ⬜ 미검증 |
| 루프백 회귀 | 핸드셰이크를 건너뛰고 기존대로 동작 | ⬜ 미검증 |

### 3단계 검증에서 발견하고 고친 버그 (2026-08-25)

| 버그 | 원인 | 조치 |
|---|---|---|
| 체크메이트/항복 결과가 흑 플레이어에게 뒤집혀 표시 | `GameManager.EndGame()`이 절대 색 기준 `FinishType`을 그대로 `EndGamePanel`(UI)과 `PlayerState.EndGame`(전적)에 넘겼는데, 둘 다 "`WhiteWin` = 플레이어 승리"로 해석한다. 플레이어가 흑이면 승패 표시·보상(`CardRewardManager`가 `WhiteWin`일 때만 카드 지급)·전적이 전부 뒤집힌다. 스테이지 0에서 항복 자체의 색 판정은 고쳤지만 그 결과가 UI·보상·전적으로 넘어가는 지점은 안 고쳐진 상태였다. **멀티 게스트뿐 아니라 단일 플레이에서 흑을 선택해도 재현되던 버그다.** | `GameManager.EndGame()`에 `ToPlayerRelativeResult()`를 추가해 UI·`PlayerState`로 넘기기 직전 `PlayerColor` 기준으로 변환. `FinishType` 자체(다른 소비처는 `!= None`만 봄)는 절대값 그대로 유지 |
| 핸드셰이크가 끝나기 전에 맵 노드를 눌러 대국 씬 진입 가능 | `MapManager.Awake()`가 씬 로드 즉시 — 핸드셰이크 시작 전 — 조건 없이 `Init()`을 불러 임시 맵을 채운다. 맵 노드 버튼(`UIButton`, `ButtonType.GoScene`)은 `SceneLoadManager.LoadScene()`을 곧바로 부를 뿐 `MatchSessionState`를 전혀 확인하지 않는다. 핸드셰이크가 (카드 DB 불일치처럼) 몇 초 걸려 실패하는 경우, 실패 판정이 나기 전에 이미 대국 화면에 들어가 버렸다. 정상 케이스에서는 핸드셰이크가 보통 1초 안에 끝나 눈에 안 띄던 레이스였다. | `MapUI`에 `IsMultiplayerHandshakePending()`(멀티 모드 && 세션이 `Ready` 아님) 조건을 노드 `interactable`에 추가. `GameCycleManager`가 `Ready`를 받으면 `MapManager.Init()` 직후 `MapUI.Rebuild()`를 불러 진짜(합의된) 맵으로 다시 짓고 노드를 활성화 |

---

### 착수 전 체크리스트

- [x] UGS 대시보드에서 프로젝트 생성 및 에디터 연결
- [x] 패키지 설치 — `com.unity.services.authentication` 3.7.4,
      `com.unity.services.multiplayer` 2.3.0, `com.unity.multiplayer.playmode` 1.6.3
      (`com.unity.transport` 2.7.3은 의존성으로 함께 들어옴)
- [x] `RelayMatchTransport : IMatchTransport` 구현 — 게임 로직 변경 없음
- [x] **Android 엔진 커버리지** (8-3 함정) — **해소.**
      `Assets/Plugins/Android/libs/fairystockfish-release.aar`가 있고 `FairyStockfishBridge`가
      `#if UNITY_ANDROID` 분기로 JNI(`com.example.chessaiv2.FairyStockfish`)를 쓰며,
      양쪽 다 `InitEngine("chaoschess")`로 같은 variant를 넘긴다.
      PC는 `VariantPath`로 `variants.ini`를 명시하는데 Android 경로에는 그 호출이 없어
      실기기 확인이 필요해 보였으나, **`chaoschess` 변형이 AAR 라이브러리에 내장돼 있어
      `variants.ini`를 따로 읽힐 필요가 없다.** 로크스텝의 전제는 충족된다. (2026-08-17 확인)
- [ ] `com.unity.transport`를 manifest에 명시 — 직접 `using` 하므로 MPS 의존성 변경에 대비
- [x] 체크메이트 종료 시 양쪽 승패 일치 검증 — 2026-08-25. 흑 플레이어 표시 뒤집힘 버그 발견·수정, 11-8 참고

### 테스트 방법

MPPM으로 가상 플레이어 두 개를 띄운다. **`Assets/Scenes/MainScene.unity`에서 Play한다** —
`GameCycleManager`가 이 씬에만 있고, 여기서 모드와 진영이 정해진 뒤 `DontDestroyOnLoad`로
`MainGameScene`까지 넘어간다. `MainGameScene`을 직접 Play하면 모드가 `Run`으로 떨어져
`AiTurnController`가 잡힌다.

MPPM 가상 플레이어는 씬 에셋을 공유해서 인스펙터 값으로는 두 인스턴스를 구분할 수 없다.
`MppmMatchRole`이 `CurrentPlayer.IsMainEditor`로 갈라낸다.

> **포커스를 잃은 창은 멈춘다(8-4).** 두 창을 번갈아 조작해야 하는데, 멈춘 쪽은 Relay
> 핑이 나가지 않아 연결이 끊긴다. `MatchSession`이 세션 중에만 `runInBackground`를 켜므로
> 프로젝트 설정은 그대로 두면 된다.
>
> 그래도 스톨 로그가 뜬다면 런타임 설정이 이 에디터 버전에서 Play Mode에 반영되지 않는
> 경우이니, **테스트하는 동안만** Player Settings의 `Run In Background`를 켜고
> 커밋하지 말 것.

| 인스턴스 | 역할 | 진영 |
|---|---|---|
| 메인 에디터 | Host | 백 |
| 클론(Player 2) | Guest | 흑 |

join code는 UI가 없으므로 호스트가 OS 임시 폴더에 파일로 남기고 게스트가 읽는다.

**설정** — 3c 이후로 `MainScene` 한 곳에서 끝난다.

`MainScene` → `GameCycleManager`

- `debugMultiplayerMode` 체크
- `multiplayerTransport = Relay` (새 필드의 기본값이 `Relay`라 그대로 두면 된다)

> `MainGameScene`의 `RemoteTurnProvider`에는 이제 설정할 값이 없다.
> 씬 YAML에 남아 있는 옛 `transportKind` / `relayRole` / `relayJoinCode` 값은
> 대응하는 필드가 사라졌으므로 Unity가 무시한다. 씬을 다시 저장하면 정리된다.

**노드 제약은 3b로 풀렸다.** 초기 판과 카드 큐를 호스트가 정해 내려주므로
양쪽이 서로 다른 노드를 골라도 같은 판에서 시작한다. 보스 층도 상관없다.
(예전에는 `MapManager.SelectFEN()`이 보스 층에서 FEN을 랜덤으로 고르고 맵 그래프도
클라이언트마다 달라서, 1층 일반 노드로만 테스트해야 했다.)

→ **검증 시나리오에 "서로 다른 노드 선택"을 넣어 이 해제를 확인할 것.**

---

## 12. 정리해야 할 임시 코드

| 대상 | 내용 |
|---|---|
| `MppmMatchRole` | MPPM 두 인스턴스에 역할·진영을 자동 배정하고 join code를 임시 파일로 주고받는 **에디터 전용 테스트 보조**. 연결 UI가 붙어(2026-09-04) 정상 경로에서는 더 이상 타지 않는다(`ExplicitRole`이 true면 통째로 건너뜀). UI 없이 인스펙터 토글만으로 빠르게 돌릴 때를 위해 남겨 뒀고, 매칭(5단계)이 붙으면 파일째 제거 |
| `GameCycleManager.multiplayerRole` / `multiplayerJoinCode` (구 `fallbackRelayRole`/`fallbackRelayJoinCode`) | 연결 UI가 채우는 실제 입력 겸, UI 없이 인스펙터로 테스트할 때 쓰는 디버그 폴백. 매칭이 붙으면 서버 배정으로 대체 |
| `GameCycleManager.DefaultPlayerColor`의 멀티 분기 | 역할에서 진영을 파생시키는 우회. 매칭이 붙으면 서버가 배정한 색을 `SetPlayerColor`로 넣는다 |
| 호스트 권위 (`MatchSetup`을 호스트가 결정) | 서버가 없어서 택한 임시 구조. 호스트가 자기 유리하게 카드를 뽑을 수 있다. 6단계 서버 검증에서 대체 |
| `GameCycleManager.multiplayerTransport` 기본값이 `Relay` | 2단계 검증 설정. 브랜치를 받으면 곧바로 Relay로 진입한다. 매칭이 붙으면 서버가 정한다 |
| `MatchMessage.CreateResign` | 수신부만 있고 **송신하는 곳이 없다.** 항복 UI 자체가 아직 없어 죽은 경로다. 4단계에서 이탈 처리와 함께 붙일 것 |
| `RemoteTurnProvider.OnDestroy` → `MatchSession.EndMatch()` | 매치 씬을 떠나면 연결을 끊는다. 씬 밖으로 소유권을 옮긴 뒤에도 종료 시점만은 씬에 남아 있는 셈이다. 재접속(4단계)이 붙으면 세션이 스스로 판단해야 한다 |
| `MatchProfile`을 클라이언트가 자기 신고 | 상대가 보낸 이름·아바타를 그대로 믿는다. 사칭이 가능하다. 표시 전용이고 레이팅에 쓰이지 않아 지금은 감수하지만, 4절의 "클라이언트가 보낸 신원을 믿지 않는다" 원칙과 어긋난다. 서버가 신원을 검증하는 6단계에서 대체 |
| `MatchSetupFactory.MultiplayerInitialFen` | 시작 판이 표준 배치 하나로 고정돼 있다. 판 종류를 고르게 하려면 서버나 UI가 정해야 한다 |
| `GameCycleManager.debugPlayAsBlack` | 흑 플레이 검증용. 실제 매칭이 붙으면 제거 |
| `GameCycleManager.debugMultiplayerMode` | 멀티 모드 진입용. 매칭이 붙으면 제거 |
| `GameManager.AiAutoMoveEnabled` | 이름과 역할이 어긋났다. "상대 턴 자동 진행"과 "로컬 입력 제한"이 한 변수에 묶여 있어, 카드 랩에서 원격 테스트를 하려면 분리해야 한다 |
| `CardDataSO`의 `[FormerlySerializedAs]` | 카드 `.asset` 53개가 전부 재직렬화되기 전에는 제거 금지. 떼면 아직 저장 안 된 카드의 대상 진영이 `Self`로 리셋된다 |
