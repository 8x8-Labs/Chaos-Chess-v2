# Effector 시스템

기물 또는 타일에 효과를 적용하고, 지속 시간을 관리하는 컴포넌트 시스템입니다.

---

## 클래스 구조

```
Effector (abstract)
├── PieceEffector (abstract) : IPieceEffect
├── TileEffector  (abstract) : ITileEffect
└── GlobalEffector (abstract)
```

---

## Effector (기반 클래스)

모든 Effector의 공통 기반입니다. 직접 상속하지 않고 아래 세 클래스 중 하나를 상속합니다.

| 멤버 | 설명 |
|---|---|
| `IsExpired` | 효과가 만료됐는지 여부 (`remainingTurns == 0`) |
| `IsPermanent` | 영구 효과 여부 (`remainingTurns < 0`) |
| `Apply()` | **sealed** — `GameManager.OnTurnChanged`에 구독 후 `OnApply()` 호출 |
| `Revert()` | **sealed** — `GameManager.OnTurnChanged` 구독 해제 후 `OnRevert()` 호출 |
| `OnApply()` | **abstract** — 서브클래스에서 훅/버프를 부착하는 실제 구현 |
| `OnRevert()` | **abstract** — 서브클래스에서 훅/버프를 해제하는 실제 구현 (`Destroy(this)` 포함) |
| `OnTurnChanged()` | 매 턴(플레이어 차례) 자동 호출 → 카운트 감소, 만료 시 Revert() 자동 호출 |

> `Apply()` / `Revert()`는 sealed입니다. 서브클래스에서는 반드시 `OnApply()` / `OnRevert()`를 override하세요.

> `Revert()`는 만료 또는 수동 해제 시 모두 호출됩니다. `OnRevert()`에서 등록한 모든 콜백을 해제하고 `Destroy(this)`를 호출하세요.

---

## PieceEffector

특정 기물에 부착되는 효과입니다. 대상 기물의 `gameObject`에 컴포넌트로 추가됩니다.

### Init
```csharp
public void Init(Piece piece, int duration = -1)
```
- `piece` — 효과를 부착할 기물
- `duration` — 지속 턴 수 (`-1` = 영구)

### 이벤트 메서드 (override 가능)

| 메서드 | 호출 시점 |
|---|---|
| `OnPieceCapture()` | 대상 기물이 상대 기물을 잡을 때 |
| `OnPieceCaptured()` | 대상 기물이 잡힐 때 |
| `OnPieceMove(Vector3Int dest)` | 대상 기물이 이동할 때 |

### 구현 예시

```csharp
public class BurnEffector : PieceEffector
{
    protected override void OnApply()
    {
        // 이동할 때마다 HP 감소 등의 로직을 훅으로 등록
    }

    protected override void OnRevert()
    {
        // 등록한 훅 해제
        Destroy(this);
    }

    public override void OnPieceMove(Vector3Int dest)
    {
        // 이동 시 발동할 효과
    }
}
```

### 카드에서 생성

`CardData`를 상속하는 카드 클래스에서 `CreatePieceEffector<T>()`를 사용합니다.  
지속 시간은 `DataSO.PieceLimitTurn`에서 자동으로 읽어옵니다.

```csharp
public void Execute(CardEffectArgs args)
{
    var effector = CreatePieceEffector<BurnEffector>(args.Targets[0]);
    effector.Apply();
}
```

---

## TileEffector

특정 타일 위치에 부착되는 효과입니다. 새 `GameObject`에 컴포넌트로 추가됩니다.

### Init
```csharp
public void Init(Vector3Int pos, int duration = -1)
```
- `pos` — 효과를 부착할 타일 좌표
- `duration` — 지속 턴 수 (`-1` = 영구)

### 이벤트 메서드 (override 가능)

| 메서드 | 호출 시점 |
|---|---|
| `OnPieceEnter(Piece piece)` | 기물이 해당 타일에 진입할 때 |
| `OnPieceExit(Piece piece)` | 기물이 해당 타일에서 나갈 때 |

