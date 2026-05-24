public interface IBuffRuntime
{
    bool TryApply(Player player, BuffSide side);
    bool TryRemove(Player player, BuffSide side);
}
