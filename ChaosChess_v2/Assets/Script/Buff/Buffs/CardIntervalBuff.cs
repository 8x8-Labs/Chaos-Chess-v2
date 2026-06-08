using UnityEngine;

[CreateAssetMenu(menuName = "BuffSystem/Definitions/CardInterval")]
public class CardIntervalBuff : BuffSO
{
    protected override void OnApply(Player player, BuffSide side, int magnitude)
    {
        int delta = side == BuffSide.Buff ? -magnitude : magnitude;
        player.ModifyCardInterval(delta);
    }

    protected override void OnRemove(Player player, BuffSide side, int magnitude)
    {
        int delta = side == BuffSide.Buff ? magnitude : -magnitude;
        player.ModifyCardInterval(delta);
    }
}
