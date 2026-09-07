using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하극상 - 기물 전용 (레어)
/// 상대의 퀸은 3턴 동안 전반향으로 한칸밖에 이동할 수 없습니다.
/// </summary>
public class MutinyCard : CardData, IPieceCard
{
    private PieceSelector selector;

    private void Awake()
    {
        selector = FindFirstObjectByType<PieceSelector>();
    }

    // RequiredPieceCount가 0이라 선택 UI 없이 즉시 Execute됩니다(상대 퀸 전체 자동 적용).
    // 카드는 기물(Piece) 타입이라 UI에 "적용 대상 + 퀸 아이콘"으로 표시됩니다.
    public void LoadPieceSelector()
    {
        if (selector == null) selector = FindFirstObjectByType<PieceSelector>();
        selector?.EnableSelector(this);
    }

    public void Execute(CardEffectArgs args = null)
    {
        // 선택 게이트(PieceSelector)가 시전자 기준 관계로 대상을 거르므로 실행도 같은 기준을 써야 합니다.
        PieceColor casterColor = args != null
            ? args.ResolveCasterColor()
            : CardEffectArgs.ResolveDefaultCasterColor();

        List<Queen> queens = BoardManager.Instance.GetPiece<Queen>
            (CardTargetRelationExtensions.Opposite(casterColor));

        if (queens == null || queens.Count == 0)
        {
            Debug.Log("상대 퀸이 없습니다!");
            return;
        }

        foreach(Queen p in queens)
        {
            if (PieceEffector.HasActiveMovementOverride(p))
                continue;

            MutinyEffect effect = CreatePieceEffector<MutinyEffect>(p);
            effect.Apply();
        }
    }
}

public class MutinyEffect : PieceEffector, IMovementOverrideEffect
{
    protected override void OnApply()
    {
        target.MoveFenOverride = "w";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        if (target != null && target.MoveFenOverride?.ToLower() == "w")
            target.MoveFenOverride = null;
        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }
}
