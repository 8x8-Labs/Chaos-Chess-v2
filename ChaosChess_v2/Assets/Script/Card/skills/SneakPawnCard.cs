using UnityEngine;

/// <summary>
/// 암습의 폰 - 기물 전용 (고급)
/// 폰 기물이 나이트 기물의 이동 방식을 1회 가지게 된다.
/// </summary>
public class SneakPawnCard : CardData, IPieceCard
{
    [Tooltip("암습 적용 중 백 폰에 표시할 커스텀 스프라이트")]
    [SerializeField] private Sprite whiteSneakSprite;
    [Tooltip("암습 적용 중 흑 폰에 표시할 커스텀 스프라이트")]
    [SerializeField] private Sprite blackSneakSprite;

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
        if (args == null || args.Targets == null || args.Targets.Count == 0) return;
        Piece piece = args.Targets[0];
        if (PieceEffector.HasActiveMovementOverride(piece)) return;

        Sprite sneakSprite = piece.Color == PieceColor.White ? whiteSneakSprite : blackSneakSprite;
        SneakPawnEffector effector = CreatePieceEffector<SneakPawnEffector>(piece);
        effector.SetSneakSprite(sneakSprite);
        effector.Apply(true);
    }
}

public class SneakPawnEffector : PieceEffector, IMovementOverrideEffect
{
    private Sprite sneakSprite;
    private SpriteRenderer spriteRenderer;
    private Sprite originalSprite;

    public void SetSneakSprite(Sprite sprite) => sneakSprite = sprite;

    protected override void OnApply()
    {
        target.MoveFenOverride = "e";
        BoardManager.Instance.RefreshMoves();

        // 행마가 바뀐 것을 알리기 위해 폰 스프라이트를 커스텀 스프라이트로 교체한다.
        // MoveFenOverride는 행마만 바꾸고 스프라이트는 건드리지 않으므로 여기서 직접 처리한다.
        spriteRenderer = target.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalSprite = spriteRenderer.sprite;
            if (sneakSprite != null)
                spriteRenderer.sprite = sneakSprite;
        }
    }

    protected override void OnRevert()
    {
        if (target != null && target.MoveFenOverride?.ToLower() == "e")
            target.MoveFenOverride = null;
        if (spriteRenderer != null)
            spriteRenderer.sprite = originalSprite;
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
