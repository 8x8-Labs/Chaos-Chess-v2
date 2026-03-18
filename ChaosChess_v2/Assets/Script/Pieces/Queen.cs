public class Queen : Piece
{
    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "Q";
        else
            return "q";
    }
}
