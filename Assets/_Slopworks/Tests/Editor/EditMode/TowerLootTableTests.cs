using System;
using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class TowerLootTableTests
{
    private System.Random _rng;

    [SetUp]
    public void SetUp()
    {
        // Seeded RNG for deterministic tests
        _rng = new System.Random(42);
    }

    // -- basic resolution --

    [Test]
    public void ResolveDropFromSingleEntry()
    {
        var entries = new List<LootDropDefinition>
        {
            MakeEntry("iron_ore", LootRarity.Common, 1f)
        };
        var table = new TowerLootTable(entries);

        var drop = table.ResolveDrop(0, 1, _rng);

        Assert.IsNotNull(drop);
        Assert.AreEqual("iron_ore", drop.Value.itemId);
    }

    [Test]
    public void ResolveDropReturnsCorrectRarity()
    {
        var entries = new List<LootDropDefinition>
        {
            MakeEntry("rare_gem", LootRarity.Rare, 1f)
        };
        var table = new TowerLootTable(entries);

        var drop = table.ResolveDrop(0, 1, _rng);

        Assert.AreEqual(LootRarity.Rare, drop.Value.rarity);
    }

    [Test]
    public void EmptyTableReturnsNull()
    {
        var table = new TowerLootTable(new List<LootDropDefinition>());

        var drop = table.ResolveDrop(0, 1, _rng);

        Assert.IsNull(drop);
    }

    [Test]
    public void NullEntriesReturnsNull()
    {
        var table = new TowerLootTable(null);

        var drop = table.ResolveDrop(0, 1, _rng);

        Assert.IsNull(drop);
    }

    // -- weighted selection --

    [Test]
    public void HigherWeightDropsMoreFrequently()
    {
        var entries = new List<LootDropDefinition>
        {
            MakeEntry("common_ore", LootRarity.Common, 90f),
            MakeEntry("legendary_gem", LootRarity.Legendary, 1f)
        };
        var table = new TowerLootTable(entries);

        int commonCount = 0;
        int legendaryCount = 0;
        var rng = new System.Random(123);

        for (int i = 0; i < 1000; i++)
        {
            var drop = table.ResolveDrop(0, 1, rng);
            if (drop.Value.itemId == "common_ore") commonCount++;
            if (drop.Value.itemId == "legendary_gem") legendaryCount++;
        }

        // With 90:1 weight ratio, common should appear far more often
        Assert.Greater(commonCount, legendaryCount * 5);
    }

    [Test]
    public void ZeroWeightEntryNeverDrops()
    {
        var entries = new List<LootDropDefinition>
        {
            MakeEntry("normal", LootRarity.Common, 1f),
            MakeEntry("disabled", LootRarity.Common, 0f)
        };
        var table = new TowerLootTable(entries);

        var rng = new System.Random(456);
        for (int i = 0; i < 100; i++)
        {
            var drop = table.ResolveDrop(0, 1, rng);
            Assert.IsNotNull(drop);
            Assert.AreNotEqual("disabled", drop.Value.itemId);
        }
    }

    [Test]
    public void SingleEntryAlwaysSelected()
    {
        var entries = new List<LootDropDefinition>
        {
            MakeEntry("only_one", LootRarity.Common, 1f)
        };
        var table = new TowerLootTable(entries);

        var rng = new System.Random(789);
        for (int i = 0; i < 50; i++)
        {
            var drop = table.ResolveDrop(0, 1, rng);
            Assert.AreEqual("only_one", drop.Value.itemId);
        }
    }

    // -- floor elevation filtering --

    [Test]
    public void MinFloorElevationFiltersLowerFloors()
    {
        var entry = MakeEntry("upper_only", LootRarity.Rare, 1f);
        entry.minFloorElevation = 3;
        var table = new TowerLootTable(new List<LootDropDefinition> { entry });

        // Floor 2 should not get this drop
        var drop = table.ResolveDrop(2, 1, _rng);
        Assert.IsNull(drop);

        // Floor 3 should get it
        drop = table.ResolveDrop(3, 1, _rng);
        Assert.IsNotNull(drop);
        Assert.AreEqual("upper_only", drop.Value.itemId);
    }

    [Test]
    public void MaxFloorElevationFiltersHigherFloors()
    {
        var entry = MakeEntry("lower_only", LootRarity.Common, 1f);
        entry.maxFloorElevation = 2;
        var table = new TowerLootTable(new List<LootDropDefinition> { entry });

        // Floor 2 should get this drop
        var drop = table.ResolveDrop(2, 1, _rng);
        Assert.IsNotNull(drop);

        // Floor 3 should not
        drop = table.ResolveDrop(3, 1, _rng);
        Assert.IsNull(drop);
    }

    [Test]
    public void FloorRangeFiltersCorrectly()
    {
        var entry = MakeEntry("mid_floors", LootRarity.Uncommon, 1f);
        entry.minFloorElevation = 2;
        entry.maxFloorElevation = 4;
        var table = new TowerLootTable(new List<LootDropDefinition> { entry });

        Assert.IsNull(table.ResolveDrop(1, 1, _rng));
        Assert.IsNotNull(table.ResolveDrop(2, 1, new System.Random(1)));
        Assert.IsNotNull(table.ResolveDrop(3, 1, new System.Random(2)));
        Assert.IsNotNull(table.ResolveDrop(4, 1, new System.Random(3)));
        Assert.IsNull(table.ResolveDrop(5, 1, new System.Random(4)));
    }

    [Test]
    public void ZeroFloorElevationMeansNoFilter()
    {
        var entry = MakeEntry("any_floor", LootRarity.Common, 1f);
        entry.minFloorElevation = 0;
        entry.maxFloorElevation = 0;
        var table = new TowerLootTable(new List<LootDropDefinition> { entry });

        // Should appear on any floor
        Assert.IsNotNull(table.ResolveDrop(0, 1, new System.Random(1)));
        Assert.IsNotNull(table.ResolveDrop(10, 1, new System.Random(2)));
        Assert.IsNotNull(table.ResolveDrop(99, 1, new System.Random(3)));
    }

    // -- tier filtering --

    [Test]
    public void TierRequirementFiltersLowerTiers()
    {
        var entry = MakeEntry("tier3_item", LootRarity.Epic, 1f);
        entry.tierRequirement = 3;
        var table = new TowerLootTable(new List<LootDropDefinition> { entry });

        Assert.IsNull(table.ResolveDrop(0, 1, _rng));
        Assert.IsNull(table.ResolveDrop(0, 2, _rng));
        Assert.IsNotNull(table.ResolveDrop(0, 3, new System.Random(1)));
        Assert.IsNotNull(table.ResolveDrop(0, 4, new System.Random(2)));
    }

    [Test]
    public void ZeroTierRequirementMeansNoFilter()
    {
        var entry = MakeEntry("any_tier", LootRarity.Common, 1f);
        entry.tierRequirement = 0;
        var table = new TowerLootTable(new List<LootDropDefinition> { entry });

        Assert.IsNotNull(table.ResolveDrop(0, 1, new System.Random(1)));
        Assert.IsNotNull(table.ResolveDrop(0, 5, new System.Random(2)));
    }

    [Test]
    public void CombinedFloorAndTierFiltering()
    {
        var entry = MakeEntry("endgame_loot", LootRarity.Legendary, 1f);
        entry.minFloorElevation = 5;
        entry.tierRequirement = 2;
        var table = new TowerLootTable(new List<LootDropDefinition> { entry });

        // Neither condition met
        Assert.IsNull(table.ResolveDrop(1, 1, _rng));
        // Floor met, tier not
        Assert.IsNull(table.ResolveDrop(5, 1, _rng));
        // Tier met, floor not
        Assert.IsNull(table.ResolveDrop(1, 2, _rng));
        // Both met
        Assert.IsNotNull(table.ResolveDrop(5, 2, new System.Random(1)));
    }

    // -- amount randomization --

    [Test]
    public void FixedAmountReturnsSameValue()
    {
        var entry = MakeEntry("fixed_item", LootRarity.Common, 1f);
        entry.minAmount = 3;
        entry.maxAmount = 3;
        var table = new TowerLootTable(new List<LootDropDefinition> { entry });

        var rng = new System.Random(111);
        for (int i = 0; i < 20; i++)
        {
            var drop = table.ResolveDrop(0, 1, rng);
            Assert.AreEqual(3, drop.Value.amount);
        }
    }

    [Test]
    public void AmountWithinMinMaxRange()
    {
        var entry = MakeEntry("variable_item", LootRarity.Common, 1f);
        entry.minAmount = 1;
        entry.maxAmount = 10;
        var table = new TowerLootTable(new List<LootDropDefinition> { entry });

        var rng = new System.Random(222);
        for (int i = 0; i < 100; i++)
        {
            var drop = table.ResolveDrop(0, 1, rng);
            Assert.GreaterOrEqual(drop.Value.amount, 1);
            Assert.LessOrEqual(drop.Value.amount, 10);
        }
    }

    [Test]
    public void AmountVariesWithRange()
    {
        var entry = MakeEntry("varied_item", LootRarity.Common, 1f);
        entry.minAmount = 1;
        entry.maxAmount = 100;
        var table = new TowerLootTable(new List<LootDropDefinition> { entry });

        var rng = new System.Random(333);
        var seen = new HashSet<int>();
        for (int i = 0; i < 50; i++)
        {
            var drop = table.ResolveDrop(0, 1, rng);
            seen.Add(drop.Value.amount);
        }

        // With range 1-100 over 50 rolls, should see more than 1 unique value
        Assert.Greater(seen.Count, 1);
    }

    // -- multiple drops --

    [Test]
    public void ResolveDropsReturnsRequestedCount()
    {
        var entries = new List<LootDropDefinition>
        {
            MakeEntry("item_a", LootRarity.Common, 1f),
            MakeEntry("item_b", LootRarity.Common, 1f)
        };
        var table = new TowerLootTable(entries);

        var drops = table.ResolveDrops(5, 0, 1, _rng);

        Assert.AreEqual(5, drops.Count);
    }

    [Test]
    public void ResolveDropsReturnsEmptyForEmptyTable()
    {
        var table = new TowerLootTable(new List<LootDropDefinition>());

        var drops = table.ResolveDrops(3, 0, 1, _rng);

        Assert.AreEqual(0, drops.Count);
    }

    [Test]
    public void ResolveDropsReturnsFewerWhenFiltered()
    {
        var entry = MakeEntry("tier2_only", LootRarity.Rare, 1f);
        entry.tierRequirement = 2;
        var table = new TowerLootTable(new List<LootDropDefinition> { entry });

        // Tier 1 should produce 0 drops even when asking for 5
        var drops = table.ResolveDrops(5, 0, 1, _rng);
        Assert.AreEqual(0, drops.Count);
    }

    [Test]
    public void ResolveZeroDropsReturnsEmpty()
    {
        var entries = new List<LootDropDefinition>
        {
            MakeEntry("item", LootRarity.Common, 1f)
        };
        var table = new TowerLootTable(entries);

        var drops = table.ResolveDrops(0, 0, 1, _rng);

        Assert.AreEqual(0, drops.Count);
    }

    // -- mixed filtering --

    [Test]
    public void FilteredEntriesExcludedFromWeightedSelection()
    {
        var entries = new List<LootDropDefinition>
        {
            MakeEntry("common_ore", LootRarity.Common, 100f),
            MakeEntryWithTier("tier3_gem", LootRarity.Legendary, 100f, 3)
        };
        var table = new TowerLootTable(entries);

        // At tier 1, only common_ore should drop (tier3_gem filtered out)
        var rng = new System.Random(444);
        for (int i = 0; i < 50; i++)
        {
            var drop = table.ResolveDrop(0, 1, rng);
            Assert.AreEqual("common_ore", drop.Value.itemId);
        }
    }

    [Test]
    public void AllEntriesFilteredReturnsNull()
    {
        var entries = new List<LootDropDefinition>
        {
            MakeEntryWithTier("tier5_only", LootRarity.Legendary, 1f, 5),
            MakeEntryWithFloor("high_floor_only", LootRarity.Rare, 1f, 10, 0)
        };
        var table = new TowerLootTable(entries);

        // Tier 1, floor 0: both filtered out
        var drop = table.ResolveDrop(0, 1, _rng);
        Assert.IsNull(drop);
    }

    // -- helpers --

    private LootDropDefinition MakeEntry(string itemId, LootRarity rarity, float weight)
    {
        return new LootDropDefinition
        {
            itemId = itemId,
            rarity = rarity,
            dropWeight = weight,
            minAmount = 1,
            maxAmount = 1,
            minFloorElevation = 0,
            maxFloorElevation = 0,
            tierRequirement = 0
        };
    }

    private LootDropDefinition MakeEntryWithTier(string itemId, LootRarity rarity, float weight, int tier)
    {
        var entry = MakeEntry(itemId, rarity, weight);
        entry.tierRequirement = tier;
        return entry;
    }

    private LootDropDefinition MakeEntryWithFloor(string itemId, LootRarity rarity, float weight, int minFloor, int maxFloor)
    {
        var entry = MakeEntry(itemId, rarity, weight);
        entry.minFloorElevation = minFloor;
        entry.maxFloorElevation = maxFloor;
        return entry;
    }
}
