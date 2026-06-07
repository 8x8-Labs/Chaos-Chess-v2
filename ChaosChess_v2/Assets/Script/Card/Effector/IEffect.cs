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
    bool CanPieceEnter(Piece piece, Vector3Int from, Vector3Int to);
}

public interface IPiecePathEffect
{
    void OnPieceTraverse(Piece piece, Vector3Int from, Vector3Int to);
}

public interface IPiecePathBlocker
{
    bool CanPieceTraverse(Piece piece, Vector3Int from, Vector3Int to);
}
