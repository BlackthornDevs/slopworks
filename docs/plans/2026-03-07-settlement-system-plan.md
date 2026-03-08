# Settlement management system implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build an overworld settlement system where players discover ruins, repair them with materials, claim them for production/territory/unlocks, assign NPC workers, upgrade through tiers, and connect buildings with player-built roads that form supply lines to the factory.

**Architecture:** Zone graph pattern — buildings are nodes, roads are edges. All simulation logic in plain C# (D-004), thin MonoBehaviour wrappers. Server-authoritative for co-op multiplayer via FishNet SyncDictionary/SyncList. Separate from Kevin's interior exploration system (Scripts/World/). New code lives in Scripts/Settlement/.

**Tech Stack:** Unity 2022 LTS, C#, FishNet (NetworkBehaviour), NUnit (EditMode tests), URP

**Design doc:** `docs/plans/2026-03-07-settlement-system-design.md`

**Reference patterns:**
- D-004 simulation: `Scripts/World/TowerController.cs`, `Scripts/World/BuildingState.cs`
- SO definition: `Scripts/World/TowerBuildingDefinitionSO.cs`, `Scripts/World/BuildingDefinitionSO.cs`
- IInteractable: `Scripts/Core/IInteractable.cs`
- MonoBehaviour wrapper: `Scripts/World/MEPRestorePointBehaviour.cs`, `Scripts/Automation/StorageBehaviour.cs`
- Item system: `Scripts/Core/ItemDefinitionSO.cs`, `Scripts/Core/ItemRegistry.cs`
- Test pattern: `Tests/Editor/EditMode/TowerControllerTests.cs`

---

## Task 1: Data definitions (enums, structs, data classes)

**Files:**
- Create: `Assets/_Slopworks/Scripts/Settlement/SettlementEnums.cs`
- Create: `Assets/_Slopworks/Scripts/Settlement/RepairStageDefinition.cs`
- Create: `Assets/_Slopworks/Scripts/Settlement/UpgradeTierDefinition.cs`
- Create: `Assets/_Slopworks/Scripts/Settlement/ProductionDefinition.cs`
- Create: `Assets/_Slopworks/Scripts/Settlement/SettlementEvents.cs`

**Step 1: Create the Settlement directory**

```bash
mkdir -p Assets/_Slopworks/Scripts/Settlement
```

**Step 2: Write SettlementEnums.cs**

```csharp
public enum SettlementBuildingType
{
    Farmstead,
    Workshop,
    Watchtower,
    Depot,
    Market,
    Barracks
}

public enum SettlementCapability
{
    None,
    CraftingStation,
    WeaponWorkbench,
    Merchant,
    FastTravel,
    EarlyWarning,
    WaterPurification,
    ResearchBench,
    RecruitNPCs,
    DefensePatrols
}
```

**Step 3: Write RepairStageDefinition.cs**

```csharp
using System;
using UnityEngine;

[Serializable]
public class RepairStageDefinition
{
    public string[] requiredItemIds;
    public int[] requiredAmounts;
    public GameObject[] addedPiecePrefabs;
    public SettlementCapability unlockedCapability = SettlementCapability.None;
}
```

**Step 4: Write UpgradeTierDefinition.cs**

```csharp
using System;
using UnityEngine;

[Serializable]
public class UpgradeTierDefinition
{
    public string tierName;
    public string[] requiredItemIds;
    public int[] requiredAmounts;
    public GameObject[] addedPiecePrefabs;
    public ProductionDefinition productionOverride;
    public float territoryBonus;
    public int workerSlotsBonus;
    public SettlementCapability unlockedCapability = SettlementCapability.None;
}
```

**Step 5: Write ProductionDefinition.cs**

```csharp
using System;

[Serializable]
public class ProductionDefinition
{
    public string producedItemId;
    public int producedAmount = 1;
    public float productionInterval = 30f;
    public bool requiresSupplyLine;
}
```

**Step 6: Write SettlementEvents.cs**

```csharp
public struct BuildingRepairedEvent
{
    public string BuildingId;
    public int NewRepairLevel;
}

public struct BuildingClaimedEvent
{
    public string BuildingId;
}

public struct BuildingUpgradedEvent
{
    public string BuildingId;
    public int NewTier;
}

public struct RoadBuiltEvent
{
    public string BuildingIdA;
    public string BuildingIdB;
}

public struct ProductionCollectedEvent
{
    public string BuildingId;
    public string ItemId;
    public int Amount;
}

public struct WorkerAssignedEvent
{
    public string BuildingId;
    public string WorkerId;
}

public struct WorkerUnassignedEvent
{
    public string BuildingId;
    public string WorkerId;
}
```

**Step 7: Verify compilation**

Use MCP `recompile_scripts` or check Unity console for errors.

**Step 8: Commit**

```bash
git add Assets/_Slopworks/Scripts/Settlement/
git commit -m "add settlement data definitions: enums, structs, event types"
```

---

## Task 2: SettlementBuildingDefinitionSO

**Files:**
- Create: `Assets/_Slopworks/Scripts/Settlement/SettlementBuildingDefinitionSO.cs`

**Step 1: Write the ScriptableObject**

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "Slopworks/Settlement/Building Definition")]
public class SettlementBuildingDefinitionSO : ScriptableObject
{
    public string buildingId;
    public string displayName;
    public SettlementBuildingType buildingType;

    [Header("Repair")]
    public RepairStageDefinition[] repairStages;

    [Header("Production")]
    public ProductionDefinition production;

    [Header("Territory")]
    public float territoryRadius = 20f;
    public float connectionRange = 100f;

    [Header("Workers")]
    public int workerSlots = 2;
    public float workerBonusPerSlot = 0.25f;

    [Header("Upgrades")]
    public UpgradeTierDefinition[] upgradeTiers;

    public int MaxRepairLevel => repairStages != null ? repairStages.Length : 0;
}
```

**Step 2: Verify compilation**

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Settlement/SettlementBuildingDefinitionSO.cs
git commit -m "add SettlementBuildingDefinitionSO for settlement building config"
```

---

## Task 3: SettlementBuilding simulation class + tests (TDD)

**Files:**
- Create: `Assets/_Slopworks/Scripts/Settlement/SettlementBuilding.cs`
- Create: `Assets/_Slopworks/Tests/Editor/EditMode/SettlementBuildingTests.cs`

