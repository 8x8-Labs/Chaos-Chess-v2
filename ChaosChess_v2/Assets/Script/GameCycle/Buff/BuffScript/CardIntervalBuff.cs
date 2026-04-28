using UnityEngine;

/// <summary>
/// 카드 지급 주기를 1만큼 줄이는 버프
/// </summary>
[CreateAssetMenu(menuName = "Buff/CardInterval")]
public class CardIntervalBuff : BuffSO
{
    public int reduction = 1;

    public override void OnApply(Player player)
    {
        player.ModifyCardInterval(-reduction);
    }

    public override void OnRemove(Player player)
    {
        player.ModifyCardInterval(reduction);
    }
}