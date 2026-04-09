using UnityEngine;

/// <summary>
/// 파괴전차 - 전역 (레어)
/// 카드 실행하고 폰이 상대 기물을 잡으면 그 폰은 최대 3턴 동안 다시 행동할 수 있으며, 프로모션 시 이 효과는 끊깁니다.
/// </summary>

public class DestroyerTankCards : CardData, ICard
{
    public void Execute(CardEffectArgs args = null)
    {
        DestroyerTankEffector effector = CreateGlobalEffector<DestroyerTankEffector>();
        ApplyType watchColor;

        if (GameManager.Instance.turnColor == PieceColor.White)
            watchColor = ApplyType.White;
        else
            watchColor = ApplyType.Black;

        effector.Init(PieceType.Pawn, watchColor);
        effector.Apply();
    }
}

public class DestroyerTankEffector : GlobalEffector
{
    private int remainingUses = 3;

    protected override void OnApply()
    {
        BoardManager.Instance.RegisterGlobalEffector(this);
    }

    protected override void OnRevert()
    {
        BoardManager.Instance.UnregisterGlobalEffector(this);

        Destroy(gameObject);
    }

    public override void OnPieceCapture(Piece piece, Vector3Int dest)
    {
        if (!IsWatching(piece)) return;

        remainingUses--;

        if (remainingUses == 0)
            Revert();
        else
            GameManager.Instance.GrantExtraPlayerAction(piece);
    }
}