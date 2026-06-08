public interface IPieceCard : ICard
{
    void LoadPieceSelector();
}

public interface IPieceTargetFilter
{
    bool CanSelectPiece(Piece piece);
}

public interface ITileCard: ICard
{
    void LoadTileSelector();
}
