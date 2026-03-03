using System;

/// <summary>
/// Rarity tiers for tower loot drops.
/// </summary>
public enum LootRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Configurable data class for a single loot drop entry in a tower loot table.
/// All tuning happens here — no code changes needed to add/remove/rebalance loot.
/// </summary>
[Serializable]
public class LootDropDefinition
{
    public string itemId;
    public LootRarity rarity = LootRarity.Common;
    public float dropWeight = 1f;
    public int minAmount = 1;
    public int maxAmount = 1;

    /// <summary>
    /// Minimum floor index for this drop to appear. 0 = no minimum (any floor).
    /// </summary>
    public int minFloorElevation;

    /// <summary>
    /// Maximum floor index for this drop to appear. 0 = no maximum (any floor).
    /// </summary>
    public int maxFloorElevation;

    /// <summary>
    /// Minimum difficulty tier required. 0 = available at any tier.
    /// </summary>
    public int tierRequirement;
}
