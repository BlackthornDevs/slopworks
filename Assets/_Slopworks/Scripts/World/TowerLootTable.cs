using System;
using System.Collections.Generic;

/// <summary>
/// Result of a loot drop resolution.
/// </summary>
public struct LootDrop
{
    public string itemId;
    public int amount;
    public LootRarity rarity;
}

/// <summary>
/// Data-driven loot resolver for tower runs. Filters entries by floor elevation
/// and difficulty tier, then selects via weighted random. All tuning is in data.
/// </summary>
public class TowerLootTable
{
    private readonly List<LootDropDefinition> _entries;

    public TowerLootTable(List<LootDropDefinition> entries)
    {
        _entries = entries ?? new List<LootDropDefinition>();
    }

    /// <summary>
    /// Resolve a single drop. Returns null if no valid entries exist for the given context.
    /// </summary>
    /// <param name="currentFloor">Current floor index (0-based).</param>
    /// <param name="currentTier">Current difficulty tier (1-based).</param>
    /// <param name="rng">Random source for deterministic testing.</param>
    public LootDrop? ResolveDrop(int currentFloor, int currentTier, Random rng)
    {
        var filtered = FilterEntries(currentFloor, currentTier);
        if (filtered.Count == 0)
            return null;

        var selected = WeightedSelect(filtered, rng);
        if (selected == null)
            return null;

        int amount = selected.minAmount == selected.maxAmount
            ? selected.minAmount
            : rng.Next(selected.minAmount, selected.maxAmount + 1);

        return new LootDrop
        {
            itemId = selected.itemId,
            amount = amount,
            rarity = selected.rarity
        };
    }

    /// <summary>
    /// Resolve multiple drops. Each roll is independent.
    /// </summary>
    public List<LootDrop> ResolveDrops(int count, int currentFloor, int currentTier, Random rng)
    {
        var results = new List<LootDrop>();
        for (int i = 0; i < count; i++)
        {
            var drop = ResolveDrop(currentFloor, currentTier, rng);
            if (drop.HasValue)
                results.Add(drop.Value);
        }
        return results;
    }

    private List<LootDropDefinition> FilterEntries(int currentFloor, int currentTier)
    {
        var result = new List<LootDropDefinition>();
        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];

            if (entry.minFloorElevation > 0 && currentFloor < entry.minFloorElevation)
                continue;

            if (entry.maxFloorElevation > 0 && currentFloor > entry.maxFloorElevation)
                continue;

            if (entry.tierRequirement > 0 && currentTier < entry.tierRequirement)
                continue;

            if (entry.dropWeight <= 0f)
                continue;

            result.Add(entry);
        }
        return result;
    }

    private LootDropDefinition WeightedSelect(List<LootDropDefinition> candidates, Random rng)
    {
        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += candidates[i].dropWeight;

        if (totalWeight <= 0f)
            return null;

        float roll = (float)(rng.NextDouble() * totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += candidates[i].dropWeight;
            if (roll < cumulative)
                return candidates[i];
        }

        // Fallback for floating-point edge case
        return candidates[candidates.Count - 1];
    }
}
