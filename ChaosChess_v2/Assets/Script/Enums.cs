using System;

[Flags]
public enum PieceType
{
    None = 0,
    Pawn = 1,
    Knight = 2,
    Bishop = 4,
    Rook = 8,
    King = 16,
    Queen = 32,
    Wall = 64,
    Amazon = 128,
    Chancellor = 256,
    KnightRider = 512
}
public enum PieceValue
{
    Wall = 0,
    Pawn = 1,
    Knight = 3,
    King = 3,
    Bishop = 3,
    Rook = 5,
    KnightRider = 7,
    Queen = 9,
    Chancellor = 9,
    Amazon = 13
}
/// <summary>
/// [전역 대상] 기물 적용 대상을 구별합니다.
/// </summary>
public enum ApplyType
{
    White,
    Black,
    All
}

/// <summary>
/// 카드가 노리는 대상을 시전자 기준 상대 관계로 표현합니다.
/// 절대 색상(White/Black)이 아니라 관계로 저장해야 플레이어가 흑을 잡아도 의미가 유지됩니다.
/// 직렬화 값은 기존 PieceColor/ApplyType과 호환됩니다. (0=White→Self, 1=Black→Opponent, 2=All→Any)
/// </summary>
public enum CardTargetRelation
{
    /// <summary>시전자와 같은 진영</summary>
    Self,
    /// <summary>시전자와 반대 진영</summary>
    Opponent,
    /// <summary>진영 무관</summary>
    Any
}

/// <summary>CardTargetRelation을 실제 색상으로 해소하는 헬퍼입니다.</summary>
public static class CardTargetRelationExtensions
{
    /// <summary>시전자 색을 기준으로 관계를 절대 색상으로 해소합니다. Any는 색이 없으므로 null을 반환합니다.</summary>
    public static PieceColor? Resolve(this CardTargetRelation relation, PieceColor caster)
    {
        switch (relation)
        {
            case CardTargetRelation.Self: return caster;
            case CardTargetRelation.Opponent: return Opposite(caster);
            default: return null;
        }
    }

    /// <summary>시전자 색을 기준으로 관계를 GlobalEffector 감시용 ApplyType으로 변환합니다.</summary>
    public static ApplyType ToApplyType(this CardTargetRelation relation, PieceColor caster)
    {
        PieceColor? color = relation.Resolve(caster);
        if (!color.HasValue)
            return ApplyType.All;

        return color.Value == PieceColor.White ? ApplyType.White : ApplyType.Black;
    }

    /// <summary>대상 기물의 색이 시전자 기준 관계에 부합하는지 판정합니다.</summary>
    public static bool Matches(this CardTargetRelation relation, PieceColor target, PieceColor caster)
    {
        PieceColor? required = relation.Resolve(caster);
        return !required.HasValue || required.Value == target;
    }

    public static PieceColor Opposite(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}

public enum AdditionalDescription
{
    Piece,
    Rule
}

public enum CardType
{
    Piece,
    Tile,
    Global
}

public enum Tier
{
    Common,
    Uncommon,
    Unique,
    Rare,
    Legendary
}

/// <summary>
/// 게임 한판의 결과를 저장할때 사용합니다
/// </summary>
public enum GameResult
{
    None,
    WhiteWin,
    BlackWin,
    Draw
}