**Step 1: Write the failing tests first**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SettlementBuildingTests
{
    private SettlementBuildingDefinitionSO _def;
    private SettlementBuilding _building;

    [SetUp]
    public void SetUp()
    {
        _def = ScriptableObject.CreateInstance<SettlementBuildingDefinitionSO>();
        _def.buildingId = "test_farm";
        _def.displayName = "Test Farm";
        _def.buildingType = SettlementBuildingType.Farmstead;
        _def.territoryRadius = 20f;
        _def.connectionRange = 100f;
        _def.workerSlots = 3;
        _def.workerBonusPerSlot = 0.25f;

        _def.repairStages = new RepairStageDefinition[]
        {
            new() { requiredItemIds = new[] { "metal_plate" }, requiredAmounts = new[] { 5 } },
            new() { requiredItemIds = new[] { "concrete" }, requiredAmounts = new[] { 3 } },
            new() { requiredItemIds = new[] { "wiring" }, requiredAmounts = new[] { 2 } },
        };

        _def.production = new ProductionDefinition
        {
            producedItemId = "raw_food",
            producedAmount = 1,
            productionInterval = 10f,
            requiresSupplyLine = false
        };

        _def.upgradeTiers = new UpgradeTierDefinition[]
        {
            new()
            {
                tierName = "greenhouse",
                requiredItemIds = new[] { "glass" },
                requiredAmounts = new[] { 10 },
                territoryBonus = 5f,
                workerSlotsBonus = 1,
                productionOverride = new ProductionDefinition
                {
                    producedItemId = "medicine",
                    producedAmount = 1,
                    productionInterval = 20f,
                    requiresSupplyLine = true
                }
            }
        };

        _building = new SettlementBuilding("test_farm", _def, new Vector3(50, 0, 300));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_def);
    }

    // --- Repair ---

    [Test]
    public void StartsAtRepairLevelZero()
    {
        Assert.AreEqual(0, _building.RepairLevel);
        Assert.IsFalse(_building.IsClaimed);
    }

    [Test]
    public void AdvanceRepairIncrementsLevel()
    {
        _building.AdvanceRepair();
        Assert.AreEqual(1, _building.RepairLevel);
    }

    [Test]
    public void CannotRepairPastMaxLevel()
    {
        for (int i = 0; i < 5; i++) _building.AdvanceRepair();
        Assert.AreEqual(3, _building.RepairLevel);
    }

    [Test]
    public void IsClaimedWhenFullyRepaired()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        Assert.IsTrue(_building.IsClaimed);
    }

    [Test]
    public void GetRepairRequirementsReturnsCurrentStage()
    {
        var reqs = _building.GetRepairRequirements();
        Assert.AreEqual(1, reqs.Length);
        Assert.AreEqual("metal_plate", reqs[0].itemId);
        Assert.AreEqual(5, reqs[0].amount);
    }

    [Test]
    public void GetRepairRequirementsReturnsEmptyWhenFullyRepaired()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        var reqs = _building.GetRepairRequirements();
        Assert.AreEqual(0, reqs.Length);
    }

    [Test]
    public void RepairFiresEvent()
    {
        int firedLevel = -1;
        _building.OnRepaired += (id, level) => firedLevel = level;
        _building.AdvanceRepair();
        Assert.AreEqual(1, firedLevel);
    }

    [Test]
    public void ClaimFiresEvent()
    {
        bool fired = false;
        _building.OnClaimed += id => fired = true;
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        Assert.IsTrue(fired);
    }

    // --- Production ---

    [Test]
    public void DoesNotProduceWhenUnclaimed()
    {
        string producedItem = null;
        _building.OnProduced += (id, itemId, amount) => producedItem = itemId;
        _building.Tick(100f);
        Assert.IsNull(producedItem);
    }

    [Test]
    public void ProducesAfterClaimAndInterval()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        string producedItem = null;
        int producedAmount = 0;
        _building.OnProduced += (id, itemId, amount) =>
        {
            producedItem = itemId;
            producedAmount = amount;
        };
        _building.Tick(10f);
        Assert.AreEqual("raw_food", producedItem);
        Assert.AreEqual(1, producedAmount);
    }

    [Test]
    public void DoesNotProduceBeforeIntervalElapsed()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        bool produced = false;
        _building.OnProduced += (id, itemId, amount) => produced = true;
        _building.Tick(5f);
        Assert.IsFalse(produced);
    }

    [Test]
    public void MultipleProductionCyclesInOneTick()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        int count = 0;
        _building.OnProduced += (id, itemId, amount) => count++;
        _building.Tick(25f); // 2 full intervals (10s each), remainder 5s
        Assert.AreEqual(2, count);
    }

    // --- Workers ---

    [Test]
    public void AssignWorkerSucceeds()
    {
        Assert.IsTrue(_building.AssignWorker("npc_01"));
        Assert.AreEqual(1, _building.WorkerCount);
    }

    [Test]
    public void CannotAssignMoreThanMaxSlots()
    {
        _building.AssignWorker("npc_01");
        _building.AssignWorker("npc_02");
        _building.AssignWorker("npc_03");
        Assert.IsFalse(_building.AssignWorker("npc_04"));
        Assert.AreEqual(3, _building.WorkerCount);
    }

    [Test]
    public void CannotAssignSameWorkerTwice()
    {
        _building.AssignWorker("npc_01");
        Assert.IsFalse(_building.AssignWorker("npc_01"));
        Assert.AreEqual(1, _building.WorkerCount);
    }

    [Test]
    public void UnassignWorkerSucceeds()
    {
        _building.AssignWorker("npc_01");
        Assert.IsTrue(_building.UnassignWorker("npc_01"));
        Assert.AreEqual(0, _building.WorkerCount);
    }

    [Test]
    public void WorkersBoostProductionRate()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        _building.AssignWorker("npc_01");
        _building.AssignWorker("npc_02");
        // base rate * (1 + 0.25 * 2) = 1.5x
        float expected = 1.5f;
        Assert.AreEqual(expected, _building.EffectiveProductionMultiplier, 0.001f);
    }

    [Test]
    public void WorkerBonusAffectsProductionTiming()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        _building.AssignWorker("npc_01");
        _building.AssignWorker("npc_02");
        // Effective interval = 10f / 1.5f = 6.667s
        int count = 0;
        _building.OnProduced += (id, itemId, amount) => count++;
        _building.Tick(7f);
        Assert.AreEqual(1, count); // should produce once at ~6.67s
    }

    // --- Upgrades ---

    [Test]
    public void StartsAtUpgradeTierZero()
    {
        Assert.AreEqual(0, _building.UpgradeTier);
    }

    [Test]
    public void CannotUpgradeWhenUnclaimed()
    {
        Assert.IsFalse(_building.AdvanceUpgrade());
    }

    [Test]
    public void UpgradeAdvancesTier()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        Assert.IsTrue(_building.AdvanceUpgrade());
        Assert.AreEqual(1, _building.UpgradeTier);
    }

    [Test]
    public void CannotUpgradePastMaxTier()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        _building.AdvanceUpgrade();
        Assert.IsFalse(_building.AdvanceUpgrade());
        Assert.AreEqual(1, _building.UpgradeTier);
    }

    [Test]
    public void UpgradeOverridesProduction()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        _building.AdvanceUpgrade();
        string producedItem = null;
        _building.OnProduced += (id, itemId, amount) => producedItem = itemId;
        _building.Tick(20f);
        Assert.AreEqual("medicine", producedItem);
    }

    [Test]
    public void UpgradeIncreasesTerritory()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        float baseTerr = _building.EffectiveTerritoryRadius;
        _building.AdvanceUpgrade();
        Assert.AreEqual(baseTerr + 5f, _building.EffectiveTerritoryRadius, 0.001f);
    }

    [Test]
    public void UpgradeIncreasesWorkerSlots()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        _building.AdvanceUpgrade();
        // base 3 + bonus 1 = 4
        _building.AssignWorker("a");
        _building.AssignWorker("b");
        _building.AssignWorker("c");
        Assert.IsTrue(_building.AssignWorker("d"));
        Assert.AreEqual(4, _building.WorkerCount);
    }

    [Test]
    public void UpgradeFiresEvent()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        int firedTier = -1;
        _building.OnUpgraded += (id, tier) => firedTier = tier;
        _building.AdvanceUpgrade();
        Assert.AreEqual(1, firedTier);
    }

    // --- GetUpgradeRequirements ---

    [Test]
    public void GetUpgradeRequirementsReturnsCurrentTier()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        var reqs = _building.GetUpgradeRequirements();
        Assert.AreEqual(1, reqs.Length);
        Assert.AreEqual("glass", reqs[0].itemId);
        Assert.AreEqual(10, reqs[0].amount);
    }

    [Test]
    public void GetUpgradeRequirementsReturnsEmptyAtMaxTier()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        _building.AdvanceUpgrade();
        var reqs = _building.GetUpgradeRequirements();
        Assert.AreEqual(0, reqs.Length);
    }

    // --- Territory ---

    [Test]
    public void TerritoryRadiusMatchesDefinitionWhenClaimed()
    {
        for (int i = 0; i < 3; i++) _building.AdvanceRepair();
        Assert.AreEqual(20f, _building.EffectiveTerritoryRadius, 0.001f);
    }

    [Test]
    public void TerritoryRadiusIsZeroWhenUnclaimed()
    {
        Assert.AreEqual(0f, _building.EffectiveTerritoryRadius, 0.001f);
    }
}
```

**Step 2: Run tests to verify they fail**

Run via Unity Test Runner (EditMode) or MCP `run_tests`. Expected: all fail with "SettlementBuilding not found".

**Step 3: Write SettlementBuilding.cs**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public class SettlementBuilding
{
    private readonly string _buildingId;
    private readonly SettlementBuildingDefinitionSO _definition;
    private readonly Vector3 _position;
    private readonly List<string> _assignedWorkerIds = new();

    private int _repairLevel;
    private int _upgradeTier;
    private float _productionTimer;

    public string BuildingId => _buildingId;
    public SettlementBuildingDefinitionSO Definition => _definition;
    public Vector3 Position => _position;
    public int RepairLevel => _repairLevel;
    public int UpgradeTier => _upgradeTier;
    public bool IsClaimed => _repairLevel >= _definition.MaxRepairLevel;
    public int WorkerCount => _assignedWorkerIds.Count;
    public IReadOnlyList<string> AssignedWorkerIds => _assignedWorkerIds;

    public event Action<string, int> OnRepaired;      // buildingId, newLevel
    public event Action<string> OnClaimed;             // buildingId
    public event Action<string, int> OnUpgraded;       // buildingId, newTier
    public event Action<string, string, int> OnProduced; // buildingId, itemId, amount
    public event Action<string, string> OnWorkerAssigned;   // buildingId, workerId
    public event Action<string, string> OnWorkerUnassigned; // buildingId, workerId

    public SettlementBuilding(string buildingId, SettlementBuildingDefinitionSO definition, Vector3 position)
    {
        _buildingId = buildingId;
        _definition = definition;
        _position = position;
    }

    public int MaxWorkerSlots
    {
        get
        {
            int slots = _definition.workerSlots;
            for (int i = 0; i < _upgradeTier && i < _definition.upgradeTiers.Length; i++)
                slots += _definition.upgradeTiers[i].workerSlotsBonus;
            return slots;
        }
    }

    public float EffectiveProductionMultiplier =>
        1f + _definition.workerBonusPerSlot * _assignedWorkerIds.Count;

    public float EffectiveTerritoryRadius
    {
        get
        {
            if (!IsClaimed) return 0f;
            float radius = _definition.territoryRadius;
            for (int i = 0; i < _upgradeTier && i < _definition.upgradeTiers.Length; i++)
                radius += _definition.upgradeTiers[i].territoryBonus;
            return radius;
        }
    }

    public void AdvanceRepair()
    {
        if (_repairLevel >= _definition.MaxRepairLevel) return;
        _repairLevel++;
        OnRepaired?.Invoke(_buildingId, _repairLevel);
        if (IsClaimed) OnClaimed?.Invoke(_buildingId);
    }

    public bool AdvanceUpgrade()
    {
        if (!IsClaimed) return false;
        if (_definition.upgradeTiers == null || _upgradeTier >= _definition.upgradeTiers.Length)
            return false;
        _upgradeTier++;
        _productionTimer = 0f;
        OnUpgraded?.Invoke(_buildingId, _upgradeTier);
        return true;
    }

    public (string itemId, int amount)[] GetRepairRequirements()
    {
        if (_repairLevel >= _definition.MaxRepairLevel)
            return Array.Empty<(string, int)>();
        var stage = _definition.repairStages[_repairLevel];
        var reqs = new (string, int)[stage.requiredItemIds.Length];
        for (int i = 0; i < reqs.Length; i++)
        {
            int amount = i < stage.requiredAmounts.Length ? stage.requiredAmounts[i] : 1;
            reqs[i] = (stage.requiredItemIds[i], amount);
        }
        return reqs;
    }

    public (string itemId, int amount)[] GetUpgradeRequirements()
    {
        if (!IsClaimed || _definition.upgradeTiers == null ||
            _upgradeTier >= _definition.upgradeTiers.Length)
            return Array.Empty<(string, int)>();
        var tier = _definition.upgradeTiers[_upgradeTier];
        var reqs = new (string, int)[tier.requiredItemIds.Length];
        for (int i = 0; i < reqs.Length; i++)
        {
            int amount = i < tier.requiredAmounts.Length ? tier.requiredAmounts[i] : 1;
            reqs[i] = (tier.requiredItemIds[i], amount);
        }
        return reqs;
    }

    public bool AssignWorker(string workerId)
    {
        if (_assignedWorkerIds.Count >= MaxWorkerSlots) return false;
        if (_assignedWorkerIds.Contains(workerId)) return false;
        _assignedWorkerIds.Add(workerId);
        OnWorkerAssigned?.Invoke(_buildingId, workerId);
        return true;
    }

    public bool UnassignWorker(string workerId)
    {
        if (!_assignedWorkerIds.Remove(workerId)) return false;
        OnWorkerUnassigned?.Invoke(_buildingId, workerId);
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (!IsClaimed) return;

        var prod = GetActiveProduction();
        if (prod == null || prod.productionInterval <= 0f) return;

        float effectiveInterval = prod.productionInterval / EffectiveProductionMultiplier;
        _productionTimer += deltaTime;

        while (_productionTimer >= effectiveInterval)
        {
            _productionTimer -= effectiveInterval;
            OnProduced?.Invoke(_buildingId, prod.producedItemId, prod.producedAmount);
        }
    }

    /// <summary>
    /// Sets repair level directly. Used for sync from network state.
    /// Does not fire events — caller is responsible for visual updates.
    /// </summary>
    public void SetRepairLevel(int level)
    {
        _repairLevel = Mathf.Clamp(level, 0, _definition.MaxRepairLevel);
    }

    /// <summary>
    /// Sets upgrade tier directly. Used for sync from network state.
    /// </summary>
    public void SetUpgradeTier(int tier)
    {
        int max = _definition.upgradeTiers != null ? _definition.upgradeTiers.Length : 0;
        _upgradeTier = Mathf.Clamp(tier, 0, max);
    }

    private ProductionDefinition GetActiveProduction()
    {
        // Upgraded production overrides base
        for (int i = _upgradeTier - 1; i >= 0; i--)
        {
            if (_definition.upgradeTiers[i].productionOverride != null)
                return _definition.upgradeTiers[i].productionOverride;
        }
        return _definition.production;
    }
}
```

