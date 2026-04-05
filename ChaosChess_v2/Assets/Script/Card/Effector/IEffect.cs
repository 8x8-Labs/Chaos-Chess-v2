using UnityEngine;

public interface IEffect
{
    void OnTurnChanged();
}

public interface IPieceEffect : IEffect
{
    void OnPieceCaptured();
    void OnPieceCapture();
    void OnPieceMove(Vector3Int dest);
}

public interface ITileEffect : IEffect
{
    void OnPieceEnter(Piece piece);
    void OnPieceExit(Piece piece);
}