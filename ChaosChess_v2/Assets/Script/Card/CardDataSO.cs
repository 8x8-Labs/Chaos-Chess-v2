using UnityEngine;
using UnityEngine.Tilemaps;

public enum TileEffectAnimationMode
{
    None,
    Time,
    Turn
}

/// <summary>타일 이펙트가 처음 깔릴 때의 등장 연출 방식입니다.</summary>
public enum TileAppearAnimationMode
{
    /// <summary>고정 타일: 애니메이션 없이 즉시 표시</summary>
    None,
    /// <summary>물체형 타일: 위에서 아래로 떨어지며 등장</summary>
    Drop,
    /// <summary>확대형 타일: 작은 크기에서 원래 크기로 확대되며 등장</summary>
    Scale
}

/// <summary>
/// 카드 효과가 기물/타일에 적용될 때 재생할 파티클·트윈 연출 설정입니다.
/// 프리팹을 비워두면 해당 시점 연출은 자동으로 생략됩니다.
/// </summary>
[System.Serializable]
public class CardVFXConfig
{
    [Header("파티클 프리팹")]
    [Tooltip("효과 적용 순간 1회 재생")]
    public GameObject ApplyVFXPrefab;
    [Tooltip("효과가 유지되는 동안 지속 재생(앵커에 부착되어 따라다님)")]
    public GameObject LoopVFXPrefab;
    [Tooltip("이동/잡기/타일 진입 등 게임 훅 발동 시 1회 재생")]
    public GameObject HookVFXPrefab;
    [Tooltip("효과 소멸 순간 1회 재생")]
    public GameObject RevertVFXPrefab;

    [Header("기본 트윈 연출 (펀치/스케일)")]
    [Tooltip("적용 시 앵커에 펀치 스케일 재생")]
    public bool PlayApplyAnim = true;
    [Tooltip("훅 발동 시 앵커에 펀치 스케일 재생")]
    public bool PlayHookAnim = true;
    [Range(0f, 1f)]
    public float AnimStrength = 0.2f;
    [Tooltip("펀치 트윈 진행 시간(초)")]
    [Min(0.01f)]
    public float AnimDuration = 0.3f;

    [Header("효과음 (비워두면 재생 생략)")]
    [Tooltip("효과 적용 순간 1회 재생")]
    public AudioClip ApplySFX;
    [Tooltip("이동/잡기/타일 진입 등 게임 훅 발동 시 1회 재생")]
    public AudioClip HookSFX;
    [Tooltip("효과 소멸 순간 1회 재생")]
    public AudioClip RevertSFX;
    [Range(0f, 1f)]
    [Tooltip("위 효과음들의 재생 볼륨")]
    public float SFXVolume = 1f;
}

[CreateAssetMenu(fileName = "Card Data", menuName = "Card/Card Data")]
public class CardDataSO : ScriptableObject
{
    public string CardName;
    public Sprite CardImage;

    [TextArea]
    public string CardDescription;

    public CardType Type;
    public Tier CardTier;

    [Space(30)]
    [Header("VFX 연출 설정")]
    public CardVFXConfig VFX = new CardVFXConfig();

    [Tooltip("타일/효과가 기물에 효과를 부여할 때, 그 기물에 적용되는 연출입니다. (VFX와 별개)")]
    public CardVFXConfig PieceEffectVFX = new CardVFXConfig();

    // 기물 타입에 필요한 설정
    [Space(30)]
    [Header("기물 타입 설정")]
    public PieceType PieceType;
    /// <summary>
    /// 기물 타입에서의 대상 색깔
    /// </summary>
    public PieceColor PieceTargetColor;
    [Range(-1, 10)]
    public int PieceLimitTurn;
    public int RequiredPieceCount;

    // 타일 타입에 필요한 설정
    [Space(30)]
    [Header("타일 타입 설정")]
    public int TileCount;
    /// <summary>
    /// -1 입력 시, 계속 유지
    /// </summary>
    [Range(-1, 75)]
    public int MaintainTurn;
    /// <summary>
    /// 타일 이펙트가 필요한지 여부
    /// </summary>
    public bool NeedEffectTileBase;
    /// <summary>
    /// 효과가 적용된 타일에 표시할 기본 타일베이스
    /// </summary>
    public TileBase EffectTileBase;
    /// <summary>
    /// 선택 순서별로 다른 타일베이스를 사용할지 여부
    /// </summary>
    public bool UseMultipleEffectTileBases;
    /// <summary>
    /// 선택 순서별로 표시할 타일베이스 목록입니다.
    /// </summary>
    public TileBase[] EffectTileBases;
    [Tooltip("Time은 EffectTileBase에 지정한 AnimatedTile에 위임하고, Turn은 아래 프레임 배열을 사용합니다.")]
    public TileEffectAnimationMode EffectTileAnimationMode = TileEffectAnimationMode.None;
    [Tooltip("Turn 모드에서 남은 턴에 따라 표시할 상태 프레임입니다.")]
    public TileBase[] EffectTileAnimationFrames;

