public class King : Piece
{
    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "K";
        else
            return "k";
    }
}
