using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Self-contained playtest for Phase 1 factory systems.
/// Uses the plain C# classes directly (D-004) -- no MonoBehaviour wrappers needed.
/// Creates all ScriptableObject assets at runtime so no manual asset setup is required.
/// Uses New Input System (D-003).
/// </summary>
public class PlaytestManager : MonoBehaviour
{
    // -- Plain C# systems --
    private FactoryGrid _grid;
    private BuildModeController _buildMode;
    private FactorySimulation _simulation;

    // -- Runtime SO instances --
    private FoundationDefinitionSO _foundation1x1;
    private FoundationDefinitionSO _foundation2x2;
    private MachineDefinitionSO _smelterDef;
    private RecipeSO _smeltRecipe;
    private ItemDefinitionSO _ironOreDef;
    private ItemDefinitionSO _ironIngotDef;

    // -- Build selection --
    private enum BuildSelection { Foundation1x1, Foundation2x2, Smelter }
    private BuildSelection _selection = BuildSelection.Foundation1x1;
    private bool _isBuilding;
    private Vector2Int _cursorCell;
    private bool _cursorValid;
    private int _rotation;

    // -- Placed objects --
    private readonly List<PlacedEntry> _placed = new();
    private Machine _selectedMachine;
    private int _selectedMachineIndex = -1;

    // -- Ghost preview --
    private GameObject _ghost;
    private Renderer _ghostRenderer;

    // -- Simulation --
    private bool _autoTick = true;
    private bool _showHelp = true;
    private string _statusMessage = "";
    private float _statusTimer;

    // -- Belt demo --
    private BeltSegment _demoBelt;
    private readonly List<GameObject> _beltItemVisuals = new();
    private GameObject _beltLine;

    private struct PlacedEntry
    {
        public GameObject Visual;
        public BuildingData Data;
        public Machine Machine;
    }

    private void Awake()
    {
        CreateScriptableObjects();
        CreateGroundPlane();

        _grid = new FactoryGrid();
        _buildMode = new BuildModeController();
        _simulation = new FactorySimulation(LookupRecipe);
        _demoBelt = new BeltSegment(5);

        CreateGhostPreview();
        CreateBeltVisual();
    }