    [Header("타일 등장 연출")]
    [Tooltip("None=고정 타일(즉시 표시), Drop=물체형 타일(위에서 떨어짐), Scale=작은 크기에서 확대")]
    public TileAppearAnimationMode TileAppearMode = TileAppearAnimationMode.None;
    [Tooltip("떨어지기 시작하는 높이(셀 단위)")]
    [Min(0f)]
    public float TileAppearDropHeight = 3f;
    [Tooltip("떨어지는 데 걸리는 시간(초)")]
    [Min(0.01f)]
    public float TileAppearDropDuration = 0.35f;
    [Tooltip("떨어질 때 사용할 DOTween 이징(착지감)")]
    public DG.Tweening.Ease TileAppearDropEase = DG.Tweening.Ease.OutBounce;

    [Tooltip("Scale 모드에서 확대를 시작하는 크기 배율(0 → 가운데 점에서 솟아나듯 등장)")]
    [Min(0f)]
    public float TileAppearScaleFrom = 0f;
    [Tooltip("확대되는 데 걸리는 시간(초)")]
    [Min(0.01f)]
    public float TileAppearScaleDuration = 0.3f;
    [Tooltip("확대될 때 사용할 DOTween 이징")]
    public DG.Tweening.Ease TileAppearScaleEase = DG.Tweening.Ease.OutBack;

    public TileBase GetEffectTileBase(int index)
    {
        if (!UseMultipleEffectTileBases)
            return EffectTileBase;

        if (EffectTileBases == null || EffectTileBases.Length == 0)
            return EffectTileBase;

        if (index >= 0 && index < EffectTileBases.Length && EffectTileBases[index] != null)
            return EffectTileBases[index];

        return EffectTileBases[0] != null ? EffectTileBases[0] : EffectTileBase;
    }

    public TileBase GetEffectTileAnimationFrame(int frameIndex, int effectTileIndex = 0)
    {
        if (EffectTileAnimationFrames == null || EffectTileAnimationFrames.Length == 0)
            return GetEffectTileBase(effectTileIndex);

        int safeIndex = Mathf.Clamp(frameIndex, 0, EffectTileAnimationFrames.Length - 1);
        return EffectTileAnimationFrames[safeIndex] != null
            ? EffectTileAnimationFrames[safeIndex]
            : GetEffectTileBase(effectTileIndex);
    }
    /// <summary>
    /// 활성화 시 BlockedTiles 배열 기준으로 선택 불가 타일을 지정합니다.
    /// </summary>
    public bool RestrictTiles;
    /// <summary>
    /// 8x8 = 64칸. true인 칸은 타일 선택 불가. 인덱스: y * 8 + x (y=0 하단, y=7 상단)
    /// </summary>
    public bool[] BlockedTiles = new bool[64];

    [Space(30)]
    [Header("전역 타입 설정")]
    //전역 타입에 필요한 설정
    public bool NeedTargetColor;
    /// <summary>
    /// 전역 타입에서의 대상 색깔
    /// </summary>
    public PieceColor GlobalTargetColor;
    public bool HasLimit;
    public int LimitTurn;
    [Tooltip("상태 카드에 표시할 문구 타입입니다.")]
    public ActiveEffectStatusType StatusDisplayType = ActiveEffectStatusType.Active;

    [Space(30)]
    [Header("부가 정보 설정")]
    [Tooltip("부가적인 정보가 필요하다면 활성화해주세요.")]
    public bool NeedAdditionalDescription;
    public AdditionalDescription DescriptionType;
    public string AdditionalDescriptionTitle;

    // 기물 타입 부가 설명
    [Tooltip("기물 이미지")]
    public Sprite PieceDescImage;
    [Tooltip("행마법 이미지")]
    public Sprite MovementImage;
    [TextArea]
    public string PieceDescContent;

    // 규칙 타입 부가 설명
    [TextArea]
    public string AdditionalDescriptionContent;
}
