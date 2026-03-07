using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Self-contained settlement playtest bootstrapper. Drop on a GameObject, hit Play.
/// Creates terrain, 6 buildings, settlement manager, inspect UI, lighting, and an
/// explorer character. Debug keys exercise every feature without real inventory items.
///
/// Controls:
///   WASD - Move, Mouse - Look
///   E - Interact with nearest building
///   T - Toggle territory debug spheres
///   M - Log full settlement state to console
///   R - Repair nearest building (debug shortcut)
///   U - Upgrade nearest building (debug shortcut)
///   N - Build road between two nearest unclaimed buildings (debug shortcut)
/// </summary>
public class SettlementPlaytestSetup : MonoBehaviour
{
    // -- runtime SOs to clean up --
    private readonly List<ScriptableObject> _runtimeSOs = new();

    // -- spawned objects --
    private GameObject _groundPlane;
    private GameObject _explorer;
    private GameObject _lightObj;
    private GameObject _inspectUIObj;
    private SettlementManagerBehaviour _manager;

    // -- building tracking --
    private readonly List<SettlementBuildingBehaviour> _buildingBehaviours = new();
    private readonly List<GameObject> _buildingVisuals = new();

    // -- territory debug --
    private bool _territoryVisible;
    private readonly List<GameObject> _territorySpheres = new();

    // -- camera look --
    private float _yaw;
    private float _pitch;
    private const float LookSensitivity = 2f;
    private const float MoveSpeed = 12f;

    private void Start()
    {
        CreateGround();
        CreateManager();
        CreateBuildings();
        CreateInspectUI();
        CreateLighting();
        SpawnExplorer();
        LogControlHints();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("settlement playtest: setup complete");
    }

    // ========== ground ==========

    private void CreateGround()
    {
        _groundPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _groundPlane.name = "GroundPlane";
        _groundPlane.layer = PhysicsLayers.Terrain;
        _groundPlane.isStatic = true;
        _groundPlane.transform.position = new Vector3(0f, -0.25f, 0f);
        _groundPlane.transform.localScale = new Vector3(600f, 0.5f, 600f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.35f, 0.25f, 0.18f);
        mat.SetFloat("_Smoothness", 0.1f);
        _groundPlane.GetComponent<Renderer>().material = mat;

        Debug.Log("settlement playtest: ground plane created (600x600)");
    }

    // ========== manager ==========

    private void CreateManager()
    {
        var managerObj = new GameObject("SettlementManager");
        _manager = managerObj.AddComponent<SettlementManagerBehaviour>();
        Debug.Log("settlement playtest: manager created");
    }

    // ========== buildings ==========

    private void CreateBuildings()
    {
        // factory yard: hub, 0 repair stages, pre-claimed
        CreateBuilding("factory_yard", "Factory yard", SettlementBuildingType.Depot,
            Vector3.zero, 0, null, 30f, 200f, 4);

        // farmstead: 3 repair stages, produces raw_food
        CreateBuilding("farmstead", "Farmstead", SettlementBuildingType.Farmstead,
            new Vector3(50f, 0f, 100f), 3,
            new ProductionDefinition { producedItemId = "raw_food", producedAmount = 1, productionInterval = 10f },
            20f, 100f, 2);

        // watchtower: 2 repair stages, 60m territory, no production
        CreateBuilding("watchtower", "Watchtower", SettlementBuildingType.Watchtower,
            new Vector3(0f, 0f, -80f), 2, null, 60f, 100f, 1);

        // workshop: 3 repair stages, produces repair_kit
        CreateBuilding("workshop", "Workshop", SettlementBuildingType.Workshop,
            new Vector3(100f, 0f, 30f), 3,
            new ProductionDefinition { producedItemId = "repair_kit", producedAmount = 1, productionInterval = 15f },
            20f, 100f, 2);

        // market: 2 repair stages, produces trade_token
        CreateBuilding("market", "Market", SettlementBuildingType.Market,
            new Vector3(-100f, 0f, 0f), 2,
            new ProductionDefinition { producedItemId = "trade_token", producedAmount = 1, productionInterval = 20f },
            20f, 100f, 2);

        // barracks: 2 repair stages, 40m territory, no production
        CreateBuilding("barracks", "Barracks", SettlementBuildingType.Barracks,
            new Vector3(80f, 0f, 90f), 2, null, 40f, 100f, 3);

        Debug.Log($"settlement playtest: {_buildingBehaviours.Count} buildings created");
    }

