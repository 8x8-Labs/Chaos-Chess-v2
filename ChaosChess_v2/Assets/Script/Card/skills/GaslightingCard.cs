using UnityEngine;

/// <summary>
/// 가스라이팅 - 기물 전용 (희귀)
/// 나이트, 비숍, 폰에 적용됩니다.
/// 랜덤한 상대 기물(킹, 퀸, 룩 제외)을 자신의 기물로 변환합니다.
/// </summary>
public class GaslightingCard : CardData, IPieceCard
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
        Piece p = GetRandomPiece();
        if (p == null) return;

        BoardManager.Instance.ChangePiece(
            pos: p.Pos,
            color: GameManager.Instance.PlayerColor,
            type: p.TypeToChar());

        GameManager.Instance.NextTurn(() => GameManager.Instance.RequestAIMove());
    }

    private Piece GetRandomPiece()
    {
        Piece selectedPiece = null;
        int count = 0;

        foreach (Piece p in BoardManager.Instance.GetAllPieces())
        {
            if (p.Color == GameManager.Instance.EnemyColor && (DataSO.PieceType & p.Type) != 0)
            {
                count++;
                if (Random.Range(0, count) == 0)
                    selectedPiece = p;
            }
        }

        return selectedPiece;
    }
}
