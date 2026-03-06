# Boss Encounter (J-020) Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a boss enemy to the tower's top floor with elevated stats, distinct visuals, and guaranteed loot rewards on kill.

**Architecture:** No new classes. Boss uses existing FaunaDefinitionSO with elevated stats, existing enemy template pattern, and existing LootDropDefinition system. All changes are in PlaytestContext (constants), PlaytestBootstrap (definitions), KevinPlaytestSetup (wiring), and PlaytestToolController (color).

**Tech Stack:** Unity, C#, existing tower/combat/loot systems

---

### Task 1: Add boss item ID and definition fields to PlaytestContext

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Debug/PlaytestContext.cs:65` (add constant), `:49-52` (add fields)

**Step 1: Add the boss_blueprint constant**

After line 65 (`KeyFragment`), add:

```csharp
public const string BossBlueprint = "boss_blueprint";
```

**Step 2: Add boss fauna and template fields**

After line 52 (`InteriorEnemyTemplate`), add:

```csharp
public FaunaDefinitionSO BossFaunaDef;
public GameObject BossEnemyTemplate;
```

**Step 3: Add boss blueprint item definition field**

After line 38 (`KeyFragmentDef`), add:

```csharp
public ItemDefinitionSO BossBlueprintDef;
```

**Step 4: Commit**

```bash
git add Assets/_Slopworks/Scripts/Debug/PlaytestContext.cs
git commit -m "Add boss enemy and blueprint fields to PlaytestContext"
```

---

### Task 2: Create boss definitions in PlaytestBootstrap

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Debug/PlaytestBootstrap.cs`

**Step 1: Add boss blueprint item definition**

After the KeyFragmentDef block (line 165), add:

```csharp
ctx.BossBlueprintDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
ctx.BossBlueprintDef.itemId = PlaytestContext.BossBlueprint;
ctx.BossBlueprintDef.displayName = "Boss Blueprint";
ctx.BossBlueprintDef.category = ItemCategory.Component;
ctx.BossBlueprintDef.isStackable = true;
ctx.BossBlueprintDef.maxStackSize = 16;
ctx.RuntimeSOs.Add(ctx.BossBlueprintDef);
```

**Step 2: Add boss fauna definition**

After the InteriorFaunaDef block (line 228), add:

```csharp
ctx.BossFaunaDef = ScriptableObject.CreateInstance<FaunaDefinitionSO>();
ctx.BossFaunaDef.faunaId = "tower_boss";
ctx.BossFaunaDef.maxHealth = 300f;
ctx.BossFaunaDef.moveSpeed = 2.5f;
ctx.BossFaunaDef.attackDamage = 25f;
ctx.BossFaunaDef.attackRange = 3f;
ctx.BossFaunaDef.attackCooldown = 0.8f;
ctx.BossFaunaDef.sightRange = 30f;
ctx.BossFaunaDef.sightAngle = 120f;
ctx.BossFaunaDef.hearingRange = 15f;
ctx.BossFaunaDef.attackDamageType = DamageType.Kinetic;
ctx.BossFaunaDef.alertRange = 30f;
ctx.BossFaunaDef.strafeSpeed = 2f;
ctx.BossFaunaDef.strafeRadius = 4f;
ctx.BossFaunaDef.baseBravery = 1.0f;
ctx.RuntimeSOs.Add(ctx.BossFaunaDef);
```

**Step 3: Create boss enemy template**

Add a new method after `CreateInteriorEnemyTemplate` (after line 497):

