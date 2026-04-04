using System;

[Flags]
public enum PieceType
{
    None = 0,
    Pawn = 1,
    Knight = 2,
    Bishop= 4,
    Rook= 8,
    King = 16,
    Queen = 32,
    Wall = 64,
    Amazon = 128
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

public enum AdditionalDescription
{
    Break,
    Revive
    // ...
}

public enum CardType
{
    Piece,
    Tile,
    Global
}