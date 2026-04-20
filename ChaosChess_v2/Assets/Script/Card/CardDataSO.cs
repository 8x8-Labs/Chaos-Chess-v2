using UnityEngine;

[CreateAssetMenu(fileName = "Card Data", menuName = "Card/Card Data")]
public class CardDataSO : ScriptableObject
{
    public string CardName;
    public Sprite CardImage;

    [TextArea]
    public string CardDescription;

    public CardType Type;
    public Tier CardTier;

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