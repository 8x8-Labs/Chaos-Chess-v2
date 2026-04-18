/// <summary>지원 카드 지급 주기를 줄이는 버프</summary>
public class CardIntervalBuff : IPlayerBuff
{
    private readonly int _reduction;

    /// <param name="reduction">줄일 턴 수 (기본값 1)</param>
    public CardIntervalBuff(int reduction = 1)
    {
        _reduction = reduction;
    }

    public void OnApply(Player player) => PlayerState.Instance.ModifyCardInterval(-_reduction);
    public void OnRemove(Player player) => PlayerState.Instance.ModifyCardInterval(_reduction);
}
