using UnityEngine;

/// <summary>
/// Elo래이팅을 reduction만큼 늘리는 디버프
/// </summary>
[CreateAssetMenu(menuName = "Debuff/EloRating")]
public class EloRatingDebuff : BuffSO
{
    [SerializeField] int reduction;
    
    public override void OnApply(Player player)
    {
        if (GameManager.Instance == null)
            return;

        GameManager.Instance.ModifyELO(reduction);
    }

    public override void OnRemove(Player player)
    {
        if (GameManager.Instance == null)
            return;

        GameManager.Instance.ModifyELO(-reduction);
    }
}
