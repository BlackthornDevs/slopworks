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

    // -- extraction --

    [Test]
    public void ExtractBanksFragments()
    {
        _tower.StartRun(_building);
        int banked = _tower.Extract(2);

        Assert.AreEqual(2, banked);
        Assert.AreEqual(2, _tower.BankedFragments);
    }

    [Test]
    public void ExtractAccumulatesFragmentsAcrossRuns()
    {
        _tower.StartRun(_building);
        _tower.Extract(1);

        _tower.StartRun(_building);
        int banked = _tower.Extract(2);

        Assert.AreEqual(3, banked);
        Assert.AreEqual(3, _tower.BankedFragments);
    }

    [Test]
    public void ExtractEndsRun()
    {
        _tower.StartRun(_building);
        _tower.Extract(0);

        Assert.IsFalse(_tower.IsRunActive);
    }

    [Test]
    public void ExtractWithZeroFragments()
    {
        _tower.StartRun(_building);
        int banked = _tower.Extract(0);

        Assert.AreEqual(0, banked);
        Assert.AreEqual(0, _tower.BankedFragments);
    }

    // -- death --

    [Test]
    public void DieKeepsBankedFragments()
    {
        _tower.StartRun(_building);
        _tower.Extract(2);

        _tower.StartRun(_building);
        _tower.Die();

        Assert.AreEqual(2, _tower.BankedFragments);
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

        Assert.IsFalse(_tower.UnlockBoss(3));
    }

    [Test]
    public void BossUnlocksWithExactBankedFragments()
    {
        BankFragments(4);

        Assert.IsTrue(_tower.UnlockBoss(0));
    }

    [Test]
    public void BossUnlocksWithExactCarriedFragments()
    {
        _tower.StartRun(_building);

        Assert.IsTrue(_tower.UnlockBoss(4));
    }

    [Test]
    public void BossUnlocksWithMixedCarriedAndBanked()
    {
        // Bank 2, carry 2 = 4 total, meets requirement
        _tower.StartRun(_building);
        _tower.Extract(2);

        _tower.StartRun(_building);

        Assert.IsTrue(_tower.UnlockBoss(2));
    }

    [Test]
    public void BossUnlocksWithExcessFragments()
    {
        BankFragments(6);

        Assert.IsTrue(_tower.UnlockBoss(0));
    }

    [Test]
    public void BossLockedWithZeroFragments()
    {
        _tower.StartRun(_building);

        Assert.IsFalse(_tower.UnlockBoss(0));
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
    public void CompleteBossDoesNotResetBankedFragments()
    {
        BankFragments(4);
        _tower.CompleteBoss();

        Assert.AreEqual(4, _tower.BankedFragments);
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
    public void FragmentsResetEachCycleViaConsume()
    {
        BankFragments(4);
        Assert.IsTrue(_tower.UnlockBoss(0));

        _tower.ConsumeFragments();
        _tower.CompleteBoss();
        Assert.IsFalse(_tower.UnlockBoss(0));

        BankFragments(4);
        Assert.IsTrue(_tower.UnlockBoss(0));
    }

    // -- fragment consumption --

    [Test]
    public void ConsumeFragmentsResetsBankedToZero()
    {
        BankFragments(3);
        int consumed = _tower.ConsumeFragments();

        Assert.AreEqual(3, consumed);
        Assert.AreEqual(0, _tower.BankedFragments);
    }

    [Test]
    public void ConsumeFragmentsWithZeroBanked()
    {
        _tower.StartRun(_building);
        int consumed = _tower.ConsumeFragments();

        Assert.AreEqual(0, consumed);
        Assert.AreEqual(0, _tower.BankedFragments);
    }

    [Test]
    public void ConsumeThenCompleteBossTiersUpWithZeroFragments()
    {
        BankFragments(4);
        _tower.ConsumeFragments();
        _tower.StartRun(_building);
        _tower.CompleteBoss();

        Assert.AreEqual(2, _tower.CurrentTier);
        Assert.AreEqual(0, _tower.BankedFragments);
        Assert.IsFalse(_tower.IsRunActive);
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

        Assert.IsTrue(_tower.UnlockBoss(0));
    }

    // -- helpers --

    private void BankFragments(int count)
    {
        _tower.StartRun(_building);
        _tower.Extract(count);
    }
}
