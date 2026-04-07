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
| `Apply()` | **sealed** — `GameManager.OnAnyTurnChanged`에 구독 후 `OnApply()` 호출 |
| `Revert()` | **sealed** — `GameManager.OnAnyTurnChanged` 구독 해제 후 `OnRevert()` 호출 |
| `OnApply()` | **abstract** — 서브클래스에서 훅/버프를 부착하는 실제 구현 |
| `OnRevert()` | **abstract** — 서브클래스에서 훅/버프를 해제하는 실제 구현 (`Destroy(this)` 포함) |
| `OnTurnChanged()` | 매 턴(`GameManager.NextTurn()`) 자동 호출 → 카운트 감소, 만료 시 `Revert()` 자동 호출 |

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