**Step 4: Run tests to verify they pass**

Expected: all 27 tests pass.

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Settlement/SettlementBuilding.cs Assets/_Slopworks/Tests/Editor/EditMode/SettlementBuildingTests.cs
git commit -m "add SettlementBuilding simulation class with 27 tests"
```

---

## Task 4: SettlementGraph simulation class + tests (TDD)

**Files:**
- Create: `Assets/_Slopworks/Scripts/Settlement/SettlementGraph.cs`
- Create: `Assets/_Slopworks/Tests/Editor/EditMode/SettlementGraphTests.cs`

**Step 1: Write the failing tests first**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SettlementGraphTests
{
    private SettlementBuildingDefinitionSO _farmDef;
    private SettlementBuildingDefinitionSO _workshopDef;
    private SettlementBuildingDefinitionSO _factoryDef;
    private SettlementGraph _graph;

    [SetUp]
    public void SetUp()
    {
        _farmDef = ScriptableObject.CreateInstance<SettlementBuildingDefinitionSO>();
        _farmDef.buildingId = "farm";
        _farmDef.connectionRange = 100f;
        _farmDef.territoryRadius = 20f;
        _farmDef.repairStages = new RepairStageDefinition[]
        {
            new() { requiredItemIds = new[] { "metal" }, requiredAmounts = new[] { 1 } }
        };
        _farmDef.production = new ProductionDefinition
        {
            producedItemId = "raw_food",
            producedAmount = 1,
            productionInterval = 10f
        };

        _workshopDef = ScriptableObject.CreateInstance<SettlementBuildingDefinitionSO>();
        _workshopDef.buildingId = "workshop";
        _workshopDef.connectionRange = 100f;
        _workshopDef.territoryRadius = 25f;
        _workshopDef.repairStages = new RepairStageDefinition[]
        {
            new() { requiredItemIds = new[] { "metal" }, requiredAmounts = new[] { 1 } }
        };

        _factoryDef = ScriptableObject.CreateInstance<SettlementBuildingDefinitionSO>();
        _factoryDef.buildingId = "factory_yard";
        _factoryDef.connectionRange = 150f;
        _factoryDef.territoryRadius = 30f;
        _factoryDef.repairStages = new RepairStageDefinition[0]; // pre-claimed

        _graph = new SettlementGraph("factory_yard");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_farmDef);
        Object.DestroyImmediate(_workshopDef);
        Object.DestroyImmediate(_factoryDef);
    }

    // --- Registration ---

    [Test]
    public void RegisterBuildingAddsToGraph()
    {
        _graph.Register(_farmDef, new Vector3(50, 0, 300));
        Assert.IsNotNull(_graph.Get("farm"));
    }

    [Test]
    public void GetReturnsNullForUnknownId()
    {
        Assert.IsNull(_graph.Get("nonexistent"));
    }

    [Test]
    public void CannotRegisterDuplicateId()
    {
        _graph.Register(_farmDef, Vector3.zero);
        Assert.IsFalse(_graph.Register(_farmDef, Vector3.one));
    }

    [Test]
    public void AllBuildingsReturnsAllRegistered()
    {
        _graph.Register(_farmDef, Vector3.zero);
        _graph.Register(_workshopDef, Vector3.one);
        Assert.AreEqual(2, _graph.AllBuildings.Count);
    }

    // --- Roads ---

    [Test]
    public void BuildRoadConnectsTwoBuildings()
    {
        _graph.Register(_farmDef, Vector3.zero);
        _graph.Register(_workshopDef, new Vector3(50, 0, 0));
        Assert.IsTrue(_graph.BuildRoad("farm", "workshop"));
    }

    [Test]
    public void CannotBuildRoadToSelf()
    {
        _graph.Register(_farmDef, Vector3.zero);
        Assert.IsFalse(_graph.BuildRoad("farm", "farm"));
    }

    [Test]
    public void CannotBuildRoadBeyondRange()
    {
        _graph.Register(_farmDef, Vector3.zero);
        _graph.Register(_workshopDef, new Vector3(500, 0, 0)); // beyond 100m range
        Assert.IsFalse(_graph.BuildRoad("farm", "workshop"));
    }

    [Test]
    public void CannotBuildDuplicateRoad()
    {
        _graph.Register(_farmDef, Vector3.zero);
        _graph.Register(_workshopDef, new Vector3(50, 0, 0));
        _graph.BuildRoad("farm", "workshop");
        Assert.IsFalse(_graph.BuildRoad("farm", "workshop"));
    }

    [Test]
    public void RoadIsBidirectional()
    {
        _graph.Register(_farmDef, Vector3.zero);
        _graph.Register(_workshopDef, new Vector3(50, 0, 0));
        _graph.BuildRoad("farm", "workshop");
        Assert.IsTrue(_graph.AreConnected("farm", "workshop"));
        Assert.IsTrue(_graph.AreConnected("workshop", "farm"));
    }

    // --- Connectivity ---

    [Test]
    public void AreConnectedReturnsFalseWithoutRoad()
    {
        _graph.Register(_farmDef, Vector3.zero);
        _graph.Register(_workshopDef, new Vector3(50, 0, 0));
        Assert.IsFalse(_graph.AreConnected("farm", "workshop"));
    }

    [Test]
    public void TransitiveConnectionWorks()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));
        _graph.Register(_workshopDef, new Vector3(90, 0, 0));

        _graph.BuildRoad("factory_yard", "farm");
        _graph.BuildRoad("farm", "workshop");
        Assert.IsTrue(_graph.AreConnected("factory_yard", "workshop"));
    }

    [Test]
    public void HasFactoryConnectionReturnsTrueWhenDirectlyConnected()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));
        _graph.BuildRoad("factory_yard", "farm");
        Assert.IsTrue(_graph.HasFactoryConnection("farm"));
    }

    [Test]
    public void HasFactoryConnectionReturnsTrueTransitively()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));
        _graph.Register(_workshopDef, new Vector3(90, 0, 0));
        _graph.BuildRoad("factory_yard", "farm");
        _graph.BuildRoad("farm", "workshop");
        Assert.IsTrue(_graph.HasFactoryConnection("workshop"));
    }

    [Test]
    public void HasFactoryConnectionReturnsFalseWhenDisconnected()
    {
        _graph.Register(_farmDef, new Vector3(50, 0, 0));
        Assert.IsFalse(_graph.HasFactoryConnection("farm"));
    }

    [Test]
    public void FactoryYardAlwaysHasFactoryConnection()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        Assert.IsTrue(_graph.HasFactoryConnection("factory_yard"));
    }

    // --- Production tick ---

    [Test]
    public void TickProducesForClaimedBuildings()
    {
        _graph.Register(_farmDef, Vector3.zero);
        var farm = _graph.Get("farm");
        farm.AdvanceRepair(); // 1 stage = claimed
        string produced = null;
        farm.OnProduced += (id, itemId, amount) => produced = itemId;
        _graph.Tick(10f);
        Assert.AreEqual("raw_food", produced);
    }

    [Test]
    public void TickSkipsUnclaimedBuildings()
    {
        _graph.Register(_farmDef, Vector3.zero);
        var farm = _graph.Get("farm");
        bool produced = false;
        farm.OnProduced += (id, itemId, amount) => produced = true;
        _graph.Tick(10f);
        Assert.IsFalse(produced);
    }

    // --- Roads list ---

    [Test]
    public void RoadsListReturnsAllRoads()
    {
        _graph.Register(_factoryDef, Vector3.zero);
        _graph.Register(_farmDef, new Vector3(50, 0, 0));
        _graph.Register(_workshopDef, new Vector3(90, 0, 0));
        _graph.BuildRoad("factory_yard", "farm");
        _graph.BuildRoad("farm", "workshop");
        Assert.AreEqual(2, _graph.Roads.Count);
    }

    // --- Territory ---

    [Test]
    public void IsInTerritoryReturnsTrueInsideRadius()
    {
        _graph.Register(_farmDef, Vector3.zero);
        var farm = _graph.Get("farm");
        farm.AdvanceRepair();
        Assert.IsTrue(_graph.IsInTerritory(new Vector3(15, 0, 0)));
    }

    [Test]
    public void IsInTerritoryReturnsFalseOutsideRadius()
    {
        _graph.Register(_farmDef, Vector3.zero);
        var farm = _graph.Get("farm");
        farm.AdvanceRepair();
        Assert.IsFalse(_graph.IsInTerritory(new Vector3(50, 0, 0)));
    }

    [Test]
    public void IsInTerritoryReturnsFalseForUnclaimedBuilding()
    {
        _graph.Register(_farmDef, Vector3.zero);
        Assert.IsFalse(_graph.IsInTerritory(new Vector3(5, 0, 0)));
    }
}
```

