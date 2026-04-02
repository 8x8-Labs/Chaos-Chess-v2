using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 텔레포트 - 기물 전용
/// 폰 기물이 선택한 비어있는 칸으로 이동합니다.
/// 카드 사용 시 턴 사용, 프로모션 칸으로 이동 불가.
/// </summary>
public class TeleportCard : CardData, IPieceCard
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
        Piece pawn = args.Targets[0];
        Vector3Int target = args.TargetPos[0];

        BoardManager bm = BoardManager.Instance;

        if (!bm.IsInside(target)) return;
        if (!bm.IsEmpty(target)) return;

        // 프로모션 칸(맨 끝 행) 이동 불가
        int promotionRow = pawn.Color == PieceColor.White ? 7 : 0;
        if (target.y == promotionRow) return;

        bm.ForceTeleport(pawn, target);
    }
}
