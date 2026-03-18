
public class Rook : Piece
{
    public override string GetFen()
    {
        if (Color == PieceColor.White)
            return "R";
        else
            return "r";
    }
}