```csharp
private void CreateBossEnemyTemplate(PlaytestContext ctx)
{
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;

    ctx.BossEnemyTemplate = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    ctx.BossEnemyTemplate.name = "BossEnemyTemplate";
    ctx.BossEnemyTemplate.layer = PhysicsLayers.Fauna;
    ctx.BossEnemyTemplate.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
    PlaytestToolController.SetColor(ctx.BossEnemyTemplate, new Color(0.5f, 0.1f, 0.6f));

    ctx.BossEnemyTemplate.SetActive(false);

    var rb = ctx.BossEnemyTemplate.AddComponent<Rigidbody>();
    rb.freezeRotation = true;

    var agent = ctx.BossEnemyTemplate.AddComponent<UnityEngine.AI.NavMeshAgent>();
    agent.speed = ctx.BossFaunaDef.moveSpeed;
    agent.stoppingDistance = ctx.BossFaunaDef.attackRange * 0.8f;
    agent.radius = 1.0f;
    agent.height = 4.0f;

    var health = ctx.BossEnemyTemplate.AddComponent<HealthBehaviour>();
    typeof(HealthBehaviour).GetField("_maxHealth", flags)?.SetValue(health, ctx.BossFaunaDef.maxHealth);

    var controller = ctx.BossEnemyTemplate.AddComponent<FaunaController>();
    typeof(FaunaController).GetField("_def", flags)?.SetValue(controller, ctx.BossFaunaDef);
    typeof(FaunaController).GetField("_onDeathEvent", flags)?.SetValue(controller, ctx.EnemyDiedEvent);

    ctx.BossEnemyTemplate.AddComponent<EnemyHitFlash>();
    ctx.BossEnemyTemplate.AddComponent<EnemyKnockback>();

    Debug.Log("playtest: boss enemy template created (inactive, purple, 2.5x scale)");
}
```

**Step 4: Call it in Setup()**

After line 31 (`CreateInteriorEnemyTemplate(ctx);`), add:

```csharp
CreateBossEnemyTemplate(ctx);
```

**Step 5: Commit**

```bash
git add Assets/_Slopworks/Scripts/Debug/PlaytestBootstrap.cs
git commit -m "Add boss fauna definition and enemy template to PlaytestBootstrap"
```

---

### Task 3: Add boss_blueprint color to PlaytestToolController

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Debug/PlaytestToolController.cs:1914`

**Step 1: Add color entry**

In the `GetItemColor` switch (after line 1914, the KeyFragment case), add:

```csharp
PlaytestContext.BossBlueprint => new Color(1.0f, 0.85f, 0.0f),
```

Gold color to make boss loot feel special.

**Step 2: Commit**

```bash
git add Assets/_Slopworks/Scripts/Debug/PlaytestToolController.cs
git commit -m "Add boss blueprint color to GetItemColor"
```

---

### Task 4: Wire boss into tower in KevinPlaytestSetup

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Debug/KevinPlaytestSetup.cs`

This is the main integration task. Four changes in one file.

**Step 1: Add boss loot entries to loot table**

In `CreateTowerDefinitions()`, after the existing loot entries list (line 898), add two entries before the closing `};`:

```csharp
new LootDropDefinition { itemId = PlaytestContext.BossBlueprint, rarity = LootRarity.Legendary, dropWeight = 10f, minAmount = 1, maxAmount = 1, minFloorElevation = 6, maxFloorElevation = 7 },
new LootDropDefinition { itemId = PlaytestContext.SignalDecoder, rarity = LootRarity.Rare, dropWeight = 8f, minAmount = 1, maxAmount = 2, minFloorElevation = 6, maxFloorElevation = 7 },
```

The high dropWeight + boss-floor-only elevation filter makes these effectively guaranteed on the boss floor.

**Step 2: Update boss floor spawn entries**

In `CreateTowerEnemies()`, replace the F6 boss spawn entries (lines 1016-1020) with:

```csharp
chunk.spawnEntries = new List<TowerSpawnEntry>
{
    new TowerSpawnEntry { templateIndex = 2, count = 1 },
    new TowerSpawnEntry { templateIndex = 0, count = 2 }
};
```

templateIndex 2 = boss, plus 2 grunt adds.

**Step 3: Add boss template to enemy templates array**

In `CreateTowerEnemies()`, update the templates array (line 981) to include the boss:

```csharp
var templates = new[] { _ctx.EnemyTemplate, _ctx.InteriorEnemyTemplate, _ctx.BossEnemyTemplate };
```

**Step 4: Add boss loot spawning on wave clear**

In `NavigateToFloor()`, inside the boss completion block (after line 1155 where it logs "BOSS DEFEATED"), add boss reward spawning:

```csharp
// Spawn boss rewards at arena center
SpawnBossRewards(capturedFloor);
```

Then add the new method anywhere in the tower section:

