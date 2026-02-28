using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Integration playtest for the PortNode spatial connection system.
/// Drop this on an empty GameObject, hit Play, and watch the full pipeline:
///
/// [Source Storage] -> Belt A -> [Smelter] -> Belt B -> [Output Storage]
///
/// Everything is placed on the FactoryGrid using BuildingPlacementService.
/// Connections (inserters) are auto-created by the PortNode system when
/// compatible ports face each other in adjacent cells.
///
/// Belt items are visualized as small cubes traveling along each belt.
/// OnGUI overlay shows real-time status of every component.
/// </summary>
public class PortNodePlaytestSetup : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int _startingOreCount = 10;
    [SerializeField] private ushort _beltSpeed = 4;
    [SerializeField] private int _beltLengthTiles = 4;
    [SerializeField] private float _craftDuration = 2f;

    // -- Infrastructure --
    private FactoryGrid _grid;
    private FactorySimulation _simulation;
    private PortNodeRegistry _portRegistry;
    private ConnectionResolver _connectionResolver;
    private BuildingPlacementService _placementService;
    private StructuralPlacementService _structuralService;
    private SnapPointRegistry _snapRegistry;

    // -- Placement results --
    private PlacementResult _sourceResult;
    private PlacementResult _beltAResult;
    private PlacementResult _smelterResult;
    private PlacementResult _beltBResult;
    private PlacementResult _outputResult;

    // -- Shortcuts to simulation objects --
    private StorageContainer _sourceStorage;
    private StorageContainer _outputStorage;
    private BeltSegment _beltA;
    private BeltSegment _beltB;
    private Machine _smelter;

    // -- Definitions (created at runtime) --
    private MachineDefinitionSO _smelterDef;
    private StorageDefinitionSO _storageDef;
    private FoundationDefinitionSO _foundationDef;
    private RecipeSO _smeltRecipe;

    // -- Visuals --
    private GameObject _sourceGO;
    private GameObject _smelterGO;
    private GameObject _outputGO;
    private GameObject _beltAGO;
    private GameObject _beltBGO;
    private readonly List<GameObject> _beltAItemPool = new();
    private readonly List<GameObject> _beltBItemPool = new();
    private readonly List<float> _positionBuffer = new();

    // -- Constants --
    private const string IronOre = "iron_ore";
    private const string IronIngot = "iron_ingot";
    private const string SmeltIronRecipeId = "smelt_iron";
    private const string SmelterType = "smelter";

    // -- Tracking --
    private float _elapsed;
    private int _itemsProduced;
    private int _inserterCount;

    private void Awake()
    {
        CreateDefinitions();
        CreateInfrastructure();
        PlaceEverything();
        SeedSource();
        CreateVisuals();

        _inserterCount = _simulation.InserterCount;
        Debug.Log($"PortNode playtest started: {_startingOreCount} ore, {_inserterCount} auto-created inserters, belt speed {_beltSpeed}");
    }

    private void FixedUpdate()
    {
        _simulation.Tick(Time.fixedDeltaTime);
        _elapsed += Time.fixedDeltaTime;

        int outputCount = _outputStorage.GetTotalItemCount();
        if (outputCount > _itemsProduced)
        {
            _itemsProduced = outputCount;
            Debug.Log($"[{_elapsed:F1}s] Ingot #{_itemsProduced} arrived in output storage");
        }
    }

    private void Update()
    {
        UpdateVisuals();
        UpdateBeltItems(_beltA, _beltAGO, _beltAItemPool);
        UpdateBeltItems(_beltB, _beltBGO, _beltBItemPool);
    }

    private void OnGUI()
    {
        float x = 10;
        float y = 10;
        float w = 400;
        float h = 22;

        GUI.Box(new Rect(x - 4, y - 4, w + 8, h * 16 + 8), "");

        DrawLine(ref y, x, w, h, "PORT NODE INTEGRATION PLAYTEST", true);
        DrawLine(ref y, x, w, h, $"Elapsed: {_elapsed:F1}s  |  Belt speed: {_beltSpeed}  |  Auto-inserters: {_inserterCount}");

        y += 6;
        DrawLine(ref y, x, w, h, $"Source storage:  {_sourceStorage.GetTotalItemCount()} ore remaining");
        DrawLine(ref y, x, w, h, $"Belt A:  {_beltA.ItemCount} items  |  end: {(_beltA.HasItemAtEnd ? "READY" : "clear")}");

        DrawLine(ref y, x, w, h, $"Smelter:  {_smelter.Status}  |  progress: {_smelter.CraftProgress:P0}");
        DrawLine(ref y, x, w, h, $"  input: {FormatSlot(_smelter.GetInput(0))}  |  output: {FormatSlot(_smelter.GetOutput(0))}");

        DrawLine(ref y, x, w, h, $"Belt B:  {_beltB.ItemCount} items  |  end: {(_beltB.HasItemAtEnd ? "READY" : "clear")}");
        DrawLine(ref y, x, w, h, $"Output storage:  {_outputStorage.GetTotalItemCount()} ingots  ({_itemsProduced} total)");

        y += 6;
        DrawLine(ref y, x, w, h, "-- INSERTERS (auto-created by PortNode system) --", true);
        var inserters = _simulation.GetInserters();
        for (int i = 0; i < inserters.Count; i++)
        {
            var ins = inserters[i];
            string state = ins.IsSwinging ? $"swinging {ins.SwingProgress:P0}" :
                           ins.HeldItemId != null ? $"held: {ins.HeldItemId}" : "idle";
            DrawLine(ref y, x, w, h, $"  Inserter {i + 1}:  {state}");
        }

        y += 6;
        DrawLine(ref y, x, w, h, "-- PORT NODES --", true);
        DrawLine(ref y, x, w, h, $"  Registered: {_portRegistry.Count}  |  Belt links: {_simulation.BeltNetwork.ConnectionCount}");
    }

    private void OnDestroy()
    {
        if (_smelterDef != null) DestroyImmediate(_smelterDef);
        if (_storageDef != null) DestroyImmediate(_storageDef);
        if (_foundationDef != null) DestroyImmediate(_foundationDef);
        if (_smeltRecipe != null) DestroyImmediate(_smeltRecipe);
    }

    // -- Setup --

    private void CreateDefinitions()
    {
        _smelterDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        _smelterDef.machineId = "smelter_basic";
        _smelterDef.machineType = SmelterType;
        _smelterDef.displayName = "Basic Smelter";
        _smelterDef.size = Vector2Int.one;
        _smelterDef.inputBufferSize = 2;
        _smelterDef.outputBufferSize = 2;
        _smelterDef.processingSpeed = 1f;
        _smelterDef.ports = new[]
        {
            new MachinePort
            {
                localOffset = Vector2Int.zero,
                direction = new Vector2Int(-1, 0), // input from west
                type = PortType.Input
            },
            new MachinePort
            {
                localOffset = Vector2Int.zero,
                direction = new Vector2Int(1, 0), // output to east
                type = PortType.Output
            }
        };

        _storageDef = ScriptableObject.CreateInstance<StorageDefinitionSO>();
        _storageDef.storageId = "storage_bin";
        _storageDef.slotCount = 4;
        _storageDef.maxStackSize = 50;
        _storageDef.size = Vector2Int.one;
        _storageDef.ports = new[]
        {
            new MachinePort
            {
                localOffset = Vector2Int.zero,
                direction = new Vector2Int(-1, 0), // input from west
                type = PortType.Input
            },
            new MachinePort
            {
                localOffset = Vector2Int.zero,
                direction = new Vector2Int(1, 0), // output to east
                type = PortType.Output
            }
        };

        _foundationDef = ScriptableObject.CreateInstance<FoundationDefinitionSO>();
        _foundationDef.foundationId = "foundation_1x1";
        _foundationDef.size = Vector2Int.one;
        _foundationDef.generatesSnapPoints = true;

        _smeltRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        _smeltRecipe.recipeId = SmeltIronRecipeId;
        _smeltRecipe.displayName = "Smelt Iron";
        _smeltRecipe.inputs = new[] { new RecipeIngredient { itemId = IronOre, count = 1 } };
        _smeltRecipe.outputs = new[] { new RecipeIngredient { itemId = IronIngot, count = 1 } };
        _smeltRecipe.craftDuration = _craftDuration;
        _smeltRecipe.requiredMachineType = SmelterType;
    }

    private void CreateInfrastructure()
    {
        RecipeSO LookupRecipe(string id) => id == SmeltIronRecipeId ? _smeltRecipe : null;

        _grid = new FactoryGrid();
        _simulation = new FactorySimulation(LookupRecipe);
        _simulation.BeltSpeed = _beltSpeed;

        _portRegistry = new PortNodeRegistry();
        _connectionResolver = new ConnectionResolver(_portRegistry, _simulation);
        _placementService = new BuildingPlacementService(
            _grid, _portRegistry, _connectionResolver, _simulation);

        _snapRegistry = new SnapPointRegistry();
        _structuralService = new StructuralPlacementService(_grid, _snapRegistry);
    }

    private void PlaceEverything()
    {
        // Layout on grid row 5, going east:
        // Cell 0: Source storage (output east)
        // Cells 1-4: Belt A (input west at 1, output east at 4)
        // Cell 5: Smelter (input west, output east)
        // Cells 6-9: Belt B (input west at 6, output east at 9)
        // Cell 10: Output storage (input west)

        int row = 5;

        // 0. Place foundations under all cells that will hold automation buildings
        int beltAEnd = _beltLengthTiles;
        int smelterCell = beltAEnd + 1;
        int beltBStart = smelterCell + 1;
        int beltBEnd = beltBStart + _beltLengthTiles - 1;
        int outputCell = beltBEnd + 1;

        for (int x = 0; x <= outputCell; x++)
            _structuralService.PlaceFoundation(_foundationDef, new Vector2Int(x, row), 0);

        // 1. Source storage
        _sourceResult = _placementService.PlaceStorage(_storageDef, new Vector2Int(0, row), 0);
        _sourceStorage = (StorageContainer)_sourceResult.SimulationObject;

        // 2. Belt A: from cell 1 to cell (1 + beltLength - 1)
        _beltAResult = _placementService.PlaceBelt(new Vector2Int(1, row), new Vector2Int(beltAEnd, row));
        _beltA = (BeltSegment)_beltAResult.SimulationObject;

        // 3. Smelter: at cell after belt A
        _smelterResult = _placementService.PlaceMachine(_smelterDef, new Vector2Int(smelterCell, row), 0);
        _smelter = (Machine)_smelterResult.SimulationObject;
        _smelter.SetRecipe(SmeltIronRecipeId);

        // 4. Belt B: from cell after smelter
        _beltBResult = _placementService.PlaceBelt(new Vector2Int(beltBStart, row), new Vector2Int(beltBEnd, row));
        _beltB = (BeltSegment)_beltBResult.SimulationObject;

        // 5. Output storage: at cell after belt B
        _outputResult = _placementService.PlaceStorage(_storageDef, new Vector2Int(outputCell, row), 0);
        _outputStorage = (StorageContainer)_outputResult.SimulationObject;

        Debug.Log($"Placed: source@0, beltA@1-{beltAEnd}, smelter@{smelterCell}, beltB@{beltBStart}-{beltBEnd}, output@{outputCell}");
        Debug.Log($"Auto-created {_simulation.InserterCount} inserters, {_simulation.BeltNetwork.ConnectionCount} belt links");
        Debug.Log($"Registered {_portRegistry.Count} port nodes");
    }

    private void SeedSource()
    {
        for (int i = 0; i < _startingOreCount; i++)
            _sourceStorage.TryInsert(IronOre);
    }

    // -- Visuals --

    private void CreateVisuals()
    {
        float cellSize = FactoryGrid.CellSize;
        int row = 5;

        // Source storage (cyan cube)
        Vector3 sourcePos = _grid.CellToWorld(new Vector2Int(0, row));
        _sourceGO = CreatePrimitive("Source Storage", PrimitiveType.Cube,
            sourcePos + Vector3.up * 0.5f, Color.cyan);

        // Belt A (stretched dark gray plane)
        int beltAEnd = _beltLengthTiles;
        Vector3 beltAStart = _grid.CellToWorld(new Vector2Int(1, row));
        Vector3 beltAEndPos = _grid.CellToWorld(new Vector2Int(beltAEnd, row));
        Vector3 beltACenter = (beltAStart + beltAEndPos) * 0.5f + Vector3.up * 0.05f;
        _beltAGO = CreatePrimitive("Belt A", PrimitiveType.Cube, beltACenter, new Color(0.3f, 0.3f, 0.3f));
        _beltAGO.transform.localScale = new Vector3(_beltLengthTiles * cellSize, 0.1f, 0.6f);

        // Smelter (orange cube)
        int smelterCell = beltAEnd + 1;
        Vector3 smelterPos = _grid.CellToWorld(new Vector2Int(smelterCell, row));
        _smelterGO = CreatePrimitive("Smelter", PrimitiveType.Cube,
            smelterPos + Vector3.up * 0.6f, new Color(1f, 0.5f, 0f));
        _smelterGO.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);

        // Belt B
        int beltBStart = smelterCell + 1;
        int beltBEnd = beltBStart + _beltLengthTiles - 1;
        Vector3 beltBStartPos = _grid.CellToWorld(new Vector2Int(beltBStart, row));
        Vector3 beltBEndPos = _grid.CellToWorld(new Vector2Int(beltBEnd, row));
        Vector3 beltBCenter = (beltBStartPos + beltBEndPos) * 0.5f + Vector3.up * 0.05f;
        _beltBGO = CreatePrimitive("Belt B", PrimitiveType.Cube, beltBCenter, new Color(0.3f, 0.3f, 0.3f));
        _beltBGO.transform.localScale = new Vector3(_beltLengthTiles * cellSize, 0.1f, 0.6f);

        // Output storage (green cube)
        int outputCell = beltBEnd + 1;
        Vector3 outputPos = _grid.CellToWorld(new Vector2Int(outputCell, row));
        _outputGO = CreatePrimitive("Output Storage", PrimitiveType.Cube,
            outputPos + Vector3.up * 0.5f, Color.green);

        // Position camera to view the layout
        var cam = Camera.main;
        if (cam != null)
        {
            float midX = (sourcePos.x + outputPos.x) * 0.5f;
            cam.transform.position = new Vector3(midX, 8f, sourcePos.z - 6f);
            cam.transform.LookAt(new Vector3(midX, 0f, sourcePos.z));
        }
    }

    private void UpdateVisuals()
    {
        // Source: dim as it empties
        float srcRatio = _sourceStorage.GetTotalItemCount() / (float)Mathf.Max(1, _startingOreCount);
        SetColor(_sourceGO, Color.Lerp(Color.gray, Color.cyan, srcRatio));

        // Smelter: color by status
        switch (_smelter.Status)
        {
            case MachineStatus.Working:
                SetColor(_smelterGO, Color.Lerp(new Color(1f, 0.5f, 0f), Color.green, _smelter.CraftProgress));
                break;
            case MachineStatus.Blocked:
                SetColor(_smelterGO, Color.red);
                break;
            default:
                SetColor(_smelterGO, new Color(1f, 0.5f, 0f));
                break;
        }

        // Output: brighten as it fills
        float outRatio = _outputStorage.GetTotalItemCount() / (float)Mathf.Max(1, _startingOreCount);
        SetColor(_outputGO, Color.Lerp(Color.gray, Color.green, outRatio));
    }

    private void UpdateBeltItems(BeltSegment belt, GameObject beltGO, List<GameObject> pool)
    {
        belt.GetItemPositions(_positionBuffer);

        // Get belt world endpoints from the belt visual
        float halfLen = beltGO.transform.localScale.x * 0.5f;
        Vector3 center = beltGO.transform.position;
        Vector3 startPos = center - Vector3.right * halfLen;
        Vector3 endPos = center + Vector3.right * halfLen;

        // Ensure pool has enough cubes
        while (pool.Count < _positionBuffer.Count)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "BeltItem";
            cube.transform.localScale = Vector3.one * 0.25f;
            var collider = cube.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            // Ore = brown, ingot = silver
            bool isOnBeltA = (belt == _beltA);
            SetColor(cube, isOnBeltA ? new Color(0.6f, 0.3f, 0.1f) : new Color(0.7f, 0.7f, 0.8f));

            cube.SetActive(false);
            pool.Add(cube);
        }

        // Position active items
        for (int i = 0; i < _positionBuffer.Count; i++)
        {
            var cube = pool[i];
            cube.SetActive(true);
            cube.transform.position = Vector3.Lerp(startPos, endPos, _positionBuffer[i])
                                      + Vector3.up * 0.2f;
        }

        // Hide unused
        for (int i = _positionBuffer.Count; i < pool.Count; i++)
            pool[i].SetActive(false);
    }

    // -- Helpers --

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 pos, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = pos;

        var collider = go.GetComponent<Collider>();
        if (collider != null) DestroyImmediate(collider);

        SetColor(go, color);
        return go;
    }

    private static void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (Application.isPlaying)
                renderer.material.color = color;
            else
                renderer.sharedMaterial.color = color;
        }
    }

    private static string FormatSlot(ItemSlot slot)
    {
        return slot.IsEmpty ? "empty" : $"{slot.item.definitionId} x{slot.count}";
    }

    private static void DrawLine(ref float y, float x, float w, float h, string text, bool bold = false)
    {
        var style = GUI.skin.label;
        if (bold)
            style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
        GUI.Label(new Rect(x, y, w, h), text, style);
        y += h;
    }
}
