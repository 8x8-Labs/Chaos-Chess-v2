using UnityEngine;

public abstract class BuffSO : ScriptableObject, IBuffRuntime
{
    [TextArea]
    [SerializeField] private string description;

    [Header("Buff Side")]
    [SerializeField] private bool hasBuff = true;
    [SerializeField] private int buffValue = 1;

    [Header("Debuff Side")]
    [SerializeField] private bool hasDebuff = true;
    [SerializeField] private int debuffValue = 1;

    public string Description => description;
    public bool HasBuff => hasBuff;
    public bool HasDebuff => hasDebuff;

    public bool CanUse(BuffSide side)
    {
        return side == BuffSide.Buff ? hasBuff : hasDebuff;
    }

    public bool TryApply(Player player, BuffSide side)
    {
        if (!CanUse(side))
        {
            return false;
        }

        OnApply(player, side, GetMagnitude(side));
        return true;
    }

    public bool TryRemove(Player player, BuffSide side)
    {
        if (!CanUse(side))
        {
            return false;
        }

        OnRemove(player, side, GetMagnitude(side));
        return true;
    }

    public int GetMagnitude(BuffSide side)
    {
        int magnitude = side == BuffSide.Buff ? buffValue : debuffValue;
        return Mathf.Max(0, magnitude);
    }

    protected abstract void OnApply(Player player, BuffSide side, int magnitude);
    protected abstract void OnRemove(Player player, BuffSide side, int magnitude);
}