### 구현 예시

```csharp
public class TrapEffector : TileEffector
{
    protected override void OnApply()
    {
        // 타일 진입 감지 등록
    }

    protected override void OnRevert()
    {
        // 등록 해제
        Destroy(gameObject); // 호스트 GameObject까지 제거
    }

    public override void OnPieceEnter(Piece piece)
    {
        BoardManager.Instance.DestroyPiece(piece);
        Revert();
    }
}
```

### 카드에서 생성

지속 시간은 `DataSO.MaintainTurn`에서 자동으로 읽어옵니다.

```csharp
public void Execute(CardEffectArgs args)
{
    var effector = CreateTileEffector<TrapEffector>(args.TargetPos[0]);
    effector.Apply();
}
```

---

## GlobalEffector

특정 타입/색상의 기물이 행동할 때 반응하는 전역 효과입니다. 새 `GameObject`에 컴포넌트로 추가됩니다.

### Init
```csharp
public void Init(PieceType type, ApplyType color = ApplyType.All, int duration = -1)
```
- `type` — 감시할 기물 타입 (`[Flags]` 조합 가능, `None` = 모든 타입)
- `color` — 감시할 색상 (`White` / `Black` / `All`)
- `duration` — 지속 턴 수 (`-1` = 영구)

### 이벤트 메서드 (override 가능)

| 메서드 | 호출 시점 |
|---|---|
| `OnPieceAct(Piece piece, Vector3Int dest)` | 감시 조건에 맞는 기물이 이동하거나 잡을 때 |

### 헬퍼

```csharp
// 행동한 기물이 감시 조건에 맞는지 확인
protected bool IsWatching(Piece piece)
```

`OnPieceAct` 내에서 직접 사용하거나, 외부에서 호출 전 필터링에 사용합니다.

### 구현 예시

```csharp
// 백 폰 또는 비숍이 행동할 때 반응
public class WatchEffector : GlobalEffector
{
    protected override void OnApply() { }

    protected override void OnRevert()
    {
        Destroy(gameObject);
    }

    public override void OnPieceAct(Piece piece, Vector3Int dest)
    {
        if (!IsWatching(piece)) return;
        // 발동 로직
    }
}
```

### 카드에서 생성

`DataSO`의 아래 필드들이 자동으로 반영됩니다.

| `CardDataSO` 필드 | 반영 대상 |
|---|---|
| `PieceType` | `watchType` |
| `NeedTargetColor` + `GlobalTargetColor` | `watchColor` |
| `HasLimit` + `LimitTurn` | `duration` |

```csharp
public void Execute(CardEffectArgs args)
{
    var effector = CreateGlobalEffector<WatchEffector>();
    effector.Apply();
}
```

---

## VFX 연출 (파티클 + 트윈)

효과가 적용/유지/소멸되거나 게임 훅이 발동할 때 파티클과 기본 펀치 트윈을 자동으로 재생합니다.
연출 에셋은 카드별 `CardDataSO.VFX`(`CardVFXConfig`)에 직접 연결합니다.

### CardDataSO.VFX 필드

| 필드 | 재생 시점 |
|---|---|
| `ApplyVFXPrefab` | 효과 적용 순간 1회 버스트 |
| `LoopVFXPrefab` | 효과 유지 중 지속 루프 (앵커에 부착되어 따라다님) |
| `HookVFXPrefab` | 게임 훅(이동/잡기/진입 등) 발동 시 1회 버스트 |
| `RevertVFXPrefab` | 효과 소멸 순간 1회 버스트 |
| `PlayApplyAnim` / `PlayHookAnim` | 적용/훅 시 앵커에 펀치 스케일 트윈 재생 여부 |
| `AnimStrength` | 펀치 세기 |
| `AnimDuration` | 펀치 트윈 진행 시간(초) |

