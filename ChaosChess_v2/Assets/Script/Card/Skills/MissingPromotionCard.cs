
/// <summary>
/// 진급누락 - 기물 전용(레어)
/// 상대 기물(킹, 퀸 제외)을 선택해 강제로 폰으로 바꿉니다. 
/// </summary>
public class MissingPromotionCard : CardData, IPieceCard
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
        if (args.Targets == null || args.Targets.Count == 0) return; 
        Piece p = args.Targets[0];

        BoardManager.Instance.ChangePiece(p.Pos, GameManager.Instance.EnemyColor, 'p');
    }

}