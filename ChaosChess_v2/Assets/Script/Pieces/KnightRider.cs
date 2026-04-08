public class KnightRider : Piece
{
    public override string GetFen()
    {
        if (MoveFenOverride != null)
            return MoveFenOverride;

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
