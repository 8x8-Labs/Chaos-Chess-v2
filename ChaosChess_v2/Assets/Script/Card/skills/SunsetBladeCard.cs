using System;
using UnityEngine;

/// <summary>
/// 노을빛 검 - 기물 전용 (희귀)
/// 폰에 적용. 상대 기물을 잡을 때 기물 옆에 있는 2개의 기물도 함께 잡힙니다 (1회 한정).
/// </summary>
public class SunsetBladeCard : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    public void LoadPieceSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<PieceSelector>();
        selector.EnableSelector(this);
    }

    [ContextMenu("Execute")]
    public void Execute(CardEffectArgs args = null)
    {
        Piece piece = args.Targets[0];

        Action<Vector3Int> effect = null;

        effect = (pos) =>
        {
            OnCaptureEffect(pos);
            piece.RemoveOnCaptureEffect(effect);
        };

        piece.AddOnCaptureEffect(effect);
    }

    private void OnCaptureEffect(Vector3Int pos)
    {
        Vector3Int[] dirs = new Vector3Int[]
        {
        Vector3Int.left,
        Vector3Int.right
        };

        foreach (Vector3Int dir in dirs)
        {
            Debug.Log(pos + dir);
            BoardManager.Instance.DestroyPiece(pos + dir);
        }
        ;
    }
}
