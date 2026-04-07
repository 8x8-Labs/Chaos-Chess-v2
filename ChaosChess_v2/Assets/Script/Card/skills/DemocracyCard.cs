using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 민주주의 - 전역 (레어)
/// 상대 폰의 개수가 상대의 폰을 제외한 기물의 개수가 두배 이상일때 혁명이 일어나 킹을 끌어내립니다. 
/// 몰락한 왕은 항복을 선언합니다.
/// </summary>
public class DemocracyCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        DemocracyEffect effect =
            CreateGlobalEffector<DemocracyEffect>();

        effect.Apply();
        effect.CheckCondition();
    }
}

public class DemocracyEffect : GlobalEffector
{
    protected override void OnApply()
    {
        GameManager.Instance.OnTurnChanged += CheckCondition;
    }

    protected override void OnRevert()
    {
        GameManager.Instance.OnTurnChanged -= CheckCondition;
        Destroy(gameObject);
    }

    public void OnDestroy()
    {
        Revert();
    }

    public void CheckCondition()
    {
        List<Piece> allPieces = BoardManager.Instance.GetAllPieces();
        int pawnCount = 0;
        int otherCount = 0;

        foreach (Piece piece in allPieces)
        {
            if (piece.Color == GameManager.Instance.EnemyColor)
            {
                if (piece.Type == PieceType.Pawn)
                {
                    pawnCount++;
                }
                else
                {
                    otherCount++;
                }
            }
        }

        if (pawnCount >= 2 * otherCount)
        {
            GameManager.Instance.OnSurrender(GameManager.Instance.EnemyColor);
            OnRevert();
        }
    }
}