# 카드 시스템 리팩터링 계획

- **작성일:** 2026-08-09
- **상태:** 계획 (미착수 — 코드 변경 없음)
- **기준 커밋:** `b4eab5b` / Unity 6000.0.68f1
- **동기:** "카드를 추가하기 어려운 구조"라는 문제 제기에 대한 코드 조사 및 대응안

> 이 문서는 코드베이스 조사를 바탕으로 작성됐다. 아래 파일:라인 참조는 조사 시점 기준이며,
> 실제 착수 전에 현재 코드와 일치하는지 다시 확인할 것.

---

## 1. 진단 결과

**결론: 어려운 건 효과 구현이 아니라 카드 등록·식별 배선이다.** 구조를 갈아엎을 문제가 아니라
배선을 정리할 문제다.

### 1.1 잘 되어 있는 부분 (건드리지 않는다)

| 항목 | 근거 |
|---|---|
| Effector 추상화 | `Apply()`/`Revert()` sealed + 턴 카운트 자동 감소 + VFX/SFX 자동 재생. 새 효과 하나가 40줄 안쪽 (`FireCard.cs` 87줄에 카드+효과 둘 다) |
| AI 플래너 | `AiCardTargetPlanner.cs` 가 `CardType`/`definition` 기반 제네릭 검증. 카드 50개인데 per-card switch 없음 |
| 프리팹 자동 생성 | `Assets/Editor/SkillPrefabCreator.cs` 가 스킬 클래스에서 프리팹 자동 생성 |

### 1.2 실제 마찰 6가지

**① 카드 정체성이 4갈래로 흩어져 있음 (가장 큰 문제)**

프리팹 GameObject(런타임 핸들) / `CardDataSO` 에셋 / `CardName` 문자열 / `AiCardId` 문자열.
안정적인 단일 ID가 없다. 특히 세이브가 **표시용 한글 이름**을 키로 쓴다 —
`SaveManager.cs:173` 에서 `CardName` 으로 저장하고 `SaveManager.cs:311` 에서 이름 비교로 역조회.
카드 이름을 다듬는 순간 기존 세이브에서 그 카드가 조용히 사라지고 `LogWarning` 한 줄만 남는다.

**② 등록 자동화가 죽어 있음**

실질 레지스트리는 `CardRandomizerManager.cs:10` 의 `allCards` 인스펙터 리스트 하나인데,
이걸 채워주는 `Tools/Populate Card Randomizer` 가 동작하지 않는다.
`SkillPrefabCreator.cs:180` 이 `CardRandomizer` 의 `cardPrefabs` 프로퍼티를 찾는데
현재 `CardRandomizer` 에는 그런 필드가 없다 (`content`, `spawnDelay` 뿐) → `FindProperty` 가 null → NRE.

결국 카드 추가 시 손으로 리스트에 끌어다 놓아야 하고, 빠뜨리면 컴파일도 통과하고 게임도 돌지만
카드만 안 나온다. **"추가하기 어렵다"의 체감 대부분이 여기서 온다.**

**③ `CardDataSO` 가 신(神) SO**

229줄에 40개 넘는 필드가 평평하게 있고, 기물/타일/전역 섹션이 서로 무관한데 54개 에셋 전부가 다 이고 있다.
새 카드 만들 때 인스펙터에서 뭘 채워야 하는지가 자명하지 않다.

**④ 셀렉터 보일러플레이트 복붙**

스킬 파일 50개 중 39개가 똑같은 `Awake() → FindFirstObjectByType<XSelector>()` + `LoadXSelector()` 블록을 반복한다.

**⑤ `DataSO` 와 `CardSO` 이중 배선**

`CardData.cs:22` 가 이미 `effector.CardSO = DataSO` 를 넣어주는데, 이펙터 8개가 자기
`public CardDataSO DataSO` 를 또 선언하고 카드가 재할당한다 (`FireCard.cs:22`).
같은 값에 이름이 둘이라 새로 짜는 사람이 `ShowTileEffect` 가 어느 쪽을 읽는지 매번 확인해야 한다.

**⑥ AI 지원 카드는 DLL을 건드려야 함**

