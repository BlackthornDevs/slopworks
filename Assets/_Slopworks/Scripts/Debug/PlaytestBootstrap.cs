using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Plain C# class that creates all shared playtest infrastructure in one call.
/// Not a MonoBehaviour -- takes a host MonoBehaviour for StartCoroutine.
/// Each dev bootstrapper calls new PlaytestBootstrap(this).Setup() in Awake.
/// </summary>
public class PlaytestBootstrap
{
    private readonly MonoBehaviour _host;
    private readonly ushort _beltSpeed;

    public PlaytestBootstrap(MonoBehaviour host, ushort beltSpeed = 4)
    {
        _host = host;
        _beltSpeed = beltSpeed;
    }

    public PlaytestContext Setup()
    {
        var ctx = new PlaytestContext { RuntimeSOs = new List<ScriptableObject>() };
        CreateDefinitions(ctx);
        CreateRegistries(ctx);
        CreateInfrastructure(ctx);
        CreatePlayer(ctx);
        WirePlayerCombat(ctx);
        CreateEnemyTemplate(ctx);
        CreateHUD(ctx);
        _host.StartCoroutine(PreloadInventory(ctx));
        return ctx;
    }

    private void CreateDefinitions(PlaytestContext ctx)
    {
        // Structural
        ctx.FoundationDef = ScriptableObject.CreateInstance<FoundationDefinitionSO>();
        ctx.FoundationDef.foundationId = "foundation_1x1";
        ctx.FoundationDef.displayName = "Foundation 1x1";
        ctx.FoundationDef.size = Vector2Int.one;
        ctx.FoundationDef.generatesSnapPoints = true;
        ctx.RuntimeSOs.Add(ctx.FoundationDef);

        ctx.WallDef = ScriptableObject.CreateInstance<WallDefinitionSO>();
        ctx.WallDef.wallId = "wall_basic";
        ctx.WallDef.displayName = "Basic Wall";
        ctx.RuntimeSOs.Add(ctx.WallDef);

        ctx.RampDef = ScriptableObject.CreateInstance<RampDefinitionSO>();
        ctx.RampDef.rampId = "ramp_basic";
        ctx.RampDef.displayName = "Basic Ramp";
        ctx.RampDef.footprintLength = 3;
        ctx.RuntimeSOs.Add(ctx.RampDef);

        // Automation
        ctx.SmelterDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        ctx.SmelterDef.machineId = "smelter_basic";
        ctx.SmelterDef.machineType = PlaytestContext.SmelterType;
        ctx.SmelterDef.displayName = "Basic Smelter";
        ctx.SmelterDef.size = Vector2Int.one;
        ctx.SmelterDef.inputBufferSize = 2;
        ctx.SmelterDef.outputBufferSize = 2;
        ctx.SmelterDef.processingSpeed = 1f;
        ctx.SmelterDef.ports = new[]
        {
            new MachinePort
            {
                localOffset = Vector2Int.zero,
                direction = new Vector2Int(-1, 0),
                type = PortType.Input
            },
            new MachinePort
            {
                localOffset = Vector2Int.zero,
                direction = new Vector2Int(1, 0),
                type = PortType.Output
            }
        };
        ctx.RuntimeSOs.Add(ctx.SmelterDef);

        ctx.StorageDef = ScriptableObject.CreateInstance<StorageDefinitionSO>();
        ctx.StorageDef.storageId = "storage_bin";
        ctx.StorageDef.displayName = "Storage Bin";
        ctx.StorageDef.slotCount = 4;
        ctx.StorageDef.maxStackSize = 50;
        ctx.StorageDef.size = Vector2Int.one;
        ctx.StorageDef.ports = new[]
        {
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(-1, 0), type = PortType.Input },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(1, 0), type = PortType.Input },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(0, -1), type = PortType.Input },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(0, 1), type = PortType.Input },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(-1, 0), type = PortType.Output },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(1, 0), type = PortType.Output },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(0, -1), type = PortType.Output },
            new MachinePort { localOffset = Vector2Int.zero, direction = new Vector2Int(0, 1), type = PortType.Output },
        };
        ctx.RuntimeSOs.Add(ctx.StorageDef);

        ctx.IronOreDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        ctx.IronOreDef.itemId = PlaytestContext.IronOre;
        ctx.IronOreDef.displayName = "Iron Ore";
        ctx.IronOreDef.category = ItemCategory.RawMaterial;
        ctx.IronOreDef.isStackable = true;
        ctx.IronOreDef.maxStackSize = 64;
        ctx.RuntimeSOs.Add(ctx.IronOreDef);

        ctx.IronIngotDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        ctx.IronIngotDef.itemId = PlaytestContext.IronIngot;
        ctx.IronIngotDef.displayName = "Iron Ingot";
        ctx.IronIngotDef.category = ItemCategory.Component;
        ctx.IronIngotDef.isStackable = true;
        ctx.IronIngotDef.maxStackSize = 64;
        ctx.RuntimeSOs.Add(ctx.IronIngotDef);

        ctx.IronScrapDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        ctx.IronScrapDef.itemId = PlaytestContext.IronScrap;
        ctx.IronScrapDef.displayName = "Iron Scrap";
        ctx.IronScrapDef.category = ItemCategory.RawMaterial;
        ctx.IronScrapDef.isStackable = true;
        ctx.IronScrapDef.maxStackSize = 64;
        ctx.RuntimeSOs.Add(ctx.IronScrapDef);

        ctx.SmeltRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        ctx.SmeltRecipe.recipeId = PlaytestContext.SmeltIronRecipeId;
        ctx.SmeltRecipe.displayName = "Smelt Iron";
        ctx.SmeltRecipe.inputs = new[] { new RecipeIngredient { itemId = PlaytestContext.IronScrap, count = 1 } };
        ctx.SmeltRecipe.outputs = new[] { new RecipeIngredient { itemId = PlaytestContext.IronIngot, count = 1 } };
        ctx.SmeltRecipe.craftDuration = 2f;
        ctx.SmeltRecipe.requiredMachineType = PlaytestContext.SmelterType;
        ctx.RuntimeSOs.Add(ctx.SmeltRecipe);

        // Combat
        ctx.WeaponDef = ScriptableObject.CreateInstance<WeaponDefinitionSO>();
        ctx.WeaponDef.weaponId = "test_rifle";
        ctx.WeaponDef.damage = 25f;
        ctx.WeaponDef.fireRate = 2f;
        ctx.WeaponDef.range = 50f;
        ctx.WeaponDef.damageType = DamageType.Kinetic;
        ctx.WeaponDef.magazineSize = 12;
        ctx.WeaponDef.reloadTime = 1.5f;
        ctx.RuntimeSOs.Add(ctx.WeaponDef);

        ctx.FaunaDef = ScriptableObject.CreateInstance<FaunaDefinitionSO>();
        ctx.FaunaDef.faunaId = "test_grunt";
        ctx.FaunaDef.maxHealth = 50f;
        ctx.FaunaDef.moveSpeed = 3f;
        ctx.FaunaDef.attackDamage = 10f;
        ctx.FaunaDef.attackRange = 2.5f;
        ctx.FaunaDef.attackCooldown = 1.5f;
        ctx.FaunaDef.sightRange = 15f;
        ctx.FaunaDef.sightAngle = 120f;
        ctx.FaunaDef.hearingRange = 8f;
        ctx.FaunaDef.attackDamageType = DamageType.Kinetic;
        ctx.FaunaDef.alertRange = 20f;
        ctx.FaunaDef.strafeSpeed = 2.5f;
        ctx.FaunaDef.strafeRadius = 3f;
        ctx.FaunaDef.baseBravery = 0.5f;
        ctx.RuntimeSOs.Add(ctx.FaunaDef);

        ctx.EnemyDiedEvent = ScriptableObject.CreateInstance<GameEventSO>();
        ctx.RuntimeSOs.Add(ctx.EnemyDiedEvent);
    }

    private void CreateRegistries(PlaytestContext ctx)
    {
        var registryObj = new GameObject("Registries");
        registryObj.SetActive(false);

        var itemRegistry = registryObj.AddComponent<ItemRegistry>();
        var itemsField = typeof(ItemRegistry).GetField("_items",
            BindingFlags.NonPublic | BindingFlags.Instance);
        itemsField?.SetValue(itemRegistry, new[] { ctx.IronOreDef, ctx.IronIngotDef, ctx.IronScrapDef });

        var recipeRegistry = registryObj.AddComponent<RecipeRegistry>();
        var recipesField = typeof(RecipeRegistry).GetField("_recipes",
            BindingFlags.NonPublic | BindingFlags.Instance);
        recipesField?.SetValue(recipeRegistry, new[] { ctx.SmeltRecipe });

        registryObj.SetActive(true);
        Debug.Log("playtest: registries created");
    }

    private void CreateInfrastructure(PlaytestContext ctx)
    {
        ctx.Grid = new FactoryGrid();
        ctx.SnapRegistry = new SnapPointRegistry();
        ctx.PlacementService = new StructuralPlacementService(ctx.Grid, ctx.SnapRegistry);

        RecipeSO LookupRecipe(string id) => id == PlaytestContext.SmeltIronRecipeId ? ctx.SmeltRecipe : null;
        ctx.Simulation = new FactorySimulation(LookupRecipe);
        ctx.Simulation.BeltSpeed = _beltSpeed;
        ctx.PortRegistry = new PortNodeRegistry();
        ctx.ConnectionResolver = new ConnectionResolver(ctx.PortRegistry, ctx.Simulation);
        ctx.AutomationService = new BuildingPlacementService(
            ctx.Grid, ctx.PortRegistry, ctx.ConnectionResolver, ctx.Simulation);
    }

    private void CreatePlayer(PlaytestContext ctx)
    {
        float centerX = 10f * FactoryGrid.CellSize;
        float centerZ = 10f * FactoryGrid.CellSize;

        var player = new GameObject("Player");
        player.layer = PhysicsLayers.Player;
        player.transform.position = new Vector3(centerX, 1.5f, centerZ - 5f);

        var capsule = player.AddComponent<CapsuleCollider>();
        capsule.radius = 0.3f;
        capsule.height = 1.8f;
        capsule.center = new Vector3(0, 0.9f, 0);

        var rb = player.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Repurpose existing Main Camera as isometric camera
        var isoCam = Camera.main;
        if (isoCam != null)
        {
            isoCam.gameObject.name = "IsometricCamera";
            var oldListener = isoCam.GetComponent<AudioListener>();
            if (oldListener != null) Object.DestroyImmediate(oldListener);
            if (isoCam.GetComponent<PlaytestCameraController>() == null)
                isoCam.gameObject.AddComponent<PlaytestCameraController>();
            isoCam.transform.position = new Vector3(centerX, 20f, centerZ - 12f);
            isoCam.transform.LookAt(new Vector3(centerX, 0f, centerZ));
            isoCam.gameObject.SetActive(false);
        }

        // FPS camera on player
        var camObj = new GameObject("PlayerCamera");
        camObj.tag = "MainCamera";
        camObj.transform.SetParent(player.transform, false);
        camObj.transform.localPosition = new Vector3(0, 1.6f, 0);
        var fpsCam = camObj.AddComponent<Camera>();
        camObj.AddComponent<AudioListener>();

        // Camera mode toggle
        var playerCtrl = player.GetComponent<PlayerController>();
        var modeController = player.AddComponent<CameraModeController>();
        var modeFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(CameraModeController).GetField("_fpsCamera", modeFlags)?.SetValue(modeController, fpsCam);
        if (isoCam != null)
            typeof(CameraModeController).GetField("_isometricCamera", modeFlags)?.SetValue(modeController, isoCam);
        typeof(CameraModeController).GetField("_playerController", modeFlags)?.SetValue(modeController, playerCtrl);

        // Components (PlayerInventory before PlayerController so Awake finds it)
        ctx.PlayerInventory = player.AddComponent<PlayerInventory>();
        player.AddComponent<PlayerController>();
        player.AddComponent<HealthBehaviour>();

        // Pickup trigger (child)
        var pickupObj = new GameObject("PickupTrigger");
        pickupObj.transform.SetParent(player.transform, false);
        pickupObj.layer = PhysicsLayers.Player;
        pickupObj.AddComponent<ItemPickupTrigger>();

        ctx.PlayerObject = player;
        Debug.Log($"playtest: player created at ({centerX}, 1.5, {centerZ - 5})");
    }

    private void WirePlayerCombat(PlaytestContext ctx)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var fpsCam = ctx.PlayerObject.GetComponentInChildren<Camera>();

        // Camera effects on FPS camera object
        var camObj = fpsCam.gameObject;
        if (camObj.GetComponent<CameraRecoil>() == null)
            camObj.AddComponent<CameraRecoil>();
        if (camObj.GetComponent<CameraShake>() == null)
            camObj.AddComponent<CameraShake>();

        // Muzzle flash as child of camera
        var muzzleObj = new GameObject("MuzzleFlashPoint");
        muzzleObj.transform.SetParent(camObj.transform);
        muzzleObj.transform.localPosition = new Vector3(0f, -0.1f, 0.5f);
        muzzleObj.AddComponent<MuzzleFlash>();

        // WeaponBehaviour -- must set _weaponDefinition before Awake creates WeaponController
        ctx.PlayerObject.SetActive(false);
        ctx.WeaponBehaviour = ctx.PlayerObject.AddComponent<WeaponBehaviour>();
        typeof(WeaponBehaviour).GetField("_weaponDefinition", flags)?.SetValue(ctx.WeaponBehaviour, ctx.WeaponDef);
        typeof(WeaponBehaviour).GetField("_camera", flags)?.SetValue(ctx.WeaponBehaviour, fpsCam);
        ctx.PlayerObject.SetActive(true);

        // HealthBehaviour max health via reflection
        var health = ctx.PlayerObject.GetComponent<HealthBehaviour>();
        typeof(HealthBehaviour).GetField("_maxHealth", flags)?.SetValue(health, 100f);

        Debug.Log("playtest: player combat wired (weapon, recoil, shake, muzzle flash)");
    }

    private void CreateEnemyTemplate(PlaytestContext ctx)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;

        ctx.EnemyTemplate = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        ctx.EnemyTemplate.name = "EnemyTemplate";
        ctx.EnemyTemplate.layer = PhysicsLayers.Fauna;
        PlaytestToolController.SetColor(ctx.EnemyTemplate, new Color(0.8f, 0.2f, 0.2f));

        // Deactivate before adding components to prevent Awake/Start from running
        ctx.EnemyTemplate.SetActive(false);

        var rb = ctx.EnemyTemplate.AddComponent<Rigidbody>();
        rb.freezeRotation = true;

        var agent = ctx.EnemyTemplate.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.speed = ctx.FaunaDef.moveSpeed;
        agent.stoppingDistance = ctx.FaunaDef.attackRange * 0.8f;

        var health = ctx.EnemyTemplate.AddComponent<HealthBehaviour>();
        typeof(HealthBehaviour).GetField("_maxHealth", flags)?.SetValue(health, ctx.FaunaDef.maxHealth);

        var controller = ctx.EnemyTemplate.AddComponent<FaunaController>();
        typeof(FaunaController).GetField("_def", flags)?.SetValue(controller, ctx.FaunaDef);
        typeof(FaunaController).GetField("_onDeathEvent", flags)?.SetValue(controller, ctx.EnemyDiedEvent);

        ctx.EnemyTemplate.AddComponent<EnemyHitFlash>();
        ctx.EnemyTemplate.AddComponent<EnemyKnockback>();

        Debug.Log("playtest: enemy template created (inactive)");
    }

    private void CreateHUD(PlaytestContext ctx)
    {
        var canvasObj = new GameObject("HUDCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        canvasObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        canvasObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        ctx.PlayerHUD = canvasObj.AddComponent<PlayerHUD>();
        canvasObj.AddComponent<RecipeSelectionUI>();
        canvasObj.AddComponent<StorageUI>();
        canvasObj.AddComponent<InventoryUI>();
        var hitMarker = canvasObj.AddComponent<HitMarkerUI>();

        // Wire hit marker to weapon
        if (ctx.WeaponBehaviour != null)
            ctx.WeaponBehaviour.SetHitMarker(hitMarker);
    }

    private IEnumerator PreloadInventory(PlaytestContext ctx)
    {
        yield return null;
        ctx.PlayerInventory.TryAdd(ItemInstance.Create(PlaytestContext.IronScrap), 10);
        ctx.PlayerInventory.TryAdd(ItemInstance.Create(PlaytestContext.IronOre), 5);
        Debug.Log("playtest: preloaded items into inventory");
    }
}
