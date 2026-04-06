public class KnightRider : Piece
{
    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "Z";
        else
            return "z";
    }
}
