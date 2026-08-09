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
            CreateGlobalEffector<CheckmateDeclarationEffect>(args);
        effect.SetCasterColor(args != null
            ? args.ResolveCasterColor()
            : CardEffectArgs.ResolveDefaultCasterColor());

        effect.Apply();
    }
}

public class CheckmateDeclarationEffect : GlobalEffector
{
    // 파괴 대상은 시전자의 상대 진영입니다. 지속 중 턴이 바뀌어도 대상이 뒤집히지 않도록 고정합니다.
    private PieceColor casterColor = PieceColor.White;

    public void SetCasterColor(PieceColor color)
    {
        casterColor = color;
    }

    protected override void OnApply()
    {
        GameManager.Instance.OnPlayerCheckStateChanged += PlayerCheck;
    }

    protected override void OnRevert()
    {
        GameManager.Instance.OnPlayerCheckStateChanged -= PlayerCheck;
        Destroy(gameObject);
    }

    protected override void OnDestroy()
    {
        Revert();
        base.OnDestroy();
    }

    public void PlayerCheck(bool isPlayerInCheck)
    {
        if (isPlayerInCheck)
        {
            PieceColor targetColor = CardTargetRelationExtensions.Opposite(casterColor);
            List<Piece> list = BoardManager.Instance.GetAllPieces()
                .Where(p => p.Color == targetColor
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
}