**Step 2: Run tests to verify they fail**

Expected: all fail with "SettlementGraph not found".

**Step 3: Write SettlementGraph.cs**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public class SettlementGraph
{
    private readonly string _factoryHubId;
    private readonly Dictionary<string, SettlementBuilding> _buildings = new();
    private readonly List<(string a, string b)> _roads = new();
    private readonly Dictionary<string, HashSet<string>> _adjacency = new();

    public IReadOnlyDictionary<string, SettlementBuilding> AllBuildings => _buildings;
    public IReadOnlyList<(string a, string b)> Roads => _roads;

    public SettlementGraph(string factoryHubId)
    {
        _factoryHubId = factoryHubId;
    }

    public bool Register(SettlementBuildingDefinitionSO definition, Vector3 position)
    {
        if (_buildings.ContainsKey(definition.buildingId)) return false;
        var building = new SettlementBuilding(definition.buildingId, definition, position);
        _buildings[definition.buildingId] = building;
        _adjacency[definition.buildingId] = new HashSet<string>();
        return true;
    }

    public SettlementBuilding Get(string buildingId)
    {
        _buildings.TryGetValue(buildingId, out var building);
        return building;
    }

    public bool BuildRoad(string idA, string idB)
    {
        if (idA == idB) return false;
        if (!_buildings.TryGetValue(idA, out var a)) return false;
        if (!_buildings.TryGetValue(idB, out var b)) return false;

        // Check range (use smaller of the two connection ranges)
        float maxRange = Mathf.Min(a.Definition.connectionRange, b.Definition.connectionRange);
        float distance = Vector3.Distance(a.Position, b.Position);
        if (distance > maxRange) return false;

        // Check for duplicate
        if (_adjacency[idA].Contains(idB)) return false;

        _roads.Add((idA, idB));
        _adjacency[idA].Add(idB);
        _adjacency[idB].Add(idA);
        return true;
    }

    public bool AreConnected(string idA, string idB)
    {
        if (idA == idB) return true;
        if (!_adjacency.ContainsKey(idA) || !_adjacency.ContainsKey(idB)) return false;

        // BFS
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(idA);
        visited.Add(idA);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == idB) return true;
            foreach (var neighbor in _adjacency[current])
            {
                if (visited.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }
        return false;
    }

    public bool HasFactoryConnection(string buildingId)
    {
        if (buildingId == _factoryHubId) return _buildings.ContainsKey(_factoryHubId);
        return AreConnected(buildingId, _factoryHubId);
    }

    public void Tick(float deltaTime)
    {
        foreach (var building in _buildings.Values)
            building.Tick(deltaTime);
    }

    public bool IsInTerritory(Vector3 worldPosition)
    {
        foreach (var building in _buildings.Values)
        {
            if (!building.IsClaimed) continue;
            float radius = building.EffectiveTerritoryRadius;
            if (radius <= 0f) continue;
            float dist = Vector3.Distance(worldPosition, building.Position);
            if (dist <= radius) return true;
        }
        return false;
    }
}
```

**Step 4: Run tests to verify they pass**

Expected: all 21 tests pass.

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Settlement/SettlementGraph.cs Assets/_Slopworks/Tests/Editor/EditMode/SettlementGraphTests.cs
git commit -m "add SettlementGraph simulation with connectivity, territory, production tick — 21 tests"
```

