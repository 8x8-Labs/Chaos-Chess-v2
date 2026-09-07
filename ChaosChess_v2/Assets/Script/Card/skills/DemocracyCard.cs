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
        Debug.Log("[Democracy] Card executed.");

        DemocracyEffect effect =
            CreateGlobalEffector<DemocracyEffect>(args);
        effect.SetCasterColor(args != null
            ? args.ResolveCasterColor()
            : CardEffectArgs.ResolveDefaultCasterColor());

        effect.Apply();
        effect.CheckCondition();
    }
}

public class DemocracyEffect : GlobalEffector
{
    // 혁명 판정 대상은 시전자의 상대 진영입니다. 지속 중 턴이 바뀌어도 대상이 뒤집히지 않도록 고정합니다.
    private PieceColor casterColor = PieceColor.White;

    public void SetCasterColor(PieceColor color)
    {
        casterColor = color;
    }

    protected override void OnApply()
    {
        GameManager.Instance.OnTurnChanged += CheckCondition;
    }

    protected override void OnRevert()
    {
        Debug.Log("[Democracy] Effect reverted.");
        GameManager.Instance.OnTurnChanged -= CheckCondition;
        Destroy(gameObject);
    }

    protected override void OnDestroy()
    {
        Revert();
        base.OnDestroy();
    }

    public void CheckCondition()
    {
        List<Piece> allPieces = BoardManager.Instance.GetAllPieces();
        PieceColor targetColor = CardTargetRelationExtensions.Opposite(casterColor);
        int pawnCount = 0;
        int otherCount = 0;

        foreach (Piece piece in allPieces)
        {
            if (piece.Color == targetColor)
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
            Debug.Log($"[Democracy] Condition met: Pawns={pawnCount}, Others={otherCount}");
            GameManager.Instance.OnSurrender(targetColor);
            Revert();
        }
    }
}
