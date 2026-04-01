using DG.Tweening;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

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