    private void CreateBuilding(string id, string displayName, SettlementBuildingType type,
        Vector3 position, int repairStageCount, ProductionDefinition production,
        float territoryRadius, float connectionRange, int workerSlots)
    {
        // create definition SO at runtime
        var def = ScriptableObject.CreateInstance<SettlementBuildingDefinitionSO>();
        def.buildingId = id;
        def.displayName = displayName;
        def.buildingType = type;
        def.territoryRadius = territoryRadius;
        def.connectionRange = connectionRange;
        def.workerSlots = workerSlots;
        def.workerBonusPerSlot = 0.25f;
        def.production = production;

        // repair stages with placeholder requirements
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

        // register with manager
        var building = _manager.RegisterBuilding(def, position);
        if (building == null)
        {
            Debug.LogWarning($"settlement playtest: failed to register {id}");
            return;
        }

        // subscribe to events for logging
        building.OnRepaired += (bId, level) =>
            Debug.Log($"settlement playtest: {displayName} repaired to level {level}");
        building.OnClaimed += bId =>
            Debug.Log($"settlement playtest: {displayName} claimed");
        building.OnProduced += (bId, itemId, amount) =>
            Debug.Log($"settlement playtest: {displayName} produced {amount}x {itemId}");

        // create visual: cube ruin shell
        var visualObj = new GameObject($"Building_{id}");
        visualObj.transform.position = position;

        var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shell.name = "RuinShell";
        shell.transform.SetParent(visualObj.transform);
        shell.transform.localPosition = new Vector3(0f, 2f, 0f);
        shell.transform.localScale = new Vector3(6f, 4f, 6f);
        shell.layer = PhysicsLayers.Structures;

        // weathered brown material
        var shellMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        shellMat.color = new Color(0.4f, 0.3f, 0.2f);
        shellMat.SetFloat("_Smoothness", 0.15f);
        shell.GetComponent<Renderer>().material = shellMat;

        // remove shell's default collider (we add a sphere trigger on the parent)
        var shellCollider = shell.GetComponent<Collider>();
        if (shellCollider != null) Destroy(shellCollider);

        // text label above building
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(visualObj.transform);
        labelObj.transform.localPosition = new Vector3(0f, 5.5f, 0f);
        var textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = displayName;
        textMesh.fontSize = 32;
        textMesh.characterSize = 0.3f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;

        // sphere collider trigger + kinematic rigidbody for interaction
        var sphereCol = visualObj.AddComponent<SphereCollider>();
        sphereCol.radius = 5f;
        sphereCol.isTrigger = true;
        var rb = visualObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        visualObj.layer = PhysicsLayers.Interactable;

        // add SettlementBuildingBehaviour, set definition via reflection
        var behaviour = visualObj.AddComponent<SettlementBuildingBehaviour>();
        var defField = typeof(SettlementBuildingBehaviour).GetField("_definition",
            BindingFlags.NonPublic | BindingFlags.Instance);
        defField?.SetValue(behaviour, def);
        behaviour.Initialize(building);

        _buildingBehaviours.Add(behaviour);
        _buildingVisuals.Add(visualObj);

        Debug.Log($"settlement playtest: placed {displayName} at {position}");
    }

    // ========== inspect UI ==========

    private void CreateInspectUI()
    {
        _inspectUIObj = new GameObject("SettlementInspectUI");
        _inspectUIObj.AddComponent<SettlementInspectUI>();
        Debug.Log("settlement playtest: inspect UI created");
    }

    // ========== lighting ==========

    private void CreateLighting()
    {
        _lightObj = new GameObject("DirectionalLight");
        var light = _lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.85f, 0.65f);
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        _lightObj.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

