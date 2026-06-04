using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PieceIconSet", menuName = "Card/Piece Icon Set")]
public class PieceIconSetSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public PieceType type;
        public Sprite whiteIcon;
        public Sprite blackIcon;
    }

    public Entry[] icons;

    public Sprite GetIcon(PieceType type, PieceColor color)
    {
        foreach (var e in icons)
        {
            if (e.type != type) continue;
            return color == PieceColor.White ? e.whiteIcon : e.blackIcon;
        }
        return null;
    }
}
