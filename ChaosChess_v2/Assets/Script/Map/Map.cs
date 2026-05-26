using System.Collections.Generic;
using UnityEngine;

public enum NodeType { Normal, Elite, Boss }

[System.Serializable]
public class Map
{
    public int ELO;
    public string FEN;
    public bool isCleared;
    public int floor;

    public NodeType nodeType;
    public int column;
    public List<int> nextColumns = new();
    public bool isAccessible;
    public Vector2 uiPosition;
}
