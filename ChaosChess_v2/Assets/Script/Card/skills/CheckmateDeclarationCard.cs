using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 체크메이트 선언 - 전역
/// 다음 4턴 안에 체크를 당할 경우, 상대의 기물 5개가 자동으로 파괴됩니다.
/// </summary>
public class CheckmateDeclarationCard : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        CheckmateDeclarationEffect effect = 
            CreateGlobalEffector<CheckmateDeclarationEffect>();

        effect.Apply();
    }
}

public class CheckmateDeclarationEffect : GlobalEffector
{
    protected override void OnApply()
    {
        GameManager.Instance.OnPlayerTurnStarted += PlayerCheck;
    }

    protected override void OnRevert()
    {
        GameManager.Instance.OnPlayerTurnStarted -= PlayerCheck;
        Destroy(gameObject);
    }

    public void OnDestroy()
    {
        Revert();
    }

    public void PlayerCheck()
    {
        bool check = FairyStockfishBridge.Instance.IsInCheck();
        if (check)
        {
            List<Piece> list = BoardManager.Instance.GetAllPieces()
                .Where(p => p.Color == GameManager.Instance.EnemyColor
                         && p.Type != PieceType.King
                         && p.Type != PieceType.Queen)
                .ToList();

            // 기물 5개 파괴
            for (int i = 0; i < 5 && list.Count > 0; i++)
            {
                int rand = Random.Range(0, list.Count);
                Piece target = list[rand];
                BoardManager.Instance.DestroyPiece(target);
                list.RemoveAt(rand);
            }
            Revert();
        }
    }
}