        // fog
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.35f, 0.28f, 0.2f);
        RenderSettings.fogStartDistance = 50f;
        RenderSettings.fogEndDistance = 300f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.15f, 0.12f, 0.1f);

        Debug.Log("settlement playtest: lighting and fog created");
    }

    // ========== explorer ==========

    private void SpawnExplorer()
    {
        _explorer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        _explorer.name = "Explorer";
        _explorer.layer = PhysicsLayers.Player;
        _explorer.transform.position = new Vector3(0f, 1f, -5f);

        // remove default capsule collider and add character controller-friendly one
        var defaultCol = _explorer.GetComponent<Collider>();
        if (defaultCol != null) Destroy(defaultCol);
        var cc = _explorer.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;

        // camera
        var camObj = new GameObject("ExplorerCamera");
        camObj.transform.SetParent(_explorer.transform);
        camObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var cam = camObj.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 500f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = RenderSettings.fogColor;
        camObj.AddComponent<AudioListener>();

        // hide the capsule renderer so it doesn't block the camera
        var capsuleRenderer = _explorer.GetComponent<Renderer>();
        if (capsuleRenderer != null) capsuleRenderer.enabled = false;

        _yaw = _explorer.transform.eulerAngles.y;
        _pitch = 0f;

        Debug.Log("settlement playtest: explorer spawned at (0, 1, -5)");
    }

    // ========== update ==========

    private void Update()
    {
        HandleMovement();
        HandleDebugKeys();
    }

    private void HandleMovement()
    {
        if (_explorer == null) return;

        var cc = _explorer.GetComponent<CharacterController>();
        if (cc == null) return;

        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null || kb == null) return;

        // mouse look
        var mouseDelta = mouse.delta.ReadValue();
        _yaw += mouseDelta.x * LookSensitivity * 0.1f;
        _pitch -= mouseDelta.y * LookSensitivity * 0.1f;
        _pitch = Mathf.Clamp(_pitch, -80f, 80f);

        _explorer.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        var camTransform = _explorer.GetComponentInChildren<Camera>()?.transform;
        if (camTransform != null)
            camTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

        // WASD movement
        var move = Vector3.zero;
        if (kb.wKey.isPressed) move += _explorer.transform.forward;
        if (kb.sKey.isPressed) move -= _explorer.transform.forward;
        if (kb.dKey.isPressed) move += _explorer.transform.right;
        if (kb.aKey.isPressed) move -= _explorer.transform.right;
        move *= MoveSpeed;

        // gravity
        if (!cc.isGrounded)
            move.y = -9.81f;

        cc.Move(move * Time.deltaTime);
    }

    private void HandleDebugKeys()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null) return;

        // T: toggle territory debug spheres
        if (kb.tKey.wasPressedThisFrame)
            ToggleTerritorySpheres();

        // M: log full settlement state
        if (kb.mKey.wasPressedThisFrame)
            LogSettlementState();

        // E: interact with nearest building
        if (kb.eKey.wasPressedThisFrame)
            InteractWithNearest();

        // R: repair nearest building (debug)
        if (kb.rKey.wasPressedThisFrame)
            RepairNearest();

        // U: upgrade nearest building (debug)
        if (kb.uKey.wasPressedThisFrame)
            UpgradeNearest();

        // N: build road between two nearest buildings to the player
        if (kb.nKey.wasPressedThisFrame)
            BuildRoadNearest();

        // Escape: unlock cursor
        if (kb.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Left click: lock cursor
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ========== debug actions ==========

    private SettlementBuildingBehaviour FindNearest()
    {
        if (_explorer == null || _buildingBehaviours.Count == 0) return null;

        var pos = _explorer.transform.position;
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
        if (_explorer == null || _buildingBehaviours.Count < 2) return null;

        var nearest = FindNearest();
        var pos = _explorer.transform.position;
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
        nearest.Interact(_explorer);
        Debug.Log($"settlement playtest: interacted with {nearest.Definition.displayName}");
    }

    private void RepairNearest()
    {
        var nearest = FindNearest();
        if (nearest == null || nearest.Simulation == null) return;

        if (nearest.Simulation.IsClaimed)
        {
            Debug.Log($"settlement playtest: {nearest.Definition.displayName} already claimed, nothing to repair");
            return;
        }

        nearest.Simulation.AdvanceRepair();
        Debug.Log($"settlement playtest: repaired {nearest.Definition.displayName} to level {nearest.Simulation.RepairLevel}/{nearest.Definition.MaxRepairLevel}");
    }

    private void UpgradeNearest()
    {
        var nearest = FindNearest();
        if (nearest == null || nearest.Simulation == null) return;

        if (!nearest.Simulation.IsClaimed)
        {
            Debug.Log($"settlement playtest: {nearest.Definition.displayName} not yet claimed, repair first");
            return;
        }

        bool result = nearest.Simulation.AdvanceUpgrade();
        if (result)
            Debug.Log($"settlement playtest: upgraded {nearest.Definition.displayName} to tier {nearest.Simulation.UpgradeTier}");
        else
            Debug.Log($"settlement playtest: {nearest.Definition.displayName} already at max upgrade tier");
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
            // spawn road visual
            var roadObj = new GameObject($"Road_{idA}_{idB}");
            var roadBehaviour = roadObj.AddComponent<SettlementRoadBehaviour>();
            roadBehaviour.Initialize(idA, idB, a.transform.position, b.transform.position);
            Debug.Log($"settlement playtest: road built {idA} <-> {idB}");
        }
        else
        {
            Debug.Log($"settlement playtest: road failed {idA} <-> {idB} (duplicate, out of range, or missing building)");
        }
    }

    // ========== territory debug spheres ==========

    private void ToggleTerritorySpheres()
    {
        _territoryVisible = !_territoryVisible;

        if (_territoryVisible)
        {
            CreateTerritorySpheres();
            Debug.Log("settlement playtest: territory spheres shown");
        }
        else
        {
            DestroyTerritorySpheres();
            Debug.Log("settlement playtest: territory spheres hidden");
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

            // remove collider so it doesn't interfere
            var col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // transparent green material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetFloat("_Surface", 1f); // transparent
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
            Debug.Log("settlement playtest: no manager or graph");
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
        float w = 420f;
        float h = 20f;

        GUI.color = Color.white;
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };

        GUI.Label(new Rect(x, y, w, h), "Settlement playtest", style);
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
        GUI.Label(new Rect(x, y, w, h), "E=interact  R=repair  U=upgrade  N=build road  T=territory  M=log state", style);
    }

    // ========== helpers ==========

    private void LogControlHints()
    {
        Debug.Log("settlement playtest controls:");
        Debug.Log("  WASD=move, Mouse=look, Esc=unlock cursor, Click=lock cursor");
        Debug.Log("  E=interact, R=repair nearest, U=upgrade nearest, N=road between 2 nearest");
        Debug.Log("  T=toggle territory spheres, M=log settlement state");
    }

    // ========== cleanup ==========

    private void OnDestroy()
    {
        DestroyTerritorySpheres();

        foreach (var so in _runtimeSOs)
        {
            if (so != null) DestroyImmediate(so);
        }
        _runtimeSOs.Clear();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
