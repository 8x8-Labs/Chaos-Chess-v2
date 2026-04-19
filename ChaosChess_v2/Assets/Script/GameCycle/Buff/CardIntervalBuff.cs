using UnityEngine;

[CreateAssetMenu(menuName = "Buff/CardInterval")]
public class CardIntervalBuff : BuffSO
{
    public int reduction = 1;

    public override void OnApply(Player player)
    {
        PlayerState.Instance.ModifyCardInterval(-reduction);
    }

    public override void OnRemove(Player player)
    {
        PlayerState.Instance.ModifyCardInterval(reduction);
    }
}