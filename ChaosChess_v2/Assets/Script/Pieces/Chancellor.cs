public class Chancellor : Piece
{
    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "Y";
        else
            return "y";
    }
}
