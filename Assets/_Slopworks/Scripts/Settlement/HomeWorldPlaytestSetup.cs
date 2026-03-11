using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// HomeWorld playtest bootstrapper. Wires pre-placed buildings on a dressed 1200m terrain
/// with the settlement system. Drop on a GameObject in the HomeWorldPlaytest scene.
///
/// Buildings are manually placed in the scene editor (drag FBX models, position on terrain).
/// This bootstrapper creates settlement definitions at runtime and registers them with the
/// manager. All debug keys from SettlementPlaytestSetup carry over.
///
/// Controls:
///   WASD - Move, Mouse - Look, Shift - Sprint, Ctrl+Shift - Super-sprint
///   E - Interact with nearest building
///   R - Repair nearest building
///   U - Upgrade nearest building
///   N - Build road between two nearest buildings
///   T - Toggle territory debug spheres
///   M - Log full settlement state
/// </summary>
public class HomeWorldPlaytestSetup : MonoBehaviour
{
    [System.Serializable]
    public class BuildingPlacement
    {
        public GameObject sceneObject;
        public string buildingId;
        public string displayName;
        public SettlementBuildingType buildingType;
        public int repairStageCount;
        public float territoryRadius = 20f;
        public float connectionRange = 150f;
        public int workerSlots = 2;

        [Header("Production (optional)")]
        public string producedItemId;
        public int producedAmount;
        public float productionInterval;
    }

    [Header("Buildings")]
    [SerializeField] private List<BuildingPlacement> _buildings = new();

    [Header("Options")]
    [SerializeField] private bool _autoCreateBuildings = true;

    // -- runtime state --
    private readonly List<ScriptableObject> _runtimeSOs = new();
    private readonly List<SettlementBuildingBehaviour> _buildingBehaviours = new();
    private SettlementManagerBehaviour _manager;
    private GameObject _inspectUIObj;

    // -- territory debug --
    private bool _territoryVisible;
    private readonly List<GameObject> _territorySpheres = new();

    // -- model asset paths (building id -> FBX path) --
    private static readonly Dictionary<string, string> ModelPaths = new()
    {
        { "factory_yard", "Assets/_Slopworks/Art/Models/Settlement/FactoryYard_ConcreteWarehouse.fbx" },
        { "farmstead", "Assets/_Slopworks/Art/Models/Settlement/Farmstead_StoneRuin.fbx" },
        { "watchtower", "Assets/_Slopworks/Art/Models/Settlement/Watchtower_RuinTower.fbx" },
        { "workshop", "Assets/_Slopworks/Art/Models/Settlement/Workshop_BrickBuilding.fbx" },
        { "market", "Assets/_Slopworks/Art/Models/Settlement/Market_TropicalShop.fbx" },
        { "barracks", "Assets/_Slopworks/Art/Models/Settlement/Barracks_ConcreteBuilding.fbx" },
    };

