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
| `ApplySFX` | 효과 적용 순간 1회 효과음 |
| `HookSFX` | 게임 훅(이동/잡기/타일 진입 등) 발동 시 1회 효과음 |
| `RevertSFX` | 효과 소멸 순간 1회 효과음 |
| `SFXVolume` | 위 효과음들의 재생 볼륨(0~1) |

> 프리팹/클립을 비워두면 해당 시점 연출은 자동으로 생략됩니다. 기존 카드(미설정)는 동작 변화가 없습니다.
> 효과음은 `SoundManager.SFXPlay`로 SFX 믹서 그룹을 통해 2D로 재생되며, 파티클과 동일한 시점(적용/훅/소멸)에 자동으로 울립니다. VFX 프리팹이 없어도 효과음만 단독 재생할 수 있습니다.

### 자동 재생 (apply / loop / revert)

베이스 `Effector`의 `Apply()` / `Revert()`에 통합되어 있어 **서브클래스가 따로 호출할 필요가 없습니다.**
위치/부착 대상은 각 타입이 제공합니다.

| 타입 | 앵커 위치 | 루프 부착 대상 |
|---|---|---|
| `PieceEffector` | `target.transform.position` | 기물 transform (이동 시 따라가고, 기물 파괴 시 자동 정리) |
| `TileEffector` | `GridPosToWorldPos(tilePos)` | 호스트 GameObject (호스트 파괴 시 자동 정리) |
| `GlobalEffector` | 보드 중앙 (단일 대상이 없어 중앙을 앵커로 사용) | 부모 없음 (정적 위치, Revert 시 정리) |

> `GlobalEffector`의 apply/revert VFX는 보드 중앙에 1회 버스트로 표시됩니다. 훅 VFX는 별도로 `OnPieceAct`에서 행동한 기물 위치를 직접 넘겨 재생합니다. 효과음(SFX)은 좌표와 무관한 2D 재생이라 세 타입 모두에서 동일하게 동작합니다.

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

## VFX 리스너 (복제본 컴포넌트 호출)

복제된 **루프 VFX 인스턴스**(`LoopVFXPrefab`)의 컴포넌트가 effector 생애주기 시점에 자기 함수를 호출받도록 하는 훅입니다.
단순히 파티클만 재생하는 게 아니라, 복제본 안의 동작 로직(조준 연출, 남은 턴 표시 등)을 effector와 연동할 때 사용합니다.

### 인터페이스 (필요한 것만 골라 구현)

| 인터페이스 | 메서드 | 호출 시점 |
|---|---|---|
| `IEffectApplyListener` | `OnEffectApply(in EffectVFXContext)` | 효과 적용 순간(루프 VFX 스폰 직후) |
| `IEffectHookListener` | `OnEffectHook(in EffectVFXContext)` | 이동/잡기/타일 진입 등 게임 훅 발동 시 |
| `IEffectTickListener` | `OnEffectTurnTick(in EffectVFXContext)` | 지속 턴 1턴 경과(만료 직전 제외) |
| `IEffectRevertListener` | `OnEffectRevert(in EffectVFXContext)` | 효과 만료/해제로 루프 VFX 정리 직전 |

각 인터페이스는 독립적입니다. 4개를 다 구현할 필요 없이 원하는 시점만 구현하세요.

```csharp
// LoopVFXPrefab에 올리는 컴포넌트. 적용/턴경과/소멸만 받고 훅은 무시.
public class AimEffect : MonoBehaviour, IEffectApplyListener, IEffectTickListener, IEffectRevertListener
{
    public void OnEffectApply(in EffectVFXContext ctx) { /* 등장 연출 */ }
    public void OnEffectTurnTick(in EffectVFXContext ctx) { /* ctx.RemainingTurns 표시 갱신 */ }
    public void OnEffectRevert(in EffectVFXContext ctx) { /* 퇴장 연출 (이후 자동 파괴) */ }
}
```

### EffectVFXContext

| 필드 | 설명 |
|---|---|
| `Effector` | 호출한 효과(대상 기물/타일 등 접근용) |
| `WorldPos` | 호출 시점 앵커 월드 좌표 |
| `RemainingTurns` | 남은 지속 턴 (`-1` = 영구) |

### 동작 규칙

