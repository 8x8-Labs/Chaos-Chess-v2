using UnityEngine;

public class UIPieceDrawer : MonoBehaviour
{
    public void DrawSelectPiece(Piece p)
    {
        p.OnSelected();
    }

    public void EraseSelectPiece(Piece p)
    {
        p.OnDeselect();
    }
}
