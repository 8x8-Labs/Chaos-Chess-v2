public interface IPieceCard : ICard
{
    void LoadPieceSelector();
}

public interface ITileCard: ICard
{
    void LoadTileSelector();
}

public interface IGlobalCard : ICard
{
    ApplyType RequiredType { get; set; }
    PieceType TargetPiece { get; set; }
}

