/// <summary>
/// Interface for anything an inserter can pull items from.
/// Implemented by belt output adapters, machine output adapters, and similar.
/// </summary>
public interface IItemSource
{
    /// <summary>
    /// Whether the source has at least one item available for extraction.
    /// </summary>
    bool HasItemAvailable { get; }

    /// <summary>
    /// Returns the item ID of the next available item without removing it.
    /// Returns null if no item is available.
    /// </summary>
    string PeekItemId();

    /// <summary>
    /// Attempts to extract one item from the source.
    /// </summary>
    /// <param name="itemId">The item ID of the extracted item, or null if extraction failed.</param>
    /// <returns>True if an item was successfully extracted.</returns>
    bool TryExtract(out string itemId);
}