---

## Task 5: SettlementBuildingBehaviour (MonoBehaviour wrapper + IInteractable)

**Files:**
- Create: `Assets/_Slopworks/Scripts/Settlement/SettlementBuildingBehaviour.cs`

This is the thin wrapper placed on each building GameObject in the scene. Handles visual piece spawning and E-key interaction.

**Step 1: Write SettlementBuildingBehaviour.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;

public class SettlementBuildingBehaviour : MonoBehaviour, IInteractable
{
    [SerializeField] private SettlementBuildingDefinitionSO _definition;

    private SettlementBuilding _simulation;
    private readonly List<GameObject> _spawnedPieces = new();
    private int _lastVisualLevel;

    public SettlementBuilding Simulation => _simulation;
    public SettlementBuildingDefinitionSO Definition => _definition;

    public void Initialize(SettlementBuilding simulation)
    {
        _simulation = simulation;
        _simulation.OnRepaired += OnRepaired;
        _simulation.OnUpgraded += OnUpgraded;
        RefreshVisuals();
    }

    public string GetInteractionPrompt()
    {
        if (_simulation == null) return "";
        if (_simulation.IsClaimed)
            return $"press E to manage {_definition.displayName}";
        return $"press E to inspect {_definition.displayName} (repair {_simulation.RepairLevel}/{_definition.MaxRepairLevel})";
    }

    public void Interact(GameObject player)
    {
        if (_simulation == null) return;

        // Find or create the settlement UI and open it for this building
        var ui = FindAnyObjectByType<SettlementInspectUI>();
        if (ui != null)
            ui.Open(_simulation);
        else
            Debug.Log($"settlement: interacted with {_definition.displayName}, no UI found");
    }

