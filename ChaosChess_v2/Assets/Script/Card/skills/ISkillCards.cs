public interface IPieceCard : ICard
{
    PieceType RequiredPieceType { get; set; }
    int RequiredPieceCount { get; set; }
    void LoadSelector();
}

public interface ITileCard: ICard
{
    int RequiredTileCount { get; set; }
    void LoadSelector();
}

public interface IGlobalCard : ICard
{
    ApplyType RequiredType { get; set; }
    PieceType TargetPiece { get; set; }
}

