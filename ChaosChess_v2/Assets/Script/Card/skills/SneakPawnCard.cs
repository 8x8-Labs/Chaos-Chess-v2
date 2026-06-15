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
        Piece piece = args.Targets[0];
        if (PieceEffector.HasActiveMovementOverride(piece)) return;

        piece.MoveFenOverride = "e";
        BoardManager.Instance.RefreshMoves();

        // 행마가 바뀐 것을 알리기 위해 폰 스프라이트를 커스텀 스프라이트로 교체한다.
        // MoveFenOverride는 행마만 바꾸고 스프라이트는 건드리지 않으므로 여기서 직접 처리한다.
        SpriteRenderer sr = piece.GetComponent<SpriteRenderer>();
        Sprite originalSprite = sr != null ? sr.sprite : null;
        Sprite sneakSprite = piece.Color == PieceColor.White ? whiteSneakSprite : blackSneakSprite;
        if (sr != null && sneakSprite != null)
            sr.sprite = sneakSprite;

        GameManager.Instance.AppendAction(DataSO.PieceLimitTurn, () =>
        {
            ResetMoveFen(piece, sr, originalSprite);
        });
    }

    public void ResetMoveFen(Piece piece, SpriteRenderer sr = null, Sprite originalSprite = null)
    {
        if (piece == null) return;

        string p = piece.MoveFenOverride?.ToLower();
        if (p != "e") return;

        piece.MoveFenOverride = null;
        if (sr != null) sr.sprite = originalSprite;
        BoardManager.Instance.RefreshMoves();
    }
}