    public void RefreshVisuals()
    {
        if (_simulation == null || _definition == null) return;

        // Spawn additive pieces for each completed repair stage
        for (int i = _lastVisualLevel; i < _simulation.RepairLevel; i++)
        {
            if (i >= _definition.repairStages.Length) break;
            var stage = _definition.repairStages[i];
            if (stage.addedPiecePrefabs == null) continue;
            foreach (var prefab in stage.addedPiecePrefabs)
            {
                if (prefab == null) continue;
                var piece = Instantiate(prefab, transform);
                _spawnedPieces.Add(piece);
            }
        }

        // Spawn additive pieces for each completed upgrade tier
        int baseUpgradeVisual = Mathf.Max(0, _lastVisualLevel - _definition.MaxRepairLevel);
        for (int i = baseUpgradeVisual; i < _simulation.UpgradeTier; i++)
        {
            if (i >= _definition.upgradeTiers.Length) break;
            var tier = _definition.upgradeTiers[i];
            if (tier.addedPiecePrefabs == null) continue;
            foreach (var prefab in tier.addedPiecePrefabs)
            {
                if (prefab == null) continue;
                var piece = Instantiate(prefab, transform);
                _spawnedPieces.Add(piece);
            }
        }

        _lastVisualLevel = _simulation.RepairLevel + _simulation.UpgradeTier;
    }

    private void OnRepaired(string buildingId, int newLevel)
    {
        Debug.Log($"settlement: {_definition.displayName} repaired to level {newLevel}");
        RefreshVisuals();
    }

    private void OnUpgraded(string buildingId, int newTier)
    {
        Debug.Log($"settlement: {_definition.displayName} upgraded to tier {newTier}");
        RefreshVisuals();
    }

    private void OnDestroy()
    {
        if (_simulation != null)
        {
            _simulation.OnRepaired -= OnRepaired;
            _simulation.OnUpgraded -= OnUpgraded;
        }
        foreach (var piece in _spawnedPieces)
        {
            if (piece != null) Destroy(piece);
        }
    }
}
```

**Step 2: Verify compilation**

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Settlement/SettlementBuildingBehaviour.cs
git commit -m "add SettlementBuildingBehaviour wrapper with IInteractable and additive visuals"
```

---

## Task 6: SettlementManagerBehaviour (singleton, graph owner, tick)

**Files:**
- Create: `Assets/_Slopworks/Scripts/Settlement/SettlementManagerBehaviour.cs`

This manages the graph, registers buildings, and runs the production tick. Designed for future FishNet sync but starts as a plain MonoBehaviour.

**Step 1: Write SettlementManagerBehaviour.cs**

```csharp
using UnityEngine;

public class SettlementManagerBehaviour : MonoBehaviour
{
    public static SettlementManagerBehaviour Instance { get; private set; }

    [SerializeField] private string _factoryHubId = "factory_yard";
    [SerializeField] private float _tickInterval = 1f;

    private SettlementGraph _graph;
    private float _tickTimer;

    public SettlementGraph Graph => _graph;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _graph = new SettlementGraph(_factoryHubId);
    }

    public SettlementBuilding RegisterBuilding(
        SettlementBuildingDefinitionSO definition, Vector3 position)
    {
        if (!_graph.Register(definition, position))
        {
            Debug.LogWarning($"settlement: failed to register {definition.buildingId}");
            return null;
        }
        var building = _graph.Get(definition.buildingId);
        Debug.Log($"settlement: registered {definition.displayName} at {position}");
        return building;
    }

    public bool BuildRoad(string idA, string idB)
    {
        bool result = _graph.BuildRoad(idA, idB);
        if (result)
            Debug.Log($"settlement: road built between {idA} and {idB}");
        else
            Debug.LogWarning($"settlement: failed to build road {idA} <-> {idB}");
        return result;
    }

    private void FixedUpdate()
    {
        if (_graph == null) return;
        _tickTimer += Time.fixedDeltaTime;
        if (_tickTimer >= _tickInterval)
        {
            _graph.Tick(_tickTimer);
            _tickTimer = 0f;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
```

**Step 2: Verify compilation**

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Settlement/SettlementManagerBehaviour.cs
git commit -m "add SettlementManagerBehaviour singleton with graph ownership and production tick"
```

---

## Task 7: SettlementInspectUI (repair/manage panel)

**Files:**
- Create: `Assets/_Slopworks/Scripts/UI/SettlementInspectUI.cs`

Runtime-created UI panel following the existing pattern (TowerElevatorUI creates all UI at runtime, no prefabs).

**Step 1: Write SettlementInspectUI.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettlementInspectUI : MonoBehaviour
{
    private SettlementBuilding _building;
    private GameObject _panel;
    private Text _titleText;
    private Text _statusText;
    private Text _requirementsText;
    private Button _actionButton;
    private Text _actionButtonText;
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        CreateUI();
        _panel.SetActive(false);
    }

    public void Open(SettlementBuilding building)
    {
        _building = building;
        _isOpen = true;
        _panel.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        _building = null;
        _isOpen = false;
        _panel.SetActive(false);
    }

    private void Update()
    {
        if (!_isOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            Close();
    }

    public void Refresh()
    {
        if (_building == null) return;

        _titleText.text = _building.Definition.displayName;

        if (!_building.IsClaimed)
        {
            _statusText.text = $"Repair progress: {_building.RepairLevel} / {_building.Definition.MaxRepairLevel}";
            var reqs = _building.GetRepairRequirements();
            _requirementsText.text = FormatRequirements("Materials needed:", reqs);
            _actionButtonText.text = "Deliver materials";
            _actionButton.onClick.RemoveAllListeners();
            _actionButton.onClick.AddListener(OnDeliverRepairMaterials);
        }
        else if (_building.UpgradeTier < (_building.Definition.upgradeTiers?.Length ?? 0))
        {
            _statusText.text = $"Claimed | Tier {_building.UpgradeTier} | Workers: {_building.WorkerCount}/{_building.MaxWorkerSlots}";
            var reqs = _building.GetUpgradeRequirements();
            _requirementsText.text = FormatRequirements("Upgrade materials:", reqs);
            _actionButtonText.text = "Upgrade";
            _actionButton.onClick.RemoveAllListeners();
            _actionButton.onClick.AddListener(OnDeliverUpgradeMaterials);
        }
        else
        {
            _statusText.text = $"Fully upgraded | Workers: {_building.WorkerCount}/{_building.MaxWorkerSlots}";
            _requirementsText.text = "All upgrades complete.";
            _actionButton.gameObject.SetActive(false);
        }
    }

    private string FormatRequirements(string header, (string itemId, int amount)[] reqs)
    {
        if (reqs.Length == 0) return "";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(header);
        foreach (var (itemId, amount) in reqs)
            sb.AppendLine($"  {itemId}: {amount}");
        return sb.ToString();
    }

    private void OnDeliverRepairMaterials()
    {
        // TODO: check player inventory, deduct materials, advance repair
        // For now, just advance directly for testing
        _building.AdvanceRepair();
        Refresh();
        Debug.Log($"settlement ui: delivered repair materials to {_building.BuildingId}");
    }

    private void OnDeliverUpgradeMaterials()
    {
        // TODO: check player inventory, deduct materials, advance upgrade
        _building.AdvanceUpgrade();
        Refresh();
        Debug.Log($"settlement ui: delivered upgrade materials to {_building.BuildingId}");
    }

    private void CreateUI()
    {
        // Canvas
        var canvasGo = new GameObject("SettlementInspectCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // Panel background
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(canvasGo.transform, false);
        var panelRect = _panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.3f, 0.2f);
        panelRect.anchorMax = new Vector2(0.7f, 0.8f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImage = _panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);

        // Title
        _titleText = CreateText(_panel.transform, "Title", 24, TextAnchor.UpperCenter,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, -20), new Vector2(-20, -60));

        // Status
        _statusText = CreateText(_panel.transform, "Status", 16, TextAnchor.UpperLeft,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, -70), new Vector2(-20, -100));

        // Requirements
        _requirementsText = CreateText(_panel.transform, "Requirements", 14, TextAnchor.UpperLeft,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, -110), new Vector2(-20, -250));

        // Action button
        var btnGo = new GameObject("ActionButton");
        btnGo.transform.SetParent(_panel.transform, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.3f, 0f);
        btnRect.anchorMax = new Vector2(0.7f, 0f);
        btnRect.offsetMin = new Vector2(0, 20);
        btnRect.offsetMax = new Vector2(0, 60);
        var btnImage = btnGo.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.5f, 0.3f, 1f);
        _actionButton = btnGo.AddComponent<Button>();

        _actionButtonText = CreateText(btnGo.transform, "ButtonText", 16, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }
}
```

