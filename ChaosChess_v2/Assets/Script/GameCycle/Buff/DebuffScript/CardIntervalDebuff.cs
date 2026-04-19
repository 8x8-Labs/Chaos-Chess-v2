using UnityEngine;

/// <summary>
/// 카드 지급 주기를 n만큼 늘리는 디버프
/// </summary>
[CreateAssetMenu(menuName = "Deuff/CardInterval")]
public class CardIntervalDebuff : BuffSO
{
    public int reduction = 1;

    public override void OnApply(Player player)
    {
        player.ModifyCardInterval(reduction);
    }

    public override void OnRemove(Player player)
    {
        player.ModifyCardInterval(reduction);
    }
}