`AiCardTargetPlanner.cs:78` 의 `planningCatalog.GetDefinition(cardId)` 가 반환하는
`CardPlanningDefinition` 은 `Assets/Plugins/ChaosChess.AI/ChaosChess.AI.dll` 안에 있다.
`AiSupported = true` 카드를 넣으려면 DLL 쪽 카탈로그 갱신 + 핀 버전 올리기가 필요하다.

> ⑥은 Unity 안에서 고칠 수 없다. 이 문서의 범위 밖이며 별개 이슈로 다뤄야 한다.

---

## 2. 수정 계획

검증 수단이 **에디터 컴파일 + 수동 플레이테스트**뿐이므로, ROI 순서가 아니라 **위험도 낮은 순서**로
4단계로 쪼갠다. 각 단계가 독립 PR이며, 문제 생기면 해당 단계만 되돌린다.

### 2.1 [1단계] 셀렉터 보일러플레이트 흡수 — 무위험

39개 파일의 `LoadXSelector()` 가 예외 없이 `selector.EnableSelector(this)` 한 줄이고,
`Awake()` 도 셀렉터 찾기만 한다는 것을 확인했다. 베이스로 올릴 수 있다.

`CardData.cs` 에 추가:

```csharp
public abstract class CardData : MonoBehaviour
{
    public CardDataSO DataSO;

    private PieceSelector pieceSelector;
    private TileSelector tileSelector;

    // 지연 조회: Awake 시점에 셀렉터가 아직 없어도 안전하다.
    protected PieceSelector PieceSelector =>
        pieceSelector != null ? pieceSelector : (pieceSelector = FindFirstObjectByType<PieceSelector>());
    protected TileSelector TileSelector =>
        tileSelector != null ? tileSelector : (tileSelector = FindFirstObjectByType<TileSelector>());

    public virtual void LoadPieceSelector() => PieceSelector?.EnableSelector(this);
    public virtual void LoadTileSelector() => TileSelector?.EnableSelector(this);

    // ... 기존 CreateXEffector 그대로
}
```

C#은 인터페이스 멤버를 베이스 클래스 상속 멤버로 충족할 수 있으므로,
`FireCard : CardData, ITileCard` 가 아무것도 안 써도 계약을 만족한다.
`IPieceCard`/`ITileCard` 를 둘 다 구현하는 `TeleportCard` 도 그대로 동작한다.

그다음 39개 파일에서 `private XSelector selector;` 필드 + `Awake()` + `LoadXSelector()` 를 삭제한다.

**예외 하나** — `TeleportCard.cs:22` 는 `pieceSelected = false` 상태 리셋이 있으므로 override로 남긴다:

```csharp
public override void LoadPieceSelector()
{
    pieceSelected = false;
    base.LoadPieceSelector();
}
```

효과: 약 −310줄. 새 카드 작성 시 `Execute()` 만 쓰면 된다.

### 2.2 [2단계] 이펙터의 `DataSO` 중복 제거 — 저위험

베이스 `Effector` 에 이미 `CardSO` 가 있고, `ShowTileEffect()` 는 인자가 없으면 `CardSO` 로 폴백한다
(`Effector.cs:554`). 즉 로컬 `DataSO` 는 순수 레거시다.

대상 8개 파일:

```
BlessingCard.cs  FireCard.cs  JumpingPlatformCard.cs  ObeyOrderCard.cs
PeaceZoneCard.cs  PortalCard.cs  PsilocybinMushroomCard.cs  SyncCard.cs
```

- 이펙터에서 `public CardDataSO DataSO;` 삭제
- 카드 쪽 `effect.DataSO = DataSO;` 삭제 (`FireCard.cs:22`, `PortalCard.cs:42-43` 등)
- `ShowTileEffect(DataSO)` → `ShowTileEffect()`

#### ⚠ 함정 — 반드시 이 순서로

7군데가 팩토리를 거치지 않고 직접 `AddComponent` 한다:

