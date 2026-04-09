public class StagRook : Piece
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
            if (Upper) return "O";
            else return "o";
        }
    }
}