```csharp
private void SpawnBossRewards(int bossFloorIndex)
{
    var origin = TowerChunkLayoutGenerator.GetChunkOrigin(TowerBasePosition, bossFloorIndex);
    float size = TowerChunkLayoutGenerator.BossSize;
    var center = origin + new Vector3(size * 0.5f, 0.5f, size * 0.5f);

    var rng = new System.Random();

    // Guaranteed blueprint
    var blueprintObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
    blueprintObj.name = "BossReward_blueprint";
    blueprintObj.transform.position = center + new Vector3(-1f, 0f, 0f);
    blueprintObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
    DestroyImmediate(blueprintObj.GetComponent<BoxCollider>());
    PlaytestToolController.SetColor(blueprintObj, PlaytestToolController.GetItemColor(PlaytestContext.BossBlueprint));
    var bpWorldItem = blueprintObj.AddComponent<WorldItem>();
    bpWorldItem.Initialize(_ctx.BossBlueprintDef, 1);
    _towerInteractables.Add(blueprintObj);

    // 1-2 bonus rare material drops from loot table
    int bonusCount = rng.Next(1, 3);
    for (int i = 0; i < bonusCount; i++)
    {
        var drop = _towerLootTable.ResolveDrop(bossFloorIndex, _towerController.CurrentTier, rng);
        if (!drop.HasValue) continue;

        var def = GetItemDefinition(drop.Value.itemId);
        if (def == null) continue;

        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = $"BossReward_{drop.Value.itemId}_{i}";
        obj.transform.position = center + new Vector3(1f + i * 0.8f, 0f, 0f);
        obj.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
        DestroyImmediate(obj.GetComponent<BoxCollider>());
        PlaytestToolController.SetColor(obj, PlaytestToolController.GetItemColor(drop.Value.itemId));
        var worldItem = obj.AddComponent<WorldItem>();
        worldItem.Initialize(def, drop.Value.amount);
        _towerInteractables.Add(obj);
    }

    Debug.Log($"tower: boss rewards spawned at arena center ({1 + bonusCount} items)");
}
```

**Step 5: Add boss_blueprint to TowerItemIds**

Update the `TowerItemIds` array (line 1184) to include boss_blueprint so it gets cleared on death:

```csharp
private static readonly string[] TowerItemIds =
{
    PlaytestContext.PowerCell, PlaytestContext.SignalDecoder,
    PlaytestContext.ReinforcedPlating, PlaytestContext.KeyFragment,
    PlaytestContext.BossBlueprint
};
```

**Step 6: Add boss_blueprint to GetItemDefinition switch**

In `GetItemDefinition()` (line 1236), add a case:

```csharp
PlaytestContext.BossBlueprint => _ctx.BossBlueprintDef,
```

**Step 7: Commit**

```bash
git add Assets/_Slopworks/Scripts/Debug/KevinPlaytestSetup.cs
git commit -m "Wire boss enemy, loot rewards, and spawn config into tower"
```

---

### Task 5: Manual playtest verification

**Steps:**
1. Open KevinPlaytest scene, hit Play
2. Enter the tower (cyan portal)
3. Collect 4 key fragments across floors (or use existing debug to add them)
4. Navigate to floor 7 (boss floor, index 6) via elevator
5. Verify: boss is a large purple capsule with 300 HP, 2 grunt adds spawn alongside
6. Kill all enemies (boss takes ~12 shots at 25 damage)
7. Verify: "BOSS DEFEATED" log appears, tier increments
8. Verify: gold blueprint cube and 1-2 bonus loot cubes spawn at arena center
9. Walk over rewards to pick them up
10. Extract via elevator (lobby -> exit)
11. Verify: boss_blueprint appears in inventory after extraction
12. Re-enter tower, verify boss floor is locked again (fragments consumed), new run starts at tier 2

---

### Task 6: Run all EditMode tests

**Step 1: Run tests**

Run all EditMode tests and verify 891+ pass with zero failures.

No new tests needed -- boss uses existing FaunaDefinitionSO (no new simulation class), existing loot table (already tested), and existing TowerController boss methods (already tested with 41+ tests). All changes are wiring in the bootstrapper.

**Step 2: Final commit**

If any fixes were needed during playtest, commit them:

```bash
git add -A
git commit -m "J-020: Boss encounter complete

- Boss enemy: tower_boss FaunaDefinitionSO (300 HP, 25 dmg, never flees)
- Boss visual: 2.5x purple capsule, distinct from grunts and stalkers
- Boss floor spawns: 1 boss + 2 grunt adds (was 4 grunts + 4 stalkers)
- Boss rewards: guaranteed blueprint + 1-2 rare material drops as WorldItems
- boss_blueprint item added to loot table, inventory, death cleanup
- Existing tower boss flow unchanged: fragment gate, consume on entry, tier on kill"
```
