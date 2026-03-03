using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class TowerControllerTests
{
    private TowerBuildingDefinitionSO _building;
    private TowerController _tower;

    [SetUp]
    public void SetUp()
    {
        _building = ScriptableObject.CreateInstance<TowerBuildingDefinitionSO>();
        _building.buildingName = "test_building";
        _building.requiredFragments = 4;

        // 5 chunks: 0-3 are normal floors, 4 is the boss floor
        for (int i = 0; i < 5; i++)
        {
            var chunk = new FloorChunkDefinition();
            chunk.spawnPoints.Add(new Vector3(i, 0, 0));
            chunk.lootNodes.Add(new Vector3(i, 1, 0));
            _building.chunks.Add(chunk);
        }
        _building.bossChunkIndex = 4;

        _tower = new TowerController();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_building);
    }

    // -- run initialization --

    [Test]
    public void StartRunSetsBuilding()
    {
        _tower.StartRun(_building);

        Assert.AreEqual(_building, _tower.CurrentBuilding);
    }

    [Test]
    public void StartRunResetsCarriedLoot()
    {
        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("rare_ore"));
        _tower.StartRun(_building);

        Assert.AreEqual(0, _tower.CarriedLoot.Count);
    }

    [Test]
    public void StartRunResetsCarriedFragments()
    {
        _tower.StartRun(_building);
        _tower.CollectFragment();
        _tower.StartRun(_building);

        Assert.AreEqual(0, _tower.CarriedFragments);
    }

    [Test]
    public void StartRunResetsClearedChunks()
    {
        _tower.StartRun(_building);
        _tower.ClearChunk(0);
        _tower.StartRun(_building);

        Assert.IsFalse(_tower.IsChunkCleared(0));
    }

    [Test]
    public void StartRunRandomizesFragmentPlacement()
    {
        _tower.StartRun(_building);

        int fragmentCount = 0;
        for (int i = 0; i < _building.chunks.Count; i++)
        {
            if (_tower.HasFragment(i))
                fragmentCount++;
        }

        // Should place exactly requiredFragments across non-boss chunks
        Assert.AreEqual(_building.requiredFragments, fragmentCount);
    }

    [Test]
    public void StartRunDoesNotPlaceFragmentOnBossChunk()
    {
        _tower.StartRun(_building);

        Assert.IsFalse(_tower.HasFragment(_building.bossChunkIndex));
    }

    [Test]
    public void StartRunSetsRunActive()
    {
        _tower.StartRun(_building);

        Assert.IsTrue(_tower.IsRunActive);
    }

    // -- chunk clearing --

    [Test]
    public void ClearChunkMarksChunkCleared()
    {
        _tower.StartRun(_building);
        _tower.ClearChunk(0);

        Assert.IsTrue(_tower.IsChunkCleared(0));
    }

    [Test]
    public void UnclearedChunkReportsNotCleared()
    {
        _tower.StartRun(_building);

        Assert.IsFalse(_tower.IsChunkCleared(0));
    }

    [Test]
    public void ClearMultipleChunks()
    {
        _tower.StartRun(_building);
        _tower.ClearChunk(0);
        _tower.ClearChunk(2);

        Assert.IsTrue(_tower.IsChunkCleared(0));
        Assert.IsFalse(_tower.IsChunkCleared(1));
        Assert.IsTrue(_tower.IsChunkCleared(2));
    }

    // -- loot collection --

    [Test]
    public void CollectLootAddsToCarried()
    {
        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("rare_ore"));

        Assert.AreEqual(1, _tower.CarriedLoot.Count);
        Assert.AreEqual("rare_ore", _tower.CarriedLoot[0].definitionId);
    }

    [Test]
    public void CollectMultipleLootItems()
    {
        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("rare_ore"));
        _tower.CollectLoot(ItemInstance.Create("power_shard"));
        _tower.CollectLoot(ItemInstance.Create("blueprint_a"));

        Assert.AreEqual(3, _tower.CarriedLoot.Count);
    }

    // -- fragment collection --

    [Test]
    public void CollectFragmentIncrementsCarried()
    {
        _tower.StartRun(_building);
        _tower.CollectFragment();

        Assert.AreEqual(1, _tower.CarriedFragments);
    }

    [Test]
    public void CollectMultipleFragments()
    {
        _tower.StartRun(_building);
        _tower.CollectFragment();
        _tower.CollectFragment();
        _tower.CollectFragment();

        Assert.AreEqual(3, _tower.CarriedFragments);
    }

    // -- extraction --

    [Test]
    public void ExtractBanksCarriedFragments()
    {
        _tower.StartRun(_building);
        _tower.CollectFragment();
        _tower.CollectFragment();
        int banked = _tower.Extract();

        Assert.AreEqual(2, banked);
        Assert.AreEqual(2, _tower.BankedFragments);
    }

    [Test]
    public void ExtractClearsCarriedFragments()
    {
        _tower.StartRun(_building);
        _tower.CollectFragment();
        _tower.Extract();

        Assert.AreEqual(0, _tower.CarriedFragments);
    }

    [Test]
    public void ExtractBanksCarriedLoot()
    {
        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("rare_ore"));
        _tower.CollectLoot(ItemInstance.Create("power_shard"));
        _tower.Extract();

        Assert.AreEqual(2, _tower.BankedLoot.Count);
    }

    [Test]
    public void ExtractClearsCarriedLoot()
    {
        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("rare_ore"));
        _tower.Extract();

        Assert.AreEqual(0, _tower.CarriedLoot.Count);
    }

    [Test]
    public void ExtractAccumulatesFragmentsAcrossRuns()
    {
        _tower.StartRun(_building);
        _tower.CollectFragment();
        _tower.Extract();

        _tower.StartRun(_building);
        _tower.CollectFragment();
        _tower.CollectFragment();
        int banked = _tower.Extract();

        Assert.AreEqual(3, banked);
        Assert.AreEqual(3, _tower.BankedFragments);
    }

    [Test]
    public void ExtractAccumulatesLootAcrossRuns()
    {
        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("ore_a"));
        _tower.Extract();

        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("ore_b"));
        _tower.Extract();

        Assert.AreEqual(2, _tower.BankedLoot.Count);
    }

    [Test]
    public void ExtractEndsRun()
    {
        _tower.StartRun(_building);
        _tower.Extract();

        Assert.IsFalse(_tower.IsRunActive);
    }

    // -- death --

    [Test]
    public void DieRemovesCarriedLoot()
    {
        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("rare_ore"));
        _tower.CollectLoot(ItemInstance.Create("power_shard"));
        _tower.Die();

        Assert.AreEqual(0, _tower.CarriedLoot.Count);
    }

    [Test]
    public void DieRemovesCarriedFragments()
    {
        _tower.StartRun(_building);
        _tower.CollectFragment();
        _tower.CollectFragment();
        _tower.Die();

        Assert.AreEqual(0, _tower.CarriedFragments);
    }

    [Test]
    public void DieKeepsBankedFragments()
    {
        _tower.StartRun(_building);
        _tower.CollectFragment();
        _tower.CollectFragment();
        _tower.Extract();

        _tower.StartRun(_building);
        _tower.CollectFragment();
        _tower.Die();

        Assert.AreEqual(2, _tower.BankedFragments);
    }

    [Test]
    public void DieKeepsBankedLoot()
    {
        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("rare_ore"));
        _tower.Extract();

        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("will_be_lost"));
        _tower.Die();

        Assert.AreEqual(1, _tower.BankedLoot.Count);
        Assert.AreEqual("rare_ore", _tower.BankedLoot[0].definitionId);
    }

    [Test]
    public void DieEndsRun()
    {
        _tower.StartRun(_building);
        _tower.Die();

        Assert.IsFalse(_tower.IsRunActive);
    }

    // -- boss unlock --

    [Test]
    public void BossLockedWithInsufficientFragments()
    {
        _tower.StartRun(_building);
        _tower.CollectFragment();
        _tower.CollectFragment();
        _tower.CollectFragment();
        _tower.Extract();

        Assert.IsFalse(_tower.UnlockBoss());
    }

    [Test]
    public void BossUnlocksWithExactFragments()
    {
        BankFragments(4);

        Assert.IsTrue(_tower.UnlockBoss());
    }

    [Test]
    public void BossUnlocksWithExcessFragments()
    {
        // Edge case: accumulated more than required across many runs
        BankFragments(6);

        Assert.IsTrue(_tower.UnlockBoss());
    }

    [Test]
    public void BossLockedWithZeroFragments()
    {
        _tower.StartRun(_building);
        _tower.Extract();

        Assert.IsFalse(_tower.UnlockBoss());
    }

    // -- boss completion --

    [Test]
    public void CompleteBossIncrementsTier()
    {
        Assert.AreEqual(1, _tower.CurrentTier);

        BankFragments(4);
        _tower.CompleteBoss();

        Assert.AreEqual(2, _tower.CurrentTier);
    }

    [Test]
    public void CompleteBossResetsBankedFragments()
    {
        BankFragments(4);
        _tower.CompleteBoss();

        Assert.AreEqual(0, _tower.BankedFragments);
    }

    [Test]
    public void CompleteBossKeepsBankedLoot()
    {
        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("rare_ore"));
        _tower.Extract();

        _tower.CompleteBoss();

        Assert.AreEqual(1, _tower.BankedLoot.Count);
    }

    [Test]
    public void CompleteBossEndsRun()
    {
        _tower.StartRun(_building);
        _tower.CompleteBoss();

        Assert.IsFalse(_tower.IsRunActive);
    }

    // -- tier progression --

    [Test]
    public void TierStartsAtOne()
    {
        Assert.AreEqual(1, _tower.CurrentTier);
    }

    [Test]
    public void MultipleBossCompletionsIncrementTier()
    {
        BankFragments(4);
        _tower.CompleteBoss();

        BankFragments(4);
        _tower.CompleteBoss();

        BankFragments(4);
        _tower.CompleteBoss();

        Assert.AreEqual(4, _tower.CurrentTier);
    }

    [Test]
    public void FragmentsResetEachCycle()
    {
        BankFragments(4);
        Assert.IsTrue(_tower.UnlockBoss());

        _tower.CompleteBoss();
        Assert.IsFalse(_tower.UnlockBoss());

        BankFragments(4);
        Assert.IsTrue(_tower.UnlockBoss());
    }

    // -- edge cases --

    [Test]
    public void StartRunWithFewerNonBossChunksThanFragments()
    {
        // Building with only 2 non-boss chunks but requires 4 fragments
        var smallBuilding = ScriptableObject.CreateInstance<TowerBuildingDefinitionSO>();
        smallBuilding.buildingName = "small_building";
        smallBuilding.requiredFragments = 4;
        smallBuilding.chunks.Add(new FloorChunkDefinition());
        smallBuilding.chunks.Add(new FloorChunkDefinition());
        smallBuilding.chunks.Add(new FloorChunkDefinition()); // boss
        smallBuilding.bossChunkIndex = 2;

        _tower.StartRun(smallBuilding);

        // Should clamp to available non-boss chunks (2)
        int fragmentCount = 0;
        for (int i = 0; i < smallBuilding.chunks.Count; i++)
        {
            if (_tower.HasFragment(i))
                fragmentCount++;
        }
        Assert.AreEqual(2, fragmentCount);

        Object.DestroyImmediate(smallBuilding);
    }

    [Test]
    public void ClearChunkOutOfRangeDoesNotThrow()
    {
        _tower.StartRun(_building);

        Assert.DoesNotThrow(() => _tower.ClearChunk(99));
        Assert.IsFalse(_tower.IsChunkCleared(99));
    }

    [Test]
    public void DoubleExtractDoesNotDuplicateLoot()
    {
        _tower.StartRun(_building);
        _tower.CollectLoot(ItemInstance.Create("rare_ore"));
        _tower.Extract();
        _tower.Extract(); // second extract with nothing carried

        Assert.AreEqual(1, _tower.BankedLoot.Count);
    }

    [Test]
    public void TwoRunsOnSameSOHaveIndependentFragmentState()
    {
        // First run: record which chunks have fragments
        _tower.StartRun(_building);
        var firstRunFragments = new HashSet<int>();
        for (int i = 0; i < _building.chunks.Count; i++)
        {
            if (_tower.HasFragment(i))
                firstRunFragments.Add(i);
        }
        Assert.AreEqual(_building.requiredFragments, firstRunFragments.Count);

        // Second run on the same SO: fragment state should be freshly randomized
        _tower.StartRun(_building);
        int secondRunCount = 0;
        for (int i = 0; i < _building.chunks.Count; i++)
        {
            if (_tower.HasFragment(i))
                secondRunCount++;
        }

        // The count must still be exactly requiredFragments (not accumulated from first run)
        Assert.AreEqual(_building.requiredFragments, secondRunCount);
    }

    [Test]
    public void TwoControllersOnSameSOHaveIndependentFragmentState()
    {
        // Two controllers sharing the same SO should not interfere with each other
        var tower2 = new TowerController();

        _tower.StartRun(_building);
        tower2.StartRun(_building);

        // Each controller should have exactly requiredFragments
        int count1 = 0, count2 = 0;
        for (int i = 0; i < _building.chunks.Count; i++)
        {
            if (_tower.HasFragment(i)) count1++;
            if (tower2.HasFragment(i)) count2++;
        }

        Assert.AreEqual(_building.requiredFragments, count1);
        Assert.AreEqual(_building.requiredFragments, count2);
    }

    [Test]
    public void RequiredFragmentsUsesDefinitionValue()
    {
        _building.requiredFragments = 2;

        BankFragments(2);

        Assert.IsTrue(_tower.UnlockBoss());
    }

    // -- helpers --

    private void BankFragments(int count)
    {
        _tower.StartRun(_building);
        for (int i = 0; i < count; i++)
            _tower.CollectFragment();
        _tower.Extract();
    }
}