    // default building positions matching dresser pad positions (for auto-create mode)
    private static readonly BuildingConfig[] DefaultBuildings = {
        new("factory_yard", "Factory yard", SettlementBuildingType.Depot,
            new Vector3(0f, 0f, 0f), 0f, 0, null, 30f, 150f, 4),
        new("farmstead", "Farmstead", SettlementBuildingType.Farmstead,
            new Vector3(80f, 0f, 60f), 40f, 3,
            new ProductionDefinition { producedItemId = "raw_food", producedAmount = 1, productionInterval = 10f },
            20f, 150f, 2),
        new("workshop", "Workshop", SettlementBuildingType.Workshop,
            new Vector3(-70f, 0f, 50f), -15f, 3,
            new ProductionDefinition { producedItemId = "repair_kit", producedAmount = 1, productionInterval = 15f },
            20f, 150f, 2),
        new("river_depot", "River depot", SettlementBuildingType.RiverDepot,
            new Vector3(-120f, 0f, -80f), 30f, 3,
            new ProductionDefinition { producedItemId = "water", producedAmount = 2, productionInterval = 12f },
            15f, 150f, 2),
        new("watchtower", "Watchtower", SettlementBuildingType.Watchtower,
            new Vector3(0f, 0f, -150f), 10f, 2, null, 60f, 150f, 1),
        new("market", "Market", SettlementBuildingType.Market,
            new Vector3(100f, 0f, -70f), 25f, 2,
            new ProductionDefinition { producedItemId = "trade_token", producedAmount = 1, productionInterval = 20f },
            20f, 150f, 2),
        new("barracks", "Barracks", SettlementBuildingType.Barracks,
            new Vector3(-80f, 0f, -40f), -30f, 2, null, 40f, 150f, 3),
        new("greenhouse", "Greenhouse", SettlementBuildingType.Greenhouse,
            new Vector3(60f, 0f, 130f), 15f, 3,
            new ProductionDefinition { producedItemId = "herbs", producedAmount = 1, productionInterval = 8f },
            15f, 150f, 2),
    };

    private struct BuildingConfig
    {
        public string id;
        public string displayName;
        public SettlementBuildingType type;
        public Vector3 position;
        public float yRotation;
        public int repairStageCount;
        public ProductionDefinition production;
        public float territoryRadius;
        public float connectionRange;
        public int workerSlots;

        public BuildingConfig(string id, string displayName, SettlementBuildingType type,
            Vector3 position, float yRotation, int repairStageCount, ProductionDefinition production,
            float territoryRadius, float connectionRange, int workerSlots)
        {
            this.id = id;
            this.displayName = displayName;
            this.type = type;
            this.position = position;
            this.yRotation = yRotation;
            this.repairStageCount = repairStageCount;
            this.production = production;
            this.territoryRadius = territoryRadius;
            this.connectionRange = connectionRange;
            this.workerSlots = workerSlots;
        }
    }

    private void Start()
    {
        DestroySceneCameras();
        EnsureGround();
        CreateManager();

        if (_autoCreateBuildings && (_buildings == null || _buildings.Count == 0))
            CreateDefaultBuildings();
        else
            WireBuildings();

        CreateInspectUI();
        AdjustFog();
        SpawnExplorer();
        LogControlHints();

        Debug.Log("homeworld playtest: setup complete");
    }

    // ========== ground fallback ==========

    private void EnsureGround()
    {
        if (Terrain.activeTerrain != null) return;

        // no terrain in scene — create a flat ground plane so the explorer has something to stand on
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "GroundPlane_Fallback";
        ground.layer = PhysicsLayers.Terrain;
        ground.isStatic = true;
        ground.transform.position = new Vector3(0f, -0.25f, 0f);
        ground.transform.localScale = new Vector3(1200f, 0.5f, 1200f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.35f, 0.25f, 0.18f);
        mat.SetFloat("_Smoothness", 0.1f);
        ground.GetComponent<Renderer>().material = mat;

        Debug.Log("homeworld playtest: no terrain found, created 1200x1200 fallback ground");
    }

    // ========== camera cleanup ==========

    private void DestroySceneCameras()
    {
        // known seam bug: scene cameras override the bootstrapper's player camera
        foreach (var cam in FindObjectsOfType<Camera>())
        {
            if (cam.gameObject != gameObject)
                Destroy(cam.gameObject);
        }
        foreach (var listener in FindObjectsOfType<AudioListener>())
        {
            if (listener.gameObject != gameObject)
                Destroy(listener.gameObject);
        }
    }

    // ========== manager ==========

    private void CreateManager()
    {
        var managerObj = new GameObject("SettlementManager");
        _manager = managerObj.AddComponent<SettlementManagerBehaviour>();
        Debug.Log("homeworld playtest: manager created");
    }

    // ========== wire pre-placed buildings ==========

