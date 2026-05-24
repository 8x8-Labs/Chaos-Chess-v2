using UnityEngine;

[CreateAssetMenu(menuName = "BuffSystem/Definitions/EloRating")]
public class EloRatingBuff : BuffSO
{
    protected override void OnApply(Player player, BuffSide side, int magnitude)
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.ModifyELO(side == BuffSide.Buff ? -magnitude : magnitude);
    }

    protected override void OnRemove(Player player, BuffSide side, int magnitude)
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.ModifyELO(side == BuffSide.Buff ? magnitude : -magnitude);
    }
}