**Step 2: Verify compilation**

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/UI/SettlementInspectUI.cs
git commit -m "add SettlementInspectUI runtime panel for repair/manage interactions"
```

---

## Task 8: SettlementRoadBehaviour (visual road between buildings)

**Files:**
- Create: `Assets/_Slopworks/Scripts/Settlement/SettlementRoadBehaviour.cs`

**Step 1: Write SettlementRoadBehaviour.cs**

```csharp
using UnityEngine;

public class SettlementRoadBehaviour : MonoBehaviour
{
    private LineRenderer _lineRenderer;
    private string _buildingIdA;
    private string _buildingIdB;

    public string BuildingIdA => _buildingIdA;
    public string BuildingIdB => _buildingIdB;

    public void Initialize(string idA, string idB, Vector3 posA, Vector3 posB)
    {
        _buildingIdA = idA;
        _buildingIdB = idB;

        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.positionCount = 2;
        _lineRenderer.SetPosition(0, posA + Vector3.up * 0.5f);
        _lineRenderer.SetPosition(1, posB + Vector3.up * 0.5f);
        _lineRenderer.startWidth = 2f;
        _lineRenderer.endWidth = 2f;
        _lineRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _lineRenderer.material.color = new Color(0.6f, 0.5f, 0.35f, 1f); // dirt path color
        _lineRenderer.useWorldSpace = true;

        Debug.Log($"settlement: road visual created {idA} <-> {idB}");
    }
}
```

**Step 2: Verify compilation**

**Step 3: Commit**

```bash
git add Assets/_Slopworks/Scripts/Settlement/SettlementRoadBehaviour.cs
git commit -m "add SettlementRoadBehaviour for visual road connections"
```

---

## Task 9: Settlement playtest bootstrapper

**Files:**
- Create: `Assets/_Slopworks/Scripts/Settlement/SettlementPlaytestSetup.cs`
- Create: `Assets/_Slopworks/Scenes/JoePlaytest/SettlementPlaytest.unity` (via editor)

This creates a self-contained playtest scene per the phase completion standard. All buildings created from runtime SOs, no asset dependencies.

**Step 1: Write SettlementPlaytestSetup.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bootstrapper for settlement system playtest. Creates terrain, buildings,
/// and UI entirely at runtime. No prefab or asset dependencies.
///
/// Controls:
/// E       — inspect/manage building (when looking at one)
/// R       — repair current building (skip material check, for testing)
/// U       — upgrade current building (skip material check, for testing)
/// W       — assign a test worker to current building
/// B       — build road between last two selected buildings
/// T       — toggle territory debug spheres
/// M       — log full settlement state to console
/// </summary>
public class SettlementPlaytestSetup : MonoBehaviour
{
    [SerializeField] private bool _spawnExplorer = true;

    private SettlementManagerBehaviour _manager;
    private SettlementInspectUI _inspectUI;
    private readonly List<ScriptableObject> _runtimeSOs = new();
    private readonly List<GameObject> _debugTerritoryVisuals = new();
    private bool _territoryVisible;
    private string _lastSelectedBuildingId;
    private string _secondLastSelectedBuildingId;

    private void Start()
    {
        CreateGround();
        SetupManager();
        CreateBuildings();
        SetupUI();
        SetupLighting();

        if (_spawnExplorer)
            SpawnExplorer();

        Debug.Log("settlement playtest: ready. E=inspect, R=repair, U=upgrade, W=worker, B=road, T=territory, M=status");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
            ToggleTerritoryDebug();

        if (Input.GetKeyDown(KeyCode.M))
            LogSettlementState();
    }

    private void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(60, 1, 60); // 600x600
        ground.layer = 12; // Terrain
        var rend = ground.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        rend.material.color = new Color(0.35f, 0.3f, 0.25f);
    }

    private void SetupManager()
    {
        var managerGo = new GameObject("SettlementManager");
        _manager = managerGo.AddComponent<SettlementManagerBehaviour>();
    }

    private void CreateBuildings()
    {
        // Factory yard (hub, pre-claimed with 0 repair stages)
        CreateBuilding("factory_yard", "Factory yard", SettlementBuildingType.Workshop,
            Vector3.zero, 30f, 150f, 3, 0);

        // Farmstead
        CreateBuilding("farmstead", "Farmstead", SettlementBuildingType.Farmstead,
            new Vector3(50, 0, 100), 20f, 100f, 3, 3,
            new ProductionDefinition { producedItemId = "raw_food", producedAmount = 1, productionInterval = 10f });

        // Watchtower
        CreateBuilding("watchtower", "Watchtower", SettlementBuildingType.Watchtower,
            new Vector3(0, 0, -80), 60f, 100f, 1, 2);

        // Workshop
        CreateBuilding("workshop", "Workshop", SettlementBuildingType.Workshop,
            new Vector3(100, 0, 30), 25f, 100f, 2, 3,
            new ProductionDefinition { producedItemId = "repair_kit", producedAmount = 1, productionInterval = 15f });

        // Market
        CreateBuilding("market", "Market", SettlementBuildingType.Market,
            new Vector3(-100, 0, 0), 15f, 100f, 2, 2,
            new ProductionDefinition { producedItemId = "trade_token", producedAmount = 1, productionInterval = 20f });

        // Barracks
        CreateBuilding("barracks", "Barracks", SettlementBuildingType.Barracks,
            new Vector3(80, 0, 90), 40f, 100f, 4, 2);
    }

    private void CreateBuilding(string id, string displayName, SettlementBuildingType type,
        Vector3 position, float territoryRadius, float connectionRange,
        int workerSlots, int repairStageCount, ProductionDefinition production = null)
    {
        var def = ScriptableObject.CreateInstance<SettlementBuildingDefinitionSO>();
        def.buildingId = id;
        def.displayName = displayName;
        def.buildingType = type;
        def.territoryRadius = territoryRadius;
        def.connectionRange = connectionRange;
        def.workerSlots = workerSlots;
        def.workerBonusPerSlot = 0.25f;
        def.production = production;
        def.upgradeTiers = new UpgradeTierDefinition[0];

        // Create repair stages with placeholder requirements
        var stages = new RepairStageDefinition[repairStageCount];
        for (int i = 0; i < repairStageCount; i++)
        {
            stages[i] = new RepairStageDefinition
            {
                requiredItemIds = new[] { "metal_plate" },
                requiredAmounts = new[] { 3 + i * 2 }
            };
        }
        def.repairStages = stages;
        _runtimeSOs.Add(def);

        // Register in graph
        var building = _manager.RegisterBuilding(def, position);
        if (building == null) return;

        // Create visual GameObject
        var go = new GameObject($"Building_{id}");
        go.transform.position = position;
        go.layer = 10; // Interactable layer

        // Base ruin mesh (cube representing the shell)
        var ruin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ruin.name = "RuinShell";
        ruin.transform.SetParent(go.transform);
        ruin.transform.localPosition = new Vector3(0, 2, 0);
        ruin.transform.localScale = new Vector3(6, 4, 6);
        var ruinRend = ruin.GetComponent<Renderer>();
        ruinRend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        ruinRend.material.color = new Color(0.4f, 0.35f, 0.3f); // weathered brown

        // Add trigger collider for interaction range
        var trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 5f;
        go.AddComponent<Rigidbody>().isKinematic = true;

        // Add behaviour
        var behaviour = go.AddComponent<SettlementBuildingBehaviour>();
        // Set definition via serialized field workaround
        var defField = typeof(SettlementBuildingBehaviour).GetField("_definition",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        defField?.SetValue(behaviour, def);
        behaviour.Initialize(building);

        // Label
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform);
        labelGo.transform.localPosition = new Vector3(0, 5, 0);
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = displayName;
        tm.characterSize = 0.5f;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.LowerCenter;
        tm.color = Color.white;
        tm.fontSize = 24;

        Debug.Log($"settlement playtest: placed {displayName} at {position}, {repairStageCount} repair stages");
    }

    private void SetupUI()
    {
        var uiGo = new GameObject("SettlementUI");
        _inspectUI = uiGo.AddComponent<SettlementInspectUI>();
    }

    private void SetupLighting()
    {
        var lightGo = new GameObject("DirectionalLight");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.9f, 0.8f);
        light.intensity = 1.2f;
        lightGo.transform.rotation = Quaternion.Euler(45, -30, 0);

        RenderSettings.ambientLight = new Color(0.3f, 0.28f, 0.25f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.5f, 0.48f, 0.45f);
        RenderSettings.fogDensity = 0.005f;
        RenderSettings.fogMode = FogMode.Exponential;
    }

    private void SpawnExplorer()
    {
        var explorer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        explorer.name = "Explorer";
        explorer.transform.position = new Vector3(0, 1, -5);
        explorer.layer = 9; // Player

        var cam = new GameObject("Camera");
        cam.transform.SetParent(explorer.transform);
        cam.transform.localPosition = new Vector3(0, 0.5f, 0);
        cam.AddComponent<Camera>();
        cam.AddComponent<AudioListener>();
    }

    private void ToggleTerritoryDebug()
    {
        _territoryVisible = !_territoryVisible;

        foreach (var vis in _debugTerritoryVisuals)
            if (vis != null) Destroy(vis);
        _debugTerritoryVisuals.Clear();

        if (!_territoryVisible) return;

        foreach (var kvp in _manager.Graph.AllBuildings)
        {
            var building = kvp.Value;
            if (!building.IsClaimed) continue;
            float radius = building.EffectiveTerritoryRadius;
            if (radius <= 0) continue;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"Territory_{building.BuildingId}";
            sphere.transform.position = building.Position;
            sphere.transform.localScale = Vector3.one * radius * 2;
            Destroy(sphere.GetComponent<Collider>());
            var rend = sphere.GetComponent<Renderer>();
            rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            rend.material.color = new Color(0, 1, 0, 0.1f);
            // Make transparent
            rend.material.SetFloat("_Surface", 1); // Transparent
            rend.material.SetFloat("_Blend", 0);
            rend.material.renderQueue = 3000;
            _debugTerritoryVisuals.Add(sphere);
        }

        Debug.Log($"settlement playtest: territory debug {(_territoryVisible ? "on" : "off")}");
    }

    private void LogSettlementState()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== settlement state ===");
        foreach (var kvp in _manager.Graph.AllBuildings)
        {
            var b = kvp.Value;
            sb.AppendLine($"  {b.BuildingId}: repair={b.RepairLevel}/{b.Definition.MaxRepairLevel} " +
                          $"claimed={b.IsClaimed} tier={b.UpgradeTier} " +
                          $"workers={b.WorkerCount}/{b.MaxWorkerSlots} " +
                          $"territory={b.EffectiveTerritoryRadius:F0}m " +
                          $"factory={_manager.Graph.HasFactoryConnection(b.BuildingId)}");
        }
        sb.AppendLine($"  roads: {_manager.Graph.Roads.Count}");
        foreach (var (a, b) in _manager.Graph.Roads)
            sb.AppendLine($"    {a} <-> {b}");
        Debug.Log(sb.ToString());
    }

    private void OnDestroy()
    {
        foreach (var so in _runtimeSOs)
            if (so != null) DestroyImmediate(so);
        foreach (var vis in _debugTerritoryVisuals)
            if (vis != null) Destroy(vis);
    }
}
```

