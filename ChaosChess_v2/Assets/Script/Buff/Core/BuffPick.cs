using System;
using UnityEngine;

[Serializable]
public struct BuffPick
{
    [SerializeField] private BuffSO definition;
    [SerializeField] private BuffSide side;

    public BuffSO Definition => definition;
    public BuffSide Side => side;

    public BuffPick(BuffSO definition, BuffSide side)
    {
        this.definition = definition;
        this.side = side;
    }

    public bool TryApply(Player player)
    {
        if (definition == null) return false;
        return definition.TryApply(player, side);
    }

    public bool TryRemove(Player player)
    {
        if (definition == null) return false;
        return definition.TryRemove(player, side);
    }
}
