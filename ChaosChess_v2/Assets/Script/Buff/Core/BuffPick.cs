using System;
using UnityEngine;

[Serializable]
public struct BuffPick
{
    [SerializeField] private BuffSO definition;
    [SerializeField] private BuffSide side;
    [SerializeField] private int appliedMagnitude;
    [SerializeField] private bool hasAppliedMagnitude;

    public BuffSO Definition => definition;
    public BuffSide Side => side;

    public BuffPick(BuffSO definition, BuffSide side)
    {
        this.definition = definition;
        this.side = side;
        appliedMagnitude = 0;
        hasAppliedMagnitude = false;
    }

    public BuffPick(BuffSO definition, BuffSide side, int fixedMagnitude)
    {
        this.definition = definition;
        this.side = side;
        appliedMagnitude = Mathf.Max(0, fixedMagnitude);
        hasAppliedMagnitude = true;
    }

    public bool TryApply(Player player)
    {
        if (definition == null) return false;
        if (!hasAppliedMagnitude)
        {
            appliedMagnitude = definition.RollMagnitude(side);
            hasAppliedMagnitude = true;
        }
        return definition.TryApply(player, side, appliedMagnitude);
    }

    public bool TryRemove(Player player)
    {
        if (definition == null) return false;
        int magnitude = hasAppliedMagnitude ? appliedMagnitude : definition.RollMagnitude(side);
        return definition.TryRemove(player, side, magnitude);
    }
}