**Step 2: Verify compilation**

**Step 3: Create the playtest scene in Unity**

Create an empty scene `Assets/_Slopworks/Scenes/JoePlaytest/SettlementPlaytest.unity` with a single GameObject that has the `SettlementPlaytestSetup` component.

**Step 4: Test manually**

1. Hit Play
2. Walk to a building cube, press E — inspect UI opens
3. Press R — repair advances, visual should log
4. Repeat until claimed — production starts (check console logs)
5. Press W — worker assigned
6. Walk to another building, press B — road builds between last two
7. Press T — territory spheres appear
8. Press M — full state dump in console

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Settlement/SettlementPlaytestSetup.cs
git commit -m "add settlement playtest bootstrapper with debug controls"
```

---

## Task 10: Verify all tests pass and commit final state

**Step 1: Run all EditMode tests**

Expected: existing tests + 48 new settlement tests (27 SettlementBuilding + 21 SettlementGraph) all pass.

**Step 2: Verify zero compilation errors**

**Step 3: Final commit if any cleanup needed**

---

## Implementation order summary

| Task | What | Tests | Depends on |
|------|------|-------|------------|
| 1 | Data definitions (enums, structs, events) | -- | -- |
| 2 | SettlementBuildingDefinitionSO | -- | 1 |
| 3 | SettlementBuilding simulation + 27 tests | 27 | 1, 2 |
| 4 | SettlementGraph simulation + 21 tests | 21 | 3 |
| 5 | SettlementBuildingBehaviour wrapper | -- | 3 |
| 6 | SettlementManagerBehaviour singleton | -- | 4 |
| 7 | SettlementInspectUI | -- | 3 |
| 8 | SettlementRoadBehaviour visual | -- | 4 |
| 9 | Playtest bootstrapper | manual | 5, 6, 7, 8 |
| 10 | Verify all tests pass | -- | 9 |

**Total new tests:** 48 (27 + 21)
**Total new files:** 13
**Estimated commits:** 10
