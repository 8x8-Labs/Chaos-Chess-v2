using UnityEngine;

[CreateAssetMenu(fileName = "Card Data", menuName = "Card/Card Data")]
public class CardDataSO : ScriptableObject
{
    public string CardName;
    public Sprite CardImage;

    [TextArea]
    public string CardDescription;

    public CardType Type;

    // 기물 타입에 필요한 설정
    [Space(30)]
    [Header("기물 타입 설정")]
    public PieceType PieceType;
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

    [Space(30)]
    [Header("전역 타입 설정")]
    //전역 타입에 필요한 설정
    public bool HasLimit;
    public int LimitTurn;

    [Space(30)]
    [Header("부가 정보 설정")]
    [Tooltip("부가적인 정보가 필요하다면 활성화해주세요.")]
    public bool NeedAdditionalDescription;
    public AdditionalDescription DescriptionType;    
}