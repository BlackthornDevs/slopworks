/// <summary>
/// Interface for anything an inserter can push items into.
/// Implemented by belt input adapters, machine input adapters, and similar.
/// </summary>
public interface IItemDestination
{
    /// <summary>
    /// Whether the destination can currently accept the specified item.
    /// </summary>
    /// <param name="itemId">The item type to check.</param>
    /// <returns>True if the item would be accepted.</returns>
    bool CanAccept(string itemId);

    /// <summary>
    /// Attempts to insert one item into the destination.
    /// </summary>
    /// <param name="itemId">The item type to insert.</param>
    /// <returns>True if the item was successfully inserted.</returns>
    bool TryInsert(string itemId);
}