```
BlessingCard.cs:29    CobwebCard.cs:132    FatherEnemyCard.cs:116   GiantCard.cs:70
LimitlessCard.cs:55   ObeyOrderCard.cs:165  PsilocybinMushroomCard.cs:63
```

여기는 `CardSO` 가 채워지지 않는다. **로컬 `DataSO` 가 존재하는 진짜 이유가 이것이다.**
따라서 `CardData` 에 헬퍼를 먼저 추가해서 이 7곳을 흡수한 뒤에 필드를 지워야 한다:

```csharp
protected T AttachEffector<T>(GameObject host, CardDataSO so = null) where T : Effector
{
    T effector = host.AddComponent<T>();
    effector.CardSO = so != null ? so : DataSO;
    return effector;
}
```

**이걸 먼저 넣지 않고 필드만 지우면 VFX·타일 비주얼이 조용히 사라진다.** 컴파일로 안 잡히는 회귀다.

### 2.3 [3단계] 등록 자동화 복구 — 중위험 (에디터 전용, 런타임 무영향)

`SkillPrefabCreator.PopulateCardRandomizer` 를 실제 레지스트리인
`CardRandomizerManager.allCards` 를 대상으로 고쳐 쓴다.

```csharp
[MenuItem("Tools/Populate Card Registry")]
public static void PopulateCardRegistry()
{
    var managers = UnityEngine.Object.FindObjectsByType<CardRandomizerManager>(FindObjectsSortMode.None);
    if (managers.Length == 0) { Debug.LogError("씬에서 CardRandomizerManager를 찾을 수 없습니다."); return; }

    var prefabs = /* OutputPath 스캔, "Card" 베이스 프리팹 제외 */;

    foreach (var m in managers)
    {
        SerializedObject so = new SerializedObject(m);
        SerializedProperty prop = so.FindProperty("allCards");
        if (prop == null)
        {
            Debug.LogError("allCards 필드를 찾을 수 없습니다. 필드명이 바뀌었는지 확인하세요.");
            continue;
        }
        // ... 채우기
    }
}
```

핵심은 `FindProperty` **null 가드**다. 이게 없어서 필드명이 드리프트했을 때 조용히 죽지 않고
컴파일은 통과한 채 런타임 NRE가 났다.

여기에 검증 메뉴를 하나 더 붙인다 — `Tools/Validate Card Registry`:

- 프리팹은 있는데 `allCards` 에 없는 카드
- `DataSO` 가 안 붙은 프리팹
- `AiSupported` 인데 `AiCardId` 가 빈 카드

이게 "카드를 넣었는데 안 나와요" 의 디버깅 시간을 없앤다.

> **덤:** `PortalCard.cs` 의 클래스명이 `PortalSkill` 이라 `FindMatchingDataSO`
> (`SkillPrefabCreator.cs:196`) 의 퍼지 매칭에 걸린다. 지금은 우연히 맞고 있을 수 있지만
> 매칭 규칙이 `classLower.Contains(assetLower)` 부분 문자열이라 카드가 늘수록 오매칭 위험이 커진다.
> 정확 일치만 허용하고 실패 시 에러를 띄우는 쪽으로 좁히는 것이 낫다.

### 2.4 [4단계] 안정 ID + 세이브 키 교체 — 고위험

참고로 **버프는 이미 `BuffSO.name`(에셋 파일명)을 키로 쓰고 있다** (`SaveManager.cs:182`).
카드만 예외다. 그 컨벤션에 맞춘다.

`CardDataSO` 에 추가:

```csharp
[Tooltip("세이브 식별용 불변 ID. 한 번 정하면 절대 바꾸지 마세요. 비어 있으면 에셋 파일명을 사용합니다.")]
[SerializeField] private string cardId;
public string CardId => string.IsNullOrWhiteSpace(cardId) ? name : cardId;
```

`RunSaveData.cs:60` 옆에 `public List<string> cardIds = new();` 추가.
저장은 `CardId` 로, 로드는 `cardIds` 우선 → 비었으면 기존 `cardNames` 로 폴백:

```csharp
if (data.cardIds is { Count: > 0 })
    foreach (string id in data.cardIds) TryAddCard(FindCardById(id), id);
else if (data.cardNames != null)                       // 구버전 세이브 마이그레이션
    foreach (string n in data.cardNames) TryAddCard(FindCardByName(n), n);
```

