using UnityEngine;


/// <summary>
/// 암습의 폰 - 기물 전용 (고급)
/// 폰 기물이 나이트 기물의 이동 방식을 1회 가지게 된다.
/// </summary>
public class SneakPawnCard : CardData, IPieceCard
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
        Piece piece = args.Targets[0];
        if (PieceEffector.HasActiveMovementOverride(piece)) return;

        SneakPawnEffector effector = CreatePieceEffector<SneakPawnEffector>(piece);
        effector.Apply(true);
    }
}

public class SneakPawnEffector : PieceEffector, IMovementOverrideEffect
{
    protected override void OnApply()
    {
        target.MoveFenOverride = "e";
        BoardManager.Instance.RefreshMoves();
    }

    protected override void OnRevert()
    {
        if (target != null && target.MoveFenOverride?.ToLower() == "e")
            target.MoveFenOverride = null;

        BoardManager.Instance.RefreshMoves();
        Destroy(this);
    }

    public override void OnPieceMove(Vector3Int dest)
    {
        Revert();
    }

    protected override void OnHalfTurnChanged()
    {
        Revert();
    }
}
