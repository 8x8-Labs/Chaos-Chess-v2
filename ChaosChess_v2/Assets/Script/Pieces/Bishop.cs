public class Bishop : Piece
{
    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "B";
        else
            return "b";
    }
}
