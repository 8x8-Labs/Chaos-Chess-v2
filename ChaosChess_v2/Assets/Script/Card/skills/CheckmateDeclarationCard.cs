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
        PieceColor casterColor = args != null
            ? args.ResolveCasterColor()
            : CardEffectArgs.ResolveDefaultCasterColor();
        effect.SetCasterColor(casterColor);

        effect.Apply();
    }
}

public class CheckmateDeclarationEffect : GlobalEffector
{
    private PieceColor casterColor = PieceColor.White;

    public void SetCasterColor(PieceColor color)
    {
        casterColor = color;
    }

    protected override void OnApply()
    {
        GameManager.Instance.OnHalfTurnChanged += CheckCasterCheckState;
        CheckCasterCheckState();
    }

    protected override void OnRevert()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnHalfTurnChanged -= CheckCasterCheckState;
        Destroy(gameObject);
    }

    protected override void OnDestroy()
    {
        Revert();
        base.OnDestroy();
    }

    public void CheckCasterCheckState()
    {
        if (GameManager.Instance == null
            || FairyStockfishBridge.Instance == null
            || GameManager.Instance.turnColor != casterColor
            || !FairyStockfishBridge.Instance.IsInCheck())
            return;

        PieceColor opponentColor = CardEffectArgs.OpponentOf(casterColor);
        List<Piece> list = BoardManager.Instance.GetAllPieces()
            .Where(p => p.Color == opponentColor
                     && p.Type != PieceType.King
                     && p.Type != PieceType.Queen)
            .ToList();

        List<Piece> targets = new();
        for (int i = 0; i < 5 && list.Count > 0; i++)
        {
            int rand = Random.Range(0, list.Count);
            targets.Add(list[rand]);
            list.RemoveAt(rand);
        }

        BoardManager.Instance.DestroyPieces(targets, false);
        Revert();
        GameManager.Instance.ReevaluateGameState();
    }
}
