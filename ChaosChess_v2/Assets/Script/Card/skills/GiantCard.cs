using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// 거대화 - 기물 전용 (무력화)
/// 기물 이동 시 1칸 반경의 기물들을 1턴 동안 무력화시킵니다.
/// 무력화 시 카드 적용은 되지만 이동할 수는 없습니다.
/// </summary>
public class GiantCard : CardData, IPieceCard
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

    public void Execute(CardEffectArgs args = null)
    {
        Piece piece = args.Targets[0];
        var effector = CreatePieceEffector<GiantEffector>(piece);
        effector.Apply();
    }
}
public class GiantEffector : PieceEffector
{
    protected override void OnApply()
    {
    }
    int[] dx = { -1, -1, -1, 0, 1, 1, 1, 0 };
    int[] dy = { -1, 0, 1, 1, 1, 0, -1, -1 };
    List<Piece> changed = new();
    public override void OnPieceMove(Vector3Int dest)
    {
        for(int i = 0; i < 8; i++)
        {
            int x = dx[i];
            int y = dy[i];
            Vector3Int pos = new Vector3Int(dest.x + x, dest.y + y, dest.z);
            if (BoardManager.Instance.IsInside(pos))
            {
                Piece target = BoardManager.Instance.GetPiece(pos);
                if (target != null)
                {
                    changed.Add(target);
                    target.MoveFenOverride = "a";
                }
            }
        }
    }
    protected override void OnRevert()
    {
        if (changed == null)
            return;
        foreach(Piece piece in changed)
        {
            piece.MoveFenOverride = null;
        }
        Destroy(this);
    }

}