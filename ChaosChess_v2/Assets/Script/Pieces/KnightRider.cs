public class KnightRider : Piece
{
    public override string GetFen()
    {
        if (FenOverride != null)
            return FenOverride;
        else
        {
            bool Upper = Color == PieceColor.White;
            if (Upper) return "Z";
            else return "z";
        }
    }
}
