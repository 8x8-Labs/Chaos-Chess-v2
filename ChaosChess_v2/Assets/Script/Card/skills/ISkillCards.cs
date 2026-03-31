using System.Collections.Generic;
using UnityEngine;

public interface IPieceCard : ICard
{
    PieceType RequiredPieceType { get; set; }
    int RequiredPieceCount { get; set; }
    void LoadSelector();
    void Execute(List<Piece> pieces);
}

public interface ITileCard: ICard
{
    int RequiredTileCount { get; set; }
    void LoadSelector();
    void Execute(List<Vector3Int> pieces);
}

public interface IGlobalCard : ICard
{
    ApplyType RequiredType { get; set; }
    PieceType TargetPiece { get; set; }
}

