using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하극상 - 기물 전용 (레어)
/// 상대의 퀸은 3턴 동안 전반향으로 한칸밖에 이동할 수 없습니다.
/// </summary>
public class MutinyCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        List<Queen> queens = BoardManager.Instance.GetPiece<Queen>
            (GameManager.Instance.EnemyColor);

        if(queens == null)
        {
            Debug.Log("상대 퀸이 없습니다!");
            return;
        }

        foreach(Queen p in queens)
        {
            MutinyEffect effect = CreatePieceEffector<MutinyEffect>(p);
            effect.Apply();
        }
    }
}

public class MutinyEffect : PieceEffector
{
    protected override void OnApply()
    {
        target.MoveFenOverride = "w";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        target.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }
}