> 프리팹을 비워두면 해당 시점 연출은 자동으로 생략됩니다. 기존 카드(미설정)는 동작 변화가 없습니다.

### 자동 재생 (apply / loop / revert)

베이스 `Effector`의 `Apply()` / `Revert()`에 통합되어 있어 **서브클래스가 따로 호출할 필요가 없습니다.**
위치/부착 대상은 각 타입이 제공합니다.

| 타입 | 앵커 위치 | 루프 부착 대상 |
|---|---|---|
| `PieceEffector` | `target.transform.position` | 기물 transform (이동 시 따라가고, 기물 파괴 시 자동 정리) |
| `TileEffector` | `GridPosToWorldPos(tilePos)` | 호스트 GameObject (호스트 파괴 시 자동 정리) |
| `GlobalEffector` | 없음 → apply/loop/revert 연출 생략 (훅 VFX만 사용) |

루프 VFX는 앵커의 자식으로 붙으므로 정상 `Revert`·강제 `Destroy`(기물 잡힘 등) 양쪽에서 누수 없이 정리됩니다.

### 훅 VFX (opt-in)

게임 훅 VFX는 **서브클래스에서 호출**해야 합니다. 베이스 훅 메서드가 기본으로 `PlayHookVFX()`를 호출하므로,
훅을 override할 때 `base.OnXxx()`를 호출하면 훅 VFX를 함께 얻습니다(호출하지 않으면 무시).

```csharp
public override void OnPieceMove(Vector3Int dest)
{
    base.OnPieceMove(dest);  // HookVFXPrefab 버스트 + 펀치
    // 고유 로직...
}
```

직접 위치를 지정하려면 `PlayHookVFX(Vector3 worldPos, Transform punchTarget = null)`를 사용합니다.
훅 버스트는 부모 없이 스폰되어 대상 기물이 같은 프레임에 파괴돼도(잡힘) 정상 표시됩니다.

### 스폰/정리

`VFXSpawner`(정적 헬퍼)가 담당합니다. 풀링은 적용하지 않으며(추후 확장), 원샷은 파티클 수명이 끝나면 자동 파괴됩니다.

---

## 턴 관리

`OnTurnChanged()`는 `GameManager.NextTurn()` 호출 시 `OnAnyTurnChanged` 이벤트를 통해 **자동으로 호출**됩니다.  
`Apply()` 시 자동으로 이벤트에 구독되고, `Revert()` 시 자동으로 해제되므로 서브클래스에서 별도로 처리할 필요가 없습니다.

지속 턴이 있는 효과(`duration > 0`)는 매 턴마다 카운트가 감소하며, 0이 되는 순간 `Revert()`가 자동 호출됩니다.  
영구 효과(`duration = -1`)는 `OnTurnChanged()`가 호출되더라도 카운트 감소 없이 무시됩니다.

---

## 실제 사용 예 — SunsetBladeCard

```csharp
// SunsetBladeCard.cs
public void Execute(CardEffectArgs args)
{
    var effector = CreatePieceEffector<SunsetBladeEffector>(args.Targets[0]);
    effector.Apply();
}

// SunsetBladeEffector — 잡을 때 좌우 기물도 함께 제거 (1회 한정)
public class SunsetBladeEffector : PieceEffector
{
    private Action<Vector3Int> captureCallback;

    protected override void OnApply()
    {
        captureCallback = (_) => { OnPieceCapture(); Revert(); };
        target.AddOnCaptureEffect(captureCallback);
    }

    protected override void OnRevert()
    {
        if (captureCallback == null) return;
        target.RemoveOnCaptureEffect(captureCallback);
        captureCallback = null;
        Destroy(this);
    }

    public override void OnPieceCapture()
    {
        var pieces = new List<Piece>();
        foreach (var dir in new[] { Vector3Int.left, Vector3Int.right })
            pieces.Add(BoardManager.Instance.GetPiece(target.Pos + dir));
        BoardManager.Instance.DestroyPiece(pieces);
    }
}
```