- 리스너는 **`LoopVFXPrefab` 인스턴스(및 그 자식)에서만** 캐싱됩니다. 동작 컴포넌트는 루프 프리팹에 올리세요. (원샷 Apply/Hook/Revert 프리팹은 스스로 파괴되어 수명 관리 대상이 아님)
- effector ↔ 복제본이 1:1로 묶이므로 같은 카드를 여러 기물에 써도 각 인스턴스는 자기 effector 시점에만 반응합니다.
- `OnEffectRevert`는 정상 만료·수동 `Revert`·강제 파괴(기물 잡힘 등) 모두에서 정확히 1회 호출됩니다(`StopLoopVFX` 단일 지점).
- `LoopVFXPrefab`에 해당 인터페이스 컴포넌트가 없으면 아무 동작도 하지 않습니다(기존 카드 무영향).
- `GlobalEffector`는 앵커 위치가 없어 루프 VFX를 띄우지 않으므로 이 메커니즘은 `PieceEffector`/`TileEffector`에만 적용됩니다.

---

## 타일 등장 연출 (TileEffector 전용)

`TileEffector`의 타일이 **처음 깔리는 순간**의 등장 방식을 카드별 `CardDataSO`에서 설정합니다.
기물에 박히는 타일베이스(`EffectTileBase`/`EffectTileAnimationFrames`)에만 적용되며, 파티클 VFX와는 독립적입니다.

### CardDataSO 필드

| 필드 | 설명 |
|---|---|
| `TileAppearMode` | `None`(고정 타일 — 즉시 표시) / `Drop`(물체형 타일 — 위에서 떨어짐) / `Scale`(작은 크기에서 원래 크기로 확대) |
| `TileAppearDropHeight` | 떨어지기 시작하는 높이(셀 단위) |
| `TileAppearDropDuration` | 떨어지는 시간(초) |
| `TileAppearDropEase` | 착지감용 DOTween `Ease`(기본 `OutBounce`) |
| `TileAppearScaleFrom` | 확대를 시작하는 크기 배율(기본 `0.7` → 원래 크기 `1`로 확대) |
| `TileAppearScaleDuration` | 확대되는 시간(초) |
| `TileAppearScaleEase` | 확대용 DOTween `Ease`(기본 `OutBack`) |

### 동작

- `Drop` 모드면 `ShowTileEffect()` 호출 시 **실제 타일을 셀에 깐 뒤 셀별 transform 행렬(`Tilemap.SetTransformMatrix`)로 위쪽에서 떨어뜨리고, 착지 순간 행렬을 항등(identity)으로 되돌립니다.** 임시 스프라이트를 따로 띄우지 않으므로 크기·sorting이 타일과 100% 일치하고, 애니메이션 타일/룰 타일에도 그대로 동작합니다.
- `Scale` 모드면 같은 방식으로 셀에 타일을 깐 뒤 `TileAppearScaleFrom`(기본 0)에서 `1`까지 균일 확대하고, 완료 시 행렬을 항등으로 되돌립니다. 타일 메시는 이미 셀 로컬 원점(타일 중심) 기준으로 그려지므로 pivot 보정 없이 순수 `Matrix4x4.Scale`만 적용해 가운데에서 확대됩니다.
- 셀 transform 행렬을 적용하려면 셀의 `TileFlags.LockTransform` 잠금을 풀어야 하므로, 연출 전에 `SetTileFlags(pos, TileFlags.None)`를 호출합니다(완료 후 재배치 시 타일 기본 플래그로 복원).
- DOTween은 `Matrix4x4`를 직접 트윈하지 못하므로 `DOVirtual.Float`로 스칼라(`Drop`=높이 오프셋 셀×`cellSize.y`, `Scale`=크기 배율)를 트윈하며 매 틱 행렬을 다시 씁니다. 이징은 각각 `TileAppearDropEase`/`TileAppearScaleEase`.
- 등장 연출은 **최초 적용(`ShowTileEffect`) 경로에만** 붙습니다. 투기장 복귀·세이브 복원 등 재표시(`ToggleTileVisual`/`RestoreTileEffects`)에서는 재생되지 않습니다.
- 착지 전 `ClearTileEffect`/덮어쓰기/`ClearAllTileEffects`가 들어오면 진행 중인 트윈은 정리되고 셀 행렬은 항등으로 복원됩니다.

> `ObeyOrderCard`/`SyncCard`처럼 `TileEffectDrawer.SetTileEffect`를 직접 호출하는 특수 케이스는 기본값(`playAppear: false`)이라 등장 연출이 생략됩니다. 필요 시 `playAppear: true`를 직접 넘기세요.

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