    private void WireBuildings()
    {
        foreach (var bp in _buildings)
        {
            if (bp.sceneObject == null)
            {
                Debug.LogWarning($"homeworld playtest: null scene object for {bp.buildingId}, skipping");
                continue;
            }

            var def = CreateDefinition(bp.buildingId, bp.displayName, bp.buildingType,
                bp.repairStageCount, bp.territoryRadius, bp.connectionRange, bp.workerSlots,
                bp.producedItemId, bp.producedAmount, bp.productionInterval);

            var pos = bp.sceneObject.transform.position;
            var building = _manager.RegisterBuilding(def, pos);
            if (building == null) continue;

            SubscribeEvents(building, bp.displayName);
            AttachBehaviour(bp.sceneObject, def, building);

            Debug.Log($"homeworld playtest: wired {bp.displayName} at {pos}");
        }
    }

    // ========== auto-create buildings ==========

    private void CreateDefaultBuildings()
    {
        var terrain = Terrain.activeTerrain;

        foreach (var cfg in DefaultBuildings)
        {
            var pos = cfg.position;

            // sample terrain height so buildings sit on the ground
            if (terrain != null)
                pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;

            var def = CreateDefinition(cfg.id, cfg.displayName, cfg.type,
                cfg.repairStageCount, cfg.territoryRadius, cfg.connectionRange, cfg.workerSlots,
                cfg.production?.producedItemId, cfg.production?.producedAmount ?? 0,
                cfg.production?.productionInterval ?? 0f);

            var building = _manager.RegisterBuilding(def, pos);
            if (building == null) continue;

            SubscribeEvents(building, cfg.displayName);

            // create visual
            var visualObj = new GameObject($"Building_{cfg.id}");
            visualObj.transform.position = pos;
            visualObj.transform.rotation = Quaternion.Euler(0f, cfg.yRotation, 0f);

            GameObject shell = null;
            float labelHeight = 5.5f;

#if UNITY_EDITOR
            if (ModelPaths.TryGetValue(cfg.id, out var modelPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (prefab != null)
                {
                    shell = Instantiate(prefab, visualObj.transform);
                    shell.name = "Model";
                    shell.transform.localPosition = Vector3.zero;
                    foreach (var r in shell.GetComponentsInChildren<Renderer>())
                        r.gameObject.layer = PhysicsLayers.Structures;
                    foreach (var col in shell.GetComponentsInChildren<Collider>())
                        Destroy(col);
                    var bounds = new Bounds(shell.transform.position, Vector3.zero);
                    foreach (var r in shell.GetComponentsInChildren<Renderer>())
                        bounds.Encapsulate(r.bounds);
                    labelHeight = bounds.max.y - pos.y + 2f;
                    Debug.Log($"homeworld playtest: loaded model for {cfg.id}");
                }
            }
#endif
            if (shell == null)
            {
                // placeholder cube for buildings without FBX
                shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shell.name = "Placeholder";
                shell.transform.SetParent(visualObj.transform);
                shell.transform.localPosition = new Vector3(0f, 3f, 0f);
                shell.transform.localScale = new Vector3(8f, 6f, 8f);
                shell.layer = PhysicsLayers.Structures;

                var shellMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                shellMat.color = new Color(0.4f, 0.35f, 0.25f);
                shellMat.SetFloat("_Smoothness", 0.15f);
                shell.GetComponent<Renderer>().material = shellMat;

                var shellCollider = shell.GetComponent<Collider>();
                if (shellCollider != null) Destroy(shellCollider);
            }

            // text label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(visualObj.transform);
            labelObj.transform.localPosition = new Vector3(0f, labelHeight, 0f);
            var textMesh = labelObj.AddComponent<TextMesh>();
            textMesh.text = cfg.displayName;
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.3f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;

            // interaction trigger
            var sphereCol = visualObj.AddComponent<SphereCollider>();
            sphereCol.radius = 15f;
            sphereCol.isTrigger = true;
            var rb = visualObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            visualObj.layer = PhysicsLayers.Interactable;

            AttachBehaviour(visualObj, def, building);

            Debug.Log($"homeworld playtest: placed {cfg.displayName} at {pos}");
        }
    }

    // ========== shared helpers ==========

    private SettlementBuildingDefinitionSO CreateDefinition(string id, string displayName,
        SettlementBuildingType type, int repairStageCount, float territoryRadius,
        float connectionRange, int workerSlots,
        string producedItemId, int producedAmount, float productionInterval)
    {
        var def = ScriptableObject.CreateInstance<SettlementBuildingDefinitionSO>();
        def.buildingId = id;
        def.displayName = displayName;
        def.buildingType = type;
        def.territoryRadius = territoryRadius;
        def.connectionRange = connectionRange;
        def.workerSlots = workerSlots;
        def.workerBonusPerSlot = 0.25f;

        if (!string.IsNullOrEmpty(producedItemId) && producedAmount > 0)
        {
            def.production = new ProductionDefinition
            {
                producedItemId = producedItemId,
                producedAmount = producedAmount,
                productionInterval = productionInterval
            };
        }

        var stages = new RepairStageDefinition[repairStageCount];
        for (int i = 0; i < repairStageCount; i++)
        {
            stages[i] = new RepairStageDefinition
            {
                requiredItemIds = new[] { "scrap_metal" },
                requiredAmounts = new[] { 5 * (i + 1) },
                addedPiecePrefabs = null
            };
        }
        def.repairStages = stages;
        def.upgradeTiers = new UpgradeTierDefinition[0];

        _runtimeSOs.Add(def);
        return def;
    }

    private void SubscribeEvents(SettlementBuilding building, string displayName)
    {
        building.OnRepaired += (bId, level) =>
            Debug.Log($"homeworld playtest: {displayName} repaired to level {level}");
        building.OnClaimed += bId =>
            Debug.Log($"homeworld playtest: {displayName} claimed");
        building.OnProduced += (bId, itemId, amount) =>
            Debug.Log($"homeworld playtest: {displayName} produced {amount}x {itemId}");
    }

    private void AttachBehaviour(GameObject obj, SettlementBuildingDefinitionSO def,
        SettlementBuilding building)
    {
        var behaviour = obj.AddComponent<SettlementBuildingBehaviour>();
        var defField = typeof(SettlementBuildingBehaviour).GetField("_definition",
            BindingFlags.NonPublic | BindingFlags.Instance);
        defField?.SetValue(behaviour, def);
        behaviour.Initialize(building);
        _buildingBehaviours.Add(behaviour);
    }

    // ========== inspect UI ==========

    private void CreateInspectUI()
    {
        _inspectUIObj = new GameObject("SettlementInspectUI");
        _inspectUIObj.AddComponent<SettlementInspectUI>();
        Debug.Log("homeworld playtest: inspect UI created");
    }

    // ========== fog ==========

    private void AdjustFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.35f, 0.28f, 0.2f);
        RenderSettings.fogStartDistance = 200f;
        RenderSettings.fogEndDistance = 800f;
        Debug.Log("homeworld playtest: fog adjusted for 1200m terrain (200-800m)");
    }

    // ========== explorer ==========

    private void SpawnExplorer()
    {
        var explorerObj = new GameObject("TerrainExplorer");

        // sample terrain height at center
        var terrain = Terrain.activeTerrain;
        float spawnY = 2f;
        if (terrain != null)
            spawnY = terrain.SampleHeight(Vector3.zero) + terrain.transform.position.y + 2f;

        explorerObj.transform.position = new Vector3(0f, spawnY, 0f);
        explorerObj.AddComponent<TerrainExplorer>();

        Debug.Log($"homeworld playtest: explorer spawned at (0, {spawnY:F1}, 0)");
    }

    // ========== update ==========

    private void Update()
    {
        HandleDebugKeys();
    }

    private void HandleDebugKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.tKey.wasPressedThisFrame)
            ToggleTerritorySpheres();

        if (kb.mKey.wasPressedThisFrame)
            LogSettlementState();

        if (kb.eKey.wasPressedThisFrame)
            InteractWithNearest();

        if (kb.rKey.wasPressedThisFrame)
            RepairNearest();

        if (kb.uKey.wasPressedThisFrame)
            UpgradeNearest();

        if (kb.nKey.wasPressedThisFrame)
            BuildRoadNearest();
    }

    // ========== debug actions ==========

    private SettlementBuildingBehaviour FindNearest()
    {
        var explorer = FindObjectOfType<TerrainExplorer>();
        if (explorer == null || _buildingBehaviours.Count == 0) return null;

        var pos = explorer.transform.position;
        SettlementBuildingBehaviour nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var b in _buildingBehaviours)
        {
            if (b == null) continue;
            float dist = Vector3.Distance(pos, b.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = b;
            }
        }
        return nearest;
    }

    private SettlementBuildingBehaviour FindSecondNearest()
    {
        if (_buildingBehaviours.Count < 2) return null;

        var nearest = FindNearest();
        var explorer = FindObjectOfType<TerrainExplorer>();
        if (explorer == null) return null;

        var pos = explorer.transform.position;
        SettlementBuildingBehaviour secondNearest = null;
        float secondDist = float.MaxValue;

        foreach (var b in _buildingBehaviours)
        {
            if (b == null || b == nearest) continue;
            float dist = Vector3.Distance(pos, b.transform.position);
            if (dist < secondDist)
            {
                secondDist = dist;
                secondNearest = b;
            }
        }
        return secondNearest;
    }

    private void InteractWithNearest()
    {
        var nearest = FindNearest();
        if (nearest == null) return;
        var explorer = FindObjectOfType<TerrainExplorer>();
        if (explorer != null)
            nearest.Interact(explorer.gameObject);
        Debug.Log($"homeworld playtest: interacted with {nearest.Definition.displayName}");
    }

    private void RepairNearest()
    {
        var nearest = FindNearest();
        if (nearest == null || nearest.Simulation == null) return;

        if (nearest.Simulation.IsClaimed)
        {
            Debug.Log($"homeworld playtest: {nearest.Definition.displayName} already claimed");
            return;
        }

        nearest.Simulation.AdvanceRepair();
        Debug.Log($"homeworld playtest: repaired {nearest.Definition.displayName} to level {nearest.Simulation.RepairLevel}/{nearest.Definition.MaxRepairLevel}");
    }

    private void UpgradeNearest()
    {
        var nearest = FindNearest();
        if (nearest == null || nearest.Simulation == null) return;

        if (!nearest.Simulation.IsClaimed)
        {
            Debug.Log($"homeworld playtest: {nearest.Definition.displayName} not claimed, repair first");
            return;
        }

        bool result = nearest.Simulation.AdvanceUpgrade();
        if (result)
            Debug.Log($"homeworld playtest: upgraded {nearest.Definition.displayName} to tier {nearest.Simulation.UpgradeTier}");
        else
            Debug.Log($"homeworld playtest: {nearest.Definition.displayName} at max tier");
    }

    private void BuildRoadNearest()
    {
        var a = FindNearest();
        var b = FindSecondNearest();
        if (a == null || b == null) return;

        string idA = a.Definition.buildingId;
        string idB = b.Definition.buildingId;
        bool result = _manager.BuildRoad(idA, idB);

        if (result)
        {
            var roadObj = new GameObject($"Road_{idA}_{idB}");
            var roadBehaviour = roadObj.AddComponent<SettlementRoadBehaviour>();
            roadBehaviour.Initialize(idA, idB, a.transform.position, b.transform.position);
            Debug.Log($"homeworld playtest: road built {idA} <-> {idB}");
        }
        else
        {
            Debug.Log($"homeworld playtest: road failed {idA} <-> {idB}");
        }
    }

    // ========== territory debug spheres ==========

    private void ToggleTerritorySpheres()
    {
        _territoryVisible = !_territoryVisible;

        if (_territoryVisible)
        {
            CreateTerritorySpheres();
            Debug.Log("homeworld playtest: territory spheres shown");
        }
        else
        {
            DestroyTerritorySpheres();
            Debug.Log("homeworld playtest: territory spheres hidden");
        }
    }

    private void CreateTerritorySpheres()
    {
        DestroyTerritorySpheres();

        foreach (var b in _buildingBehaviours)
        {
            if (b == null || b.Simulation == null) continue;
            float radius = b.Simulation.EffectiveTerritoryRadius;
            if (radius <= 0f) continue;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"Territory_{b.Definition.buildingId}";
            sphere.transform.position = b.transform.position + Vector3.up * 0.5f;
            sphere.transform.localScale = Vector3.one * radius * 2f;

            var col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.color = new Color(0.2f, 0.8f, 0.2f, 0.15f);
            sphere.GetComponent<Renderer>().material = mat;

            _territorySpheres.Add(sphere);
        }
    }

    private void DestroyTerritorySpheres()
    {
        foreach (var sphere in _territorySpheres)
        {
            if (sphere != null) Destroy(sphere);
        }
        _territorySpheres.Clear();
    }

    // ========== settlement state log ==========

    private void LogSettlementState()
    {
        if (_manager == null || _manager.Graph == null)
        {
            Debug.Log("homeworld playtest: no manager or graph");
            return;
        }

        var graph = _manager.Graph;
        Debug.Log("=== settlement state ===");

        foreach (var kvp in graph.AllBuildings)
        {
            var b = kvp.Value;
            bool factoryConn = graph.HasFactoryConnection(b.BuildingId);
            Debug.Log($"  {b.Definition.displayName}: repair={b.RepairLevel}/{b.Definition.MaxRepairLevel} " +
                      $"claimed={b.IsClaimed} tier={b.UpgradeTier} workers={b.WorkerCount}/{b.MaxWorkerSlots} " +
                      $"territory={b.EffectiveTerritoryRadius:F0}m factory={factoryConn}");
        }

        var roads = graph.Roads;
        Debug.Log($"  roads: {roads.Count}");
        foreach (var (a, b) in roads)
            Debug.Log($"    {a} <-> {b}");

        Debug.Log("=== end settlement state ===");
    }

    // ========== OnGUI ==========

    private void OnGUI()
    {
        if (_manager == null || _manager.Graph == null) return;

        float x = 10f;
        float y = 10f;
        float w = 500f;
        float h = 20f;

        GUI.color = Color.white;
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };

        GUI.Label(new Rect(x, y, w, h), "HomeWorld playtest (1200m terrain)", style);
        y += h + 4;

        style.fontStyle = FontStyle.Normal;
        style.fontSize = 12;

        var graph = _manager.Graph;

        foreach (var kvp in graph.AllBuildings)
        {
            var b = kvp.Value;
            bool factoryConn = graph.HasFactoryConnection(b.BuildingId);
            string status = b.IsClaimed ? "claimed" : $"repair {b.RepairLevel}/{b.Definition.MaxRepairLevel}";
            string territory = b.EffectiveTerritoryRadius > 0 ? $"{b.EffectiveTerritoryRadius:F0}m" : "none";
            string connStr = factoryConn ? "yes" : "no";

            GUI.Label(new Rect(x, y, w, h),
                $"{b.Definition.displayName}: {status} | tier {b.UpgradeTier} | workers {b.WorkerCount}/{b.MaxWorkerSlots} | territory {territory} | hub {connStr}",
                style);
            y += h;
        }

        y += 4;
        GUI.Label(new Rect(x, y, w, h), $"Roads: {graph.Roads.Count}", style);
        y += h + 8;

        style.fontSize = 11;
        GUI.color = new Color(0.8f, 0.8f, 0.6f);
        GUI.Label(new Rect(x, y, w, h), "E=interact  R=repair  U=upgrade  N=road  T=territory  M=log state", style);
    }

    // ========== helpers ==========

    private void LogControlHints()
    {
        Debug.Log("homeworld playtest controls:");
        Debug.Log("  WASD=move, Mouse=look, Shift=sprint, Ctrl+Shift=super-sprint");
        Debug.Log("  E=interact, R=repair, U=upgrade, N=road, T=territory, M=log state");
    }

    // ========== editor: auto-wire scene buildings ==========

    // Maps FBX asset name (as it appears when dragged into scene) to building config.
    // Matches both exact name and "(Clone)" suffix from instantiation.
    private static readonly Dictionary<string, (string id, string displayName, SettlementBuildingType type,
        int repairStages, float territory, float connRange, int workers,
        string prodItem, int prodAmount, float prodInterval)> ModelNameToConfig = new()
    {
        { "FactoryYard_ConcreteWarehouse", ("factory_yard", "Factory yard", SettlementBuildingType.Depot,
            0, 30f, 150f, 4, null, 0, 0f) },
        { "Farmstead_StoneRuin", ("farmstead", "Farmstead", SettlementBuildingType.Farmstead,
            3, 20f, 150f, 2, "raw_food", 1, 10f) },
        { "Workshop_BrickBuilding", ("workshop", "Workshop", SettlementBuildingType.Workshop,
            3, 20f, 150f, 2, "repair_kit", 1, 15f) },
        { "Watchtower_RuinTower", ("watchtower", "Watchtower", SettlementBuildingType.Watchtower,
            2, 60f, 150f, 1, null, 0, 0f) },
        { "Market_TropicalShop", ("market", "Market", SettlementBuildingType.Market,
            2, 20f, 150f, 2, "trade_token", 1, 20f) },
        { "Barracks_ConcreteBuilding", ("barracks", "Barracks", SettlementBuildingType.Barracks,
            2, 40f, 150f, 3, null, 0, 0f) },
    };

