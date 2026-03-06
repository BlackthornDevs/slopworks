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
        CreateInteriorEnemyTemplate(ctx);
        CreateBossEnemyTemplate(ctx);
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

        ctx.TurretAmmoDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        ctx.TurretAmmoDef.itemId = PlaytestContext.TurretAmmo;
        ctx.TurretAmmoDef.displayName = "Turret Ammo";
        ctx.TurretAmmoDef.category = ItemCategory.Ammo;
        ctx.TurretAmmoDef.isStackable = true;
        ctx.TurretAmmoDef.maxStackSize = 64;
        ctx.RuntimeSOs.Add(ctx.TurretAmmoDef);

        ctx.PowerCellDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        ctx.PowerCellDef.itemId = PlaytestContext.PowerCell;
        ctx.PowerCellDef.displayName = "Power Cell";
        ctx.PowerCellDef.category = ItemCategory.Component;
        ctx.PowerCellDef.isStackable = true;
        ctx.PowerCellDef.maxStackSize = 16;
        ctx.RuntimeSOs.Add(ctx.PowerCellDef);

        ctx.SignalDecoderDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        ctx.SignalDecoderDef.itemId = PlaytestContext.SignalDecoder;
        ctx.SignalDecoderDef.displayName = "Signal Decoder";
        ctx.SignalDecoderDef.category = ItemCategory.Component;
        ctx.SignalDecoderDef.isStackable = true;
        ctx.SignalDecoderDef.maxStackSize = 16;
        ctx.RuntimeSOs.Add(ctx.SignalDecoderDef);

        ctx.ReinforcedPlatingDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        ctx.ReinforcedPlatingDef.itemId = PlaytestContext.ReinforcedPlating;
        ctx.ReinforcedPlatingDef.displayName = "Reinforced Plating";
        ctx.ReinforcedPlatingDef.category = ItemCategory.Component;
        ctx.ReinforcedPlatingDef.isStackable = true;
        ctx.ReinforcedPlatingDef.maxStackSize = 16;
        ctx.RuntimeSOs.Add(ctx.ReinforcedPlatingDef);

        ctx.KeyFragmentDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        ctx.KeyFragmentDef.itemId = PlaytestContext.KeyFragment;
        ctx.KeyFragmentDef.displayName = "Key Fragment";
        ctx.KeyFragmentDef.category = ItemCategory.Component;
        ctx.KeyFragmentDef.isStackable = true;
        ctx.KeyFragmentDef.maxStackSize = 16;
        ctx.RuntimeSOs.Add(ctx.KeyFragmentDef);

        ctx.BossBlueprintDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        ctx.BossBlueprintDef.itemId = PlaytestContext.BossBlueprint;
        ctx.BossBlueprintDef.displayName = "Boss Blueprint";
        ctx.BossBlueprintDef.category = ItemCategory.Component;
        ctx.BossBlueprintDef.isStackable = true;
        ctx.BossBlueprintDef.maxStackSize = 16;
        ctx.RuntimeSOs.Add(ctx.BossBlueprintDef);

        ctx.SmeltRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        ctx.SmeltRecipe.recipeId = PlaytestContext.SmeltIronRecipeId;
        ctx.SmeltRecipe.displayName = "Smelt Iron";
        ctx.SmeltRecipe.inputs = new[] { new RecipeIngredient { itemId = PlaytestContext.IronScrap, count = 1 } };
        ctx.SmeltRecipe.outputs = new[] { new RecipeIngredient { itemId = PlaytestContext.IronIngot, count = 1 } };
        ctx.SmeltRecipe.craftDuration = 2f;
        ctx.SmeltRecipe.requiredMachineType = PlaytestContext.SmelterType;
        ctx.RuntimeSOs.Add(ctx.SmeltRecipe);

        ctx.TurretAmmoRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        ctx.TurretAmmoRecipe.recipeId = PlaytestContext.TurretAmmoRecipeId;
        ctx.TurretAmmoRecipe.displayName = "Craft Turret Ammo";
        ctx.TurretAmmoRecipe.inputs = new[] { new RecipeIngredient { itemId = PlaytestContext.IronIngot, count = 1 } };
        ctx.TurretAmmoRecipe.outputs = new[] { new RecipeIngredient { itemId = PlaytestContext.TurretAmmo, count = 4 } };
        ctx.TurretAmmoRecipe.craftDuration = 3f;
        ctx.TurretAmmoRecipe.requiredMachineType = PlaytestContext.SmelterType;
        ctx.RuntimeSOs.Add(ctx.TurretAmmoRecipe);

        // Combat
        ctx.WeaponDef = ScriptableObject.CreateInstance<WeaponDefinitionSO>();
        ctx.WeaponDef.weaponId = "test_rifle";
        ctx.WeaponDef.damage = 25f;
        ctx.WeaponDef.fireRate = 4f;
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

        ctx.InteriorFaunaDef = ScriptableObject.CreateInstance<FaunaDefinitionSO>();
        ctx.InteriorFaunaDef.faunaId = "tower_stalker";
        ctx.InteriorFaunaDef.maxHealth = 30f;
        ctx.InteriorFaunaDef.moveSpeed = 5f;
        ctx.InteriorFaunaDef.attackDamage = 15f;
        ctx.InteriorFaunaDef.attackRange = 1.5f;
        ctx.InteriorFaunaDef.attackCooldown = 0.8f;
        ctx.InteriorFaunaDef.sightRange = 12f;
        ctx.InteriorFaunaDef.sightAngle = 120f;
        ctx.InteriorFaunaDef.hearingRange = 8f;
        ctx.InteriorFaunaDef.attackDamageType = DamageType.Kinetic;
        ctx.InteriorFaunaDef.alertRange = 20f;
        ctx.InteriorFaunaDef.strafeSpeed = 2.5f;
        ctx.InteriorFaunaDef.strafeRadius = 3f;
        ctx.InteriorFaunaDef.baseBravery = 0.3f;
        ctx.RuntimeSOs.Add(ctx.InteriorFaunaDef);

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
        itemsField?.SetValue(itemRegistry, new[] {
            ctx.IronOreDef, ctx.IronIngotDef, ctx.IronScrapDef, ctx.TurretAmmoDef,
            ctx.PowerCellDef, ctx.SignalDecoderDef, ctx.ReinforcedPlatingDef,
            ctx.KeyFragmentDef, ctx.BossBlueprintDef
        });

        var recipeRegistry = registryObj.AddComponent<RecipeRegistry>();
        var recipesField = typeof(RecipeRegistry).GetField("_recipes",
            BindingFlags.NonPublic | BindingFlags.Instance);
        recipesField?.SetValue(recipeRegistry, new[] { ctx.SmeltRecipe, ctx.TurretAmmoRecipe });

        registryObj.SetActive(true);
        Debug.Log("playtest: registries created");
    }

    private void CreateInfrastructure(PlaytestContext ctx)
    {
        ctx.Grid = new FactoryGrid();
        ctx.SnapRegistry = new SnapPointRegistry();
        ctx.PlacementService = new StructuralPlacementService(ctx.Grid, ctx.SnapRegistry);

        RecipeSO LookupRecipe(string id) =>
            id == PlaytestContext.SmeltIronRecipeId ? ctx.SmeltRecipe :
            id == PlaytestContext.TurretAmmoRecipeId ? ctx.TurretAmmoRecipe : null;
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
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

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

        // Weapon viewmodel
        var pistolPrefab = Resources.Load<GameObject>("Models/Pistol/Pistol_01");
        Transform muzzlePoint = camObj.transform; // fallback
        if (pistolPrefab != null)
        {
            var pistol = Object.Instantiate(pistolPrefab, camObj.transform);
            pistol.name = "WeaponModel";
            pistol.transform.localPosition = new Vector3(0.15f, -0.12f, 0.394f);
            pistol.transform.localRotation = Quaternion.identity;
            pistol.transform.localScale = Vector3.one;
            foreach (var col in pistol.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(col);
            SetLayerRecursive(pistol, PhysicsLayers.Player);

            // Replace Built-in Standard materials with URP Lit
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader != null)
            {
                foreach (var r in pistol.GetComponentsInChildren<Renderer>())
                {
                    var oldMats = r.sharedMaterials;
                    var newMats = new Material[oldMats.Length];
                    for (int i = 0; i < oldMats.Length; i++)
                    {
                        var src = oldMats[i];
                        var mat = new Material(urpShader);
                        if (src != null)
                        {
                            mat.SetColor("_BaseColor", src.HasProperty("_Color") ? src.color : Color.gray);
                            if (src.HasProperty("_MainTex") && src.mainTexture != null)
                                mat.SetTexture("_BaseMap", src.mainTexture);
                            if (src.HasProperty("_BumpMap") && src.GetTexture("_BumpMap") != null)
                                mat.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
                            if (src.HasProperty("_MetallicGlossMap") && src.GetTexture("_MetallicGlossMap") != null)
                            {
                                mat.SetTexture("_MetallicGlossMap", src.GetTexture("_MetallicGlossMap"));
                                mat.SetFloat("_Metallic", 1f);
                                mat.SetFloat("_Smoothness", src.HasProperty("_Glossiness") ? src.GetFloat("_Glossiness") : 0.5f);
                            }
                            if (src.HasProperty("_OcclusionMap") && src.GetTexture("_OcclusionMap") != null)
                                mat.SetTexture("_OcclusionMap", src.GetTexture("_OcclusionMap"));
                            if (src.HasProperty("_EmissionMap") && src.GetTexture("_EmissionMap") != null)
                            {
                                mat.SetTexture("_EmissionMap", src.GetTexture("_EmissionMap"));
                                mat.EnableKeyword("_EMISSION");
                                mat.SetColor("_EmissionColor", src.HasProperty("_EmissionColor") ? src.GetColor("_EmissionColor") : Color.black);
                            }
                        }
                        newMats[i] = mat;
                    }
                    r.sharedMaterials = newMats;
                }
            }

            // Place muzzle flash at barrel tip
            var muzzleObj = new GameObject("MuzzleFlashPoint");
            muzzleObj.transform.SetParent(pistol.transform);
            muzzleObj.transform.localPosition = new Vector3(0f, 0.035f, 0.14f);
            muzzleObj.AddComponent<MuzzleFlash>();
            muzzlePoint = muzzleObj.transform;
            Debug.Log("playtest: pistol viewmodel attached to camera");
        }
        else
        {
            // Fallback: muzzle flash on camera
            var muzzleObj = new GameObject("MuzzleFlashPoint");
            muzzleObj.transform.SetParent(camObj.transform);
            muzzleObj.transform.localPosition = new Vector3(0f, -0.1f, 0.5f);
            muzzleObj.AddComponent<MuzzleFlash>();
            Debug.LogWarning("playtest: pistol prefab not found, using invisible weapon");
        }

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

    private void CreateInteriorEnemyTemplate(PlaytestContext ctx)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;

        ctx.InteriorEnemyTemplate = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        ctx.InteriorEnemyTemplate.name = "InteriorEnemyTemplate";
        ctx.InteriorEnemyTemplate.layer = PhysicsLayers.Fauna;
        PlaytestToolController.SetColor(ctx.InteriorEnemyTemplate, new Color(0.2f, 0.8f, 0.3f));

        ctx.InteriorEnemyTemplate.SetActive(false);

        var rb = ctx.InteriorEnemyTemplate.AddComponent<Rigidbody>();
        rb.freezeRotation = true;

        var agent = ctx.InteriorEnemyTemplate.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.speed = ctx.InteriorFaunaDef.moveSpeed;
        agent.stoppingDistance = ctx.InteriorFaunaDef.attackRange * 0.8f;

        var health = ctx.InteriorEnemyTemplate.AddComponent<HealthBehaviour>();
        typeof(HealthBehaviour).GetField("_maxHealth", flags)?.SetValue(health, ctx.InteriorFaunaDef.maxHealth);

        var controller = ctx.InteriorEnemyTemplate.AddComponent<FaunaController>();
        typeof(FaunaController).GetField("_def", flags)?.SetValue(controller, ctx.InteriorFaunaDef);
        typeof(FaunaController).GetField("_onDeathEvent", flags)?.SetValue(controller, ctx.EnemyDiedEvent);

        ctx.InteriorEnemyTemplate.AddComponent<EnemyHitFlash>();
        ctx.InteriorEnemyTemplate.AddComponent<EnemyKnockback>();

        Debug.Log("playtest: interior enemy template created (inactive, green stalker)");
    }

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

    private static void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
