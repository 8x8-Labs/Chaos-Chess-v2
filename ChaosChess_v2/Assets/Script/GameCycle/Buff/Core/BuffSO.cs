using UnityEngine;

public abstract class BuffSO : ScriptableObject, IPlayerBuff
{
    [TextArea]
    public string description;
    public BuffType type;

    public abstract void OnApply(Player player);
    public abstract void OnRemove(Player player);
}

public enum BuffType
{
    Buff,
    Debuff
}