    private void CreateGroundPlane()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "GroundPlane";
        ground.layer = PhysicsLayers.GridPlane;
        ground.transform.position = new Vector3(100f, 0f, 100f);
        ground.transform.localScale = new Vector3(20f, 1f, 20f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.35f, 0.45f, 0.35f);
        ground.GetComponent<Renderer>().material = mat;
    }

    private void Update()
    {
        HandleInput();
        UpdateGhost();
        UpdateMachineVisuals();
        UpdateBeltVisuals();

        if (_autoTick)
        {
            _simulation.Tick(Time.deltaTime);
        }

        if (_statusTimer > 0f)
            _statusTimer -= Time.deltaTime;
    }

    // -------------------------------------------------------
    // Setup
    // -------------------------------------------------------

    private void CreateScriptableObjects()
    {
        _ironOreDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        _ironOreDef.itemId = "iron_ore";
        _ironOreDef.displayName = "Iron Ore";
        _ironOreDef.isStackable = true;
        _ironOreDef.maxStackSize = 64;

        _ironIngotDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        _ironIngotDef.itemId = "iron_ingot";
        _ironIngotDef.displayName = "Iron Ingot";
        _ironIngotDef.isStackable = true;
        _ironIngotDef.maxStackSize = 64;

        _foundation1x1 = ScriptableObject.CreateInstance<FoundationDefinitionSO>();
        _foundation1x1.foundationId = "foundation_1x1";
        _foundation1x1.displayName = "Foundation 1x1";
        _foundation1x1.size = new Vector2Int(1, 1);

        _foundation2x2 = ScriptableObject.CreateInstance<FoundationDefinitionSO>();
        _foundation2x2.foundationId = "foundation_2x2";
        _foundation2x2.displayName = "Foundation 2x2";
        _foundation2x2.size = new Vector2Int(2, 2);

        _smelterDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        _smelterDef.machineId = "smelter";
        _smelterDef.displayName = "Smelter";
        _smelterDef.size = new Vector2Int(2, 2);
        _smelterDef.machineType = "smelter";
        _smelterDef.inputBufferSize = 2;
        _smelterDef.outputBufferSize = 1;
        _smelterDef.processingSpeed = 1f;

        _smeltRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        _smeltRecipe.recipeId = "smelt_iron";
        _smeltRecipe.displayName = "Smelt Iron";
        _smeltRecipe.inputs = new[] { new RecipeIngredient { itemId = "iron_ore", count = 1 } };
        _smeltRecipe.outputs = new[] { new RecipeIngredient { itemId = "iron_ingot", count = 1 } };
        _smeltRecipe.craftDuration = 3f;
        _smeltRecipe.requiredMachineType = "smelter";
    }

    private RecipeSO LookupRecipe(string recipeId)
    {
        if (recipeId == _smeltRecipe.recipeId) return _smeltRecipe;
        return null;
    }

    private void CreateGhostPreview()
    {
        _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _ghost.name = "BuildGhost";
        _ghost.layer = 2; // Ignore Raycast
        Destroy(_ghost.GetComponent<Collider>());

        _ghostRenderer = _ghost.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.color = new Color(0f, 1f, 0f, 0.4f);
        _ghostRenderer.material = mat;

        _ghost.SetActive(false);
    }

    private void CreateBeltVisual()
    {
        _beltLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _beltLine.name = "BeltSegment";
        _beltLine.layer = 2;
        Destroy(_beltLine.GetComponent<Collider>());
        _beltLine.transform.position = new Vector3(7.5f, 0.05f, 0.5f);
        _beltLine.transform.localScale = new Vector3(5f, 0.1f, 0.6f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.3f, 0.3f, 0.3f);
        _beltLine.GetComponent<Renderer>().material = mat;
    }

    // -------------------------------------------------------
    // Input (New Input System)
    // -------------------------------------------------------

    private void HandleInput()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // Toggle help
        if (kb.hKey.wasPressedThisFrame)
            _showHelp = !_showHelp;

        // Building selection
        if (kb.digit1Key.wasPressedThisFrame)
        {
            _selection = BuildSelection.Foundation1x1;
            SetStatus("Selected: Foundation 1x1");
        }
        if (kb.digit2Key.wasPressedThisFrame)
        {
            _selection = BuildSelection.Foundation2x2;
            SetStatus("Selected: Foundation 2x2");
        }
        if (kb.digit3Key.wasPressedThisFrame)
        {
            _selection = BuildSelection.Smelter;
            SetStatus("Selected: Smelter");
        }

        // Build mode toggle
        if (kb.bKey.wasPressedThisFrame)
        {
            _isBuilding = !_isBuilding;
            _ghost.SetActive(_isBuilding);
            _rotation = 0;
            SetStatus(_isBuilding ? "Build mode ON" : "Build mode OFF");
        }

        if (!_isBuilding)
        {
            HandleNonBuildInput(kb, mouse);
            return;
        }

        // Build mode input
        if (kb.escapeKey.wasPressedThisFrame)
        {
            _isBuilding = false;
            _ghost.SetActive(false);
            SetStatus("Build mode OFF");
            return;
        }

        if (kb.rKey.wasPressedThisFrame)
        {
            _rotation = (_rotation + 90) % 360;
            SetStatus($"Rotation: {_rotation}");
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            TryPlace();
        }

        UpdateCursor(mouse);
    }

    private void HandleNonBuildInput(Keyboard kb, Mouse mouse)
    {
        // Select machine by clicking
        if (mouse.leftButton.wasPressedThisFrame)
        {
            TrySelectMachine(mouse);
        }

        // Feed iron ore into selected machine
        if (kb.fKey.wasPressedThisFrame && _selectedMachine != null)
        {
            var ore = ItemInstance.Create("iron_ore");
            if (_selectedMachine.TryInsertInput(0, ore, 1))
                SetStatus("Fed 1 iron ore into smelter");
            else if (_selectedMachine.TryInsertInput(1, ore, 1))
                SetStatus("Fed 1 iron ore into smelter (slot 2)");
            else
                SetStatus("Smelter input full");
        }

        // Extract output from selected machine
        if (kb.gKey.wasPressedThisFrame && _selectedMachine != null)
        {
            var output = _selectedMachine.ExtractOutput(0, 1);
            if (!output.IsEmpty)
                SetStatus($"Extracted 1 {output.item.definitionId}");
            else
                SetStatus("Output empty");
        }

        // Manual tick
        if (kb.tKey.wasPressedThisFrame)
        {
            _simulation.Tick(Time.fixedDeltaTime);
            SetStatus("Manual tick");
        }

        // Toggle auto-tick
        if (kb.yKey.wasPressedThisFrame)
        {
            _autoTick = !_autoTick;
            SetStatus($"Auto-tick: {(_autoTick ? "ON" : "OFF")}");
        }

        // Belt demo: insert item
        if (kb.jKey.wasPressedThisFrame)
        {
            if (_demoBelt.TryInsertAtStart("iron_ore", 30))
                SetStatus("Inserted item onto belt");
            else
                SetStatus("Belt input blocked (min spacing)");
        }

        // Belt demo: extract from end
        if (kb.kKey.wasPressedThisFrame)
        {
            var extracted = _demoBelt.TryExtractFromEnd();
            if (extracted != null)
                SetStatus($"Extracted {extracted} from belt end");
            else
                SetStatus("Belt end empty (item not at output yet)");
        }

        // Belt demo: tick
        if (kb.lKey.wasPressedThisFrame)
        {
            _demoBelt.Tick(20);
            SetStatus($"Belt tick +20 subdivisions, terminal gap: {_demoBelt.TerminalGap}");
        }
    }

    // -------------------------------------------------------
    // Placement
    // -------------------------------------------------------

    private void UpdateCursor(Mouse mouse)
    {
        var cam = Camera.main;
        if (cam == null) return;

        var mousePos = mouse.position.ReadValue();
        var ray = cam.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
        if (Physics.Raycast(ray, out var hit, 500f, PhysicsLayers.PlacementMask))
        {
            _cursorCell = _grid.WorldToCell(hit.point);
            _cursorValid = _grid.CanPlace(_cursorCell, GetEffectiveSize());
        }
        else
        {
            _cursorValid = false;
        }
    }

    private Vector2Int GetCurrentSize()
    {
        return _selection switch
        {
            BuildSelection.Foundation1x1 => _foundation1x1.size,
            BuildSelection.Foundation2x2 => _foundation2x2.size,
            BuildSelection.Smelter => _smelterDef.size,
            _ => Vector2Int.one
        };
    }

    private Vector2Int GetEffectiveSize()
    {
        var size = GetCurrentSize();
        bool swapped = _rotation == 90 || _rotation == 270;
        return swapped ? new Vector2Int(size.y, size.x) : size;
    }

    private void TryPlace()
    {
        if (!_cursorValid) return;

        var effectiveSize = GetEffectiveSize();
        if (!_grid.CanPlace(_cursorCell, effectiveSize)) return;

        string buildingId = _selection switch
        {
            BuildSelection.Foundation1x1 => _foundation1x1.foundationId,
            BuildSelection.Foundation2x2 => _foundation2x2.foundationId,
            BuildSelection.Smelter => _smelterDef.machineId,
            _ => "unknown"
        };

        var data = new BuildingData(buildingId, _cursorCell, effectiveSize, _rotation);
        _grid.Place(_cursorCell, effectiveSize, data);

        // Create visual
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = $"{buildingId}_{_placed.Count}";
        visual.layer = 2;

        var worldPos = _grid.CellToWorld(_cursorCell);
        float ox = (effectiveSize.x - 1) * 0.5f;
        float oz = (effectiveSize.y - 1) * 0.5f;
        visual.transform.position = new Vector3(worldPos.x + ox, 0.5f, worldPos.z + oz);
        visual.transform.localScale = new Vector3(effectiveSize.x * 0.9f, 1f, effectiveSize.y * 0.9f);

        Machine machine = null;
        Color color;

        if (_selection == BuildSelection.Smelter)
        {
            machine = new Machine(_smelterDef);
            machine.SetRecipe(_smeltRecipe.recipeId);
            _simulation.RegisterMachine(machine);
            color = new Color(0.8f, 0.5f, 0.1f);
            SetStatus("Placed smelter (select it and press F to feed ore)");
        }
        else
        {
            color = new Color(0.3f, 0.5f, 0.8f);
            SetStatus($"Placed {buildingId}");
        }

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        visual.GetComponent<Renderer>().material = mat;

        _placed.Add(new PlacedEntry { Visual = visual, Data = data, Machine = machine });
    }

    private void TrySelectMachine(Mouse mouse)
    {
        var cam = Camera.main;
        if (cam == null) return;

        var mousePos = mouse.position.ReadValue();
        var ray = cam.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
        if (!Physics.Raycast(ray, out var hit, 500f)) return;

        var cell = _grid.WorldToCell(hit.point);
        var buildingData = _grid.GetAt(cell);
        if (buildingData == null) return;

        for (int i = 0; i < _placed.Count; i++)
        {
            if (_placed[i].Data == buildingData && _placed[i].Machine != null)
            {
                _selectedMachine = _placed[i].Machine;
                _selectedMachineIndex = i;
                SetStatus($"Selected: {_placed[i].Data.BuildingId}");
                return;
            }
        }

        SetStatus("That's not a machine");
    }

    // -------------------------------------------------------
    // Visuals
    // -------------------------------------------------------

    private void UpdateGhost()
    {
        if (!_isBuilding || !_ghost.activeSelf) return;

        var effectiveSize = GetEffectiveSize();
        var worldPos = _grid.CellToWorld(_cursorCell);
        float ox = (effectiveSize.x - 1) * 0.5f;
        float oz = (effectiveSize.y - 1) * 0.5f;

        _ghost.transform.position = new Vector3(worldPos.x + ox, 0.5f, worldPos.z + oz);
        _ghost.transform.localScale = new Vector3(effectiveSize.x * 0.95f, 0.95f, effectiveSize.y * 0.95f);
        _ghost.transform.rotation = Quaternion.Euler(0f, _rotation, 0f);

        _ghostRenderer.material.color = _cursorValid
            ? new Color(0f, 1f, 0f, 0.4f)
            : new Color(1f, 0f, 0f, 0.4f);
    }

    private void UpdateMachineVisuals()
    {
        for (int i = 0; i < _placed.Count; i++)
        {
            var entry = _placed[i];
            if (entry.Machine == null) continue;

            Color color = entry.Machine.Status switch
            {
                MachineStatus.Idle => new Color(0.8f, 0.5f, 0.1f),
                MachineStatus.Working => new Color(1f, 0.9f, 0.1f),
                MachineStatus.Blocked => new Color(0.9f, 0.1f, 0.1f),
                _ => Color.gray
            };

            if (i == _selectedMachineIndex)
            {
                color = Color.Lerp(color, Color.white, 0.3f);
            }

            var renderer = entry.Visual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;
        }
    }

    private void UpdateBeltVisuals()
    {
        foreach (var v in _beltItemVisuals)
        {
            if (v != null) Destroy(v);
        }
        _beltItemVisuals.Clear();

        var items = _demoBelt.GetItems();
        if (items.Count == 0) return;

        float beltStartX = 5f;
        float beltEndX = 10f;
        float beltTotalLength = _demoBelt.TotalLength;

        int accumulatedDist = 0;
        for (int i = 0; i < items.Count; i++)
        {
            accumulatedDist += items[i].distanceToNext;
            float t = (float)accumulatedDist / beltTotalLength;
            float worldX = Mathf.Lerp(beltStartX, beltEndX, t);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"BeltItem_{i}";
            cube.layer = 2;
            Destroy(cube.GetComponent<Collider>());
            cube.transform.position = new Vector3(worldX, 0.35f, 0.5f);
            cube.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.6f, 0.3f, 0.1f);
            cube.GetComponent<Renderer>().material = mat;

            _beltItemVisuals.Add(cube);
        }
    }

    // -------------------------------------------------------
    // HUD
    // -------------------------------------------------------

    private void SetStatus(string msg)
    {
        _statusMessage = msg;
        _statusTimer = 4f;
    }

    private void OnGUI()
    {
        if (_statusTimer > 0f)
        {
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Box(new Rect(Screen.width / 2 - 250, 10, 500, 35), _statusMessage, style);
        }

        var modeStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
        string mode = _isBuilding ? $"BUILD MODE [{_selection}] R={_rotation}" : "NORMAL MODE";
        GUI.Label(new Rect(10, 10, 400, 25), mode, modeStyle);
        GUI.Label(new Rect(10, 30, 400, 25), $"Auto-tick: {(_autoTick ? "ON" : "OFF")} | Machines: {_simulation.MachineCount}", modeStyle);

        if (_selectedMachine != null)
        {
            float y = 55;
            var infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.yellow } };
            GUI.Label(new Rect(10, y, 500, 20), "--- Selected: Smelter ---", infoStyle);
            y += 20;
            GUI.Label(new Rect(10, y, 500, 20), $"Status: {_selectedMachine.Status} | Recipe: {_selectedMachine.ActiveRecipeId ?? "none"}", infoStyle);
            y += 20;
            GUI.Label(new Rect(10, y, 500, 20), $"Progress: {_selectedMachine.CraftProgress:F1} / {_smeltRecipe.craftDuration:F1}s", infoStyle);
            y += 20;

            for (int i = 0; i < _smelterDef.inputBufferSize; i++)
            {
                var slot = _selectedMachine.GetInput(i);
                string contents = slot.IsEmpty ? "(empty)" : $"{slot.item.definitionId} x{slot.count}";
                GUI.Label(new Rect(10, y, 500, 20), $"  Input[{i}]: {contents}", infoStyle);
                y += 18;
            }
            for (int i = 0; i < _smelterDef.outputBufferSize; i++)
            {
                var slot = _selectedMachine.GetOutput(i);
                string contents = slot.IsEmpty ? "(empty)" : $"{slot.item.definitionId} x{slot.count}";
                GUI.Label(new Rect(10, y, 500, 20), $"  Output[{i}]: {contents}", infoStyle);
                y += 18;
            }
        }

        {
            float y = Screen.height - 100;
            var beltStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.7f, 0.7f, 1f) } };
            GUI.Label(new Rect(10, y, 500, 20), "--- Belt (5 tiles) ---", beltStyle);
            y += 18;
            GUI.Label(new Rect(10, y, 500, 20), $"Items: {_demoBelt.ItemCount} | TerminalGap: {_demoBelt.TerminalGap} | AtEnd: {_demoBelt.HasItemAtEnd}", beltStyle);
        }

        if (!_showHelp) return;

        float helpX = Screen.width - 310;
        float helpY = 10;
        float helpW = 300;
        float lineH = 18;
        var helpStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };

        string[] lines = new[]
        {
            "=== CONTROLS (H to hide) ===",
            "",
            "-- Camera --",
            "WASD / QE: Move / Up-Down",
            "Right-click + Mouse: Look",
            "Scroll: Speed | Shift: Fast",
            "",
            "-- Building --",
            "1: Foundation 1x1",
            "2: Foundation 2x2",
            "3: Smelter (2x2)",
            "B: Toggle build mode",
            "R: Rotate",
            "Click: Place",
            "Esc: Cancel build",
            "",
            "-- Machine --",
            "Click machine: Select",
            "F: Feed iron ore",
            "G: Extract output",
            "Y: Toggle auto-tick",
            "T: Manual tick",
            "",
            "-- Belt Demo --",
            "J: Insert item at start",
            "K: Extract from end",
            "L: Tick belt (+20 subs)",
        };

        float totalH = lines.Length * lineH + 10;
        GUI.Box(new Rect(helpX - 5, helpY - 5, helpW + 10, totalH + 10), "");

        for (int i = 0; i < lines.Length; i++)
        {
            GUI.Label(new Rect(helpX, helpY + i * lineH, helpW, lineH), lines[i], helpStyle);
        }
    }
}
