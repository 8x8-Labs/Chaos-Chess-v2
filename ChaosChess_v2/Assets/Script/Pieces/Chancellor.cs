public class Chancellor : Piece
{
    public override string GetFen()
    {
        if (MoveFenOverride != null)
            return Color == PieceColor.White
                ? MoveFenOverride.ToUpper()
                : MoveFenOverride.ToLower();

        if (FenOverride != null)
            return FenOverride;
        else
        {
            bool Upper = Color == PieceColor.White;
            if (Upper) return "Y";
            else return "y";
        }
    }
}