**`cardNames` 는 지우지 말고 한 버전 더 같이 쓴다.** 롤백했을 때 유저 세이브가 안 깨진다.

그리고 `cardId` 를 전 에셋에 박는 메뉴 `Tools/Stamp Card Ids` 를 추가한다 —
비어 있는 것만 에셋 파일명으로 채우고 중복 검사. `CardId` 가 `name` 폴백을 갖고 있어서
안 박아도 동작하지만, 그러면 `.asset` 파일명을 바꾸는 순간 세이브가 깨지므로 명시적으로 박아둔다.

---

## 3. 보류 — `CardDataSO` 분할

`[SerializeReference]` 로 타입별 설정 블록을 쪼개는 것은 개념적으로는 맞지만,
**기존 55개 에셋의 인스펙터 값이 전부 날아간다.** 마이그레이션 스크립트가 필요한데
플레이테스트를 사람이 해야 하는 상황에서 회수 대비 위험이 너무 크다.

**재검토 조건:** 필드가 60개를 넘거나, 카드 타입(기물/타일/전역)이 하나 더 생길 때.

---

## 4. 작업량 추정

| 단계 | 손대는 지점 | 코드 작업 | 검증 | 라운드트립 |
|---|---|---|---|---|
| 1. 셀렉터 흡수 | 40개 파일 (기계적 삭제) | 30~45분 | 컴파일 + 카드 5~6개 스모크 20~30분 | 1~2회 |
| 2. `DataSO` 중복 제거 | 15개 지점 (8 이펙터 + 7 수동 생성) | 30~50분 | VFX/타일 비주얼 카드별 확인 40~60분 | 2~3회 |
| 3. 등록 자동화 | 에디터 스크립트 1개 | 15~20분 | 메뉴 실행 + 리스트 확인 10분 | 1회 |
| 4. 안정 ID + 세이브 | 4개 파일 | 25~40분 | 신규 세이브 + 구버전 마이그레이션 30분 | 1~2회 |

- **코드 작업 합계:** 2~3시간
- **검증 합계:** 1.5~2시간
- **라운드트립 포함 실제 소요:** 세션 3~4개 / 캘린더 2~3일 (PR 리뷰 포함)

### 추정이 틀릴 만한 지점

- **1단계는 추정이 거의 정확하다.** 39개 파일 패턴이 예외 하나(`TeleportCard`)를 빼고 완전히 균일함을 확인했다.
- **2단계가 유일한 큰 불확실성.** 7개 수동 `AddComponent` 지점의 전문을 아직 읽지 않았다.
  각각 커스텀 host GameObject나 duration 계산을 갖고 있으면 `AttachEffector<T>` 오버로드 하나로
  안 덮인다. **최악의 경우 2단계만 2배(1시간+)로 늘어난다.** 착수 전 7개 파일을 먼저 읽으면 제거 가능한 불확실성이다.
- **검증 시간이 코딩 시간보다 튈 확률이 높다.** 특히 2단계 회귀는 컴파일로 안 잡히고 눈으로만 보인다.

---

## 5. 진행 순서와 준비물

### 세션 분할

| 세션 | 내용 | 이유 |
|---|---|---|
| A | 1단계 단독 | 가장 크고 가장 안전 |
| B | 3단계 → 2단계 | 3단계의 검증 메뉴를 2단계 회귀 탐지에 바로 쓴다 |
| C | 4단계 단독 | 세이브 변경은 다른 변경과 절대 섞지 않는다 |

### 착수 전 체크리스트

- [ ] **현재 버전의 세이브 파일을 백업한다.** 4단계 코드를 넣은 뒤에는 구버전 세이브를 만들 수 없어
      마이그레이션 폴백 경로를 영원히 검증할 수 없게 된다.
- [ ] 2단계 착수 전 수동 `AddComponent` 7개 지점 전문을 읽고 추정을 확정한다.
- [ ] 각 단계를 독립 브랜치/PR로 분리한다.
