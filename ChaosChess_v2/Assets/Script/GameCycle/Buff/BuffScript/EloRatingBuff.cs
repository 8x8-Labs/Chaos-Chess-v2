using UnityEngine;

/// <summary>
/// Elo래이팅을 50만큼 줄이는 버프
/// </summary>
[CreateAssetMenu(menuName = "Buff/EloRating")]
public class EloRatingBuff : BuffSO
{
    [SerializeField] int reduction;

    public override void OnApply(Player player)
    {
        if (GameManager.Instance == null)
            return;

        GameManager.Instance.ModifyELO(-reduction);
    }

    public override void OnRemove(Player player)
    {
        if (GameManager.Instance == null)
            return  ;

        GameManager.Instance.ModifyELO(reduction);
    }
}
