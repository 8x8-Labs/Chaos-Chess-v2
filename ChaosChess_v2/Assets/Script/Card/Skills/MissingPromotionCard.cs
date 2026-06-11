using UnityEngine;

/// <summary>
/// 진급누락 - 기물 전용(레어)
/// 상대 기물(킹, 퀸 제외)을 선택해 강제로 폰으로 바꿉니다.
/// </summary>
public class MissingPromotionCard : CardData, IPieceCard
{
    [SerializeField] private GameObject effectPrefab;

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

        // 교체 시 대상 기물이 파괴되므로 연출 위치를 먼저 확보합니다.
        Vector3 worldPos = p.transform.position;

        BoardManager.Instance.ChangePiece(p.Pos, GameManager.Instance.EnemyColor, 'p');

        if (effectPrefab != null)
            Instantiate(effectPrefab, worldPos, Quaternion.identity);
    }

}