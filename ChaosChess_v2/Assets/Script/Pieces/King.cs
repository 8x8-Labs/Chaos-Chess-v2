public class King : Piece
{
    public override string GetFen()
    {
        if (FenOverride != null)
            return FenOverride;
        else
        {
            bool Upper = Color == PieceColor.White;
            if (Upper) return "K";
            else return "k";
        }
    }
}