#if UNITY_EDITOR
    /// <summary>
    /// Right-click the component > "Auto-wire scene buildings" to scan the scene
    /// for settlement FBX models and populate the Buildings list automatically.
    /// </summary>
    [ContextMenu("Auto-wire scene buildings")]
    private void AutoWireSceneBuildings()
    {
        _buildings.Clear();
        _autoCreateBuildings = false;

        var roots = gameObject.scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string name = t.gameObject.name;
                // strip (Clone) suffix if present
                string cleanName = name.Replace("(Clone)", "").Trim();

                if (!ModelNameToConfig.TryGetValue(cleanName, out var cfg)) continue;

                var bp = new BuildingPlacement
                {
                    sceneObject = t.gameObject,
                    buildingId = cfg.id,
                    displayName = cfg.displayName,
                    buildingType = cfg.type,
                    repairStageCount = cfg.repairStages,
                    territoryRadius = cfg.territory,
                    connectionRange = cfg.connRange,
                    workerSlots = cfg.workers,
                    producedItemId = cfg.prodItem ?? "",
                    producedAmount = cfg.prodAmount,
                    productionInterval = cfg.prodInterval,
                };

                _buildings.Add(bp);
                Debug.Log($"homeworld playtest: auto-wired {cfg.displayName} from scene object '{name}'");
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"homeworld playtest: auto-wired {_buildings.Count} buildings. Save the scene to persist.");
    }
#endif

    // ========== cleanup ==========

    private void OnDestroy()
    {
        DestroyTerritorySpheres();

        foreach (var so in _runtimeSOs)
        {
            if (so != null) DestroyImmediate(so);
        }
        _runtimeSOs.Clear();
    }
}
