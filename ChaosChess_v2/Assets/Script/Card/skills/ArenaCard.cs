using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 투기장
/// 랜덤한 상대 기물 3개와 플레이어 전체 기물이 투기장으로 이동합니다.
/// 상대 기물이 모두 잡혔을 경우 잡힌 기물만 사라집니다.
/// 최대 8턴 내에 처리하지 못하면 무효 처리됩니다.
/// </summary>
public class ArenaCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        GameManager.Instance.CancelCurrentSelectionForBoardTransition();

        PieceColor casterColor = args != null
            ? args.ResolveCasterColor()
            : CardEffectArgs.ResolveDefaultCasterColor();
        PieceColor targetColor = casterColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

        // 상대 King 제외 기물 중 랜덤 3개 선택
        List<Piece> opponents = BoardManager.Instance.GetAllPieces()
            .FindAll(p => p.Color == targetColor
                       && p.Type != PieceType.King);

        // Fisher-Yates shuffle
        for (int i = opponents.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (opponents[i], opponents[j]) = (opponents[j], opponents[i]);
        }

        List<Piece> arenaOpponents = opponents.GetRange(0, Mathf.Min(3, opponents.Count));
        ArenaManager.Instance.StartArena(arenaOpponents, DataSO);
    }
}
