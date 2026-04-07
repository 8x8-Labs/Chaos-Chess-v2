public class Chancellor : Piece
{
    public override string GetFen()
    {
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
