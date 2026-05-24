using UnityEngine;

public abstract class BuffSO : ScriptableObject, IBuffRuntime
{
    [TextArea]
    [SerializeField] private string buffDescription;
    [TextArea]
    [SerializeField] private string debuffDescription;

    [SerializeField] private bool hasBuff = true;
    [SerializeField] private int buffWeight = 100;
    [SerializeField] private int buffValue = 1;
    [SerializeField] private bool useRandomBuffValue = false;
    [SerializeField] private Vector2Int buffRange = new Vector2Int(1, 1);
    [SerializeField] private bool useTensOnly = false;

    [SerializeField] private bool hasDebuff = true;
    [SerializeField] private int debuffWeight = 100;
    [SerializeField] private int debuffValue = 1;
    [SerializeField] private bool useRandomDebuffValue = false;
    [SerializeField] private Vector2Int debuffRange = new Vector2Int(1, 1);

    public bool HasBuff => hasBuff;
    public bool HasDebuff => hasDebuff;

    public bool CanUse(BuffSide side)
    {
        return side == BuffSide.Buff ? hasBuff : hasDebuff;
    }

    public int GetWeight(BuffSide side)
    {
        int weight = side == BuffSide.Buff ? buffWeight : debuffWeight;
        return Mathf.Max(0, weight);
    }

    public bool TryApply(Player player, BuffSide side)
    {
        return TryApply(player, side, RollMagnitude(side));
    }

    public bool TryRemove(Player player, BuffSide side)
    {
        return TryRemove(player, side, RollMagnitude(side));
    }

    public bool TryApply(Player player, BuffSide side, int magnitude)
    {
        if (!CanUse(side)) return false;
        OnApply(player, side, Mathf.Max(0, magnitude));
        return true;
    }

    public bool TryRemove(Player player, BuffSide side, int magnitude)
    {
        if (!CanUse(side)) return false;
        OnRemove(player, side, Mathf.Max(0, magnitude));
        return true;
    }

    public int RollMagnitude(BuffSide side)
    {
        if (side == BuffSide.Buff)
        {
            if (!useRandomBuffValue) return NormalizeMagnitude(buffValue);
            return RollFromRange(buffRange);
        }

        if (!useRandomDebuffValue) return NormalizeMagnitude(debuffValue);
        return RollFromRange(debuffRange);
    }

    public string GetDescription(BuffSide side)
    {
        string template = GetDescriptionTemplate(side);
        string valueText = RollMagnitude(side).ToString();
        return template.Replace("{value}", valueText);
    }

    public string GetDescription(BuffSide side, int fixedMagnitude)
    {
        string template = GetDescriptionTemplate(side);
        string valueText = Mathf.Max(0, fixedMagnitude).ToString();
        return template.Replace("{value}", valueText);
    }

    private string GetDescriptionTemplate(BuffSide side)
    {
        if (side == BuffSide.Buff)
        {
            return buffDescription;
        }

        return debuffDescription;
    }

    private int RollFromRange(Vector2Int range)
    {
        int min = Mathf.Min(range.x, range.y);
        int max = Mathf.Max(range.x, range.y);
        min = Mathf.Max(0, min);
        max = Mathf.Max(0, max);
        int rolled = Random.Range(min, max + 1);
        return NormalizeMagnitude(rolled);
    }

    private int NormalizeMagnitude(int value)
    {
        int normalized = Mathf.Max(0, value);
        if (!useTensOnly) return normalized;
        return Mathf.RoundToInt(normalized / 10f) * 10;
    }

    protected abstract void OnApply(Player player, BuffSide side, int magnitude);
    protected abstract void OnRemove(Player player, BuffSide side, int magnitude);
}
