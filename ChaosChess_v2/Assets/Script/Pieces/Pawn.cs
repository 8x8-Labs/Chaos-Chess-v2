public class Pawn : Piece
{
    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "P";
        else
            return "p";
    }
}