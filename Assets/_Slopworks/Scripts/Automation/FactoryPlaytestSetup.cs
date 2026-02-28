using System;
using UnityEngine;

/// <summary>
/// Self-contained playtest that creates a full automation loop and runs it.
/// Drop this on an empty GameObject, hit Play, and watch the factory work.
///
/// Layout: [Source] -> Inserter -> [Belt A] -> Inserter -> [Smelter] -> Inserter -> [Belt B] -> Inserter -> [Output]
///
/// Status is displayed via OnGUI overlay and inspector fields.
/// Colors update in real-time: green = active/working, yellow = idle, red = blocked.
/// </summary>
public class FactoryPlaytestSetup : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("How many ore items to start with in the source chest.")]
    [SerializeField] private int _startingOreCount = 10;

    [Tooltip("Belt subdivisions per tick. At 50Hz, 2 = 1 tile/sec, 4 = 2 tiles/sec.")]
    [SerializeField] private ushort _beltSpeed = 4;

    [Tooltip("How many tiles long each belt segment is.")]
    [SerializeField] private int _beltLengthTiles = 3;

    [Tooltip("Inserter arm swing time in seconds.")]
    [SerializeField] private float _inserterSwingDuration = 0.4f;

    [Tooltip("Smelting craft duration in seconds.")]
    [SerializeField] private float _craftDuration = 2f;

    // -- Simulation objects --
    private FactorySimulation _simulation;
    private StorageContainer _sourceStorage;
    private StorageContainer _outputStorage;
    private BeltSegment _beltA;
    private BeltSegment _beltB;
    private Machine _smelter;
    private Inserter _inserterSourceToBelt;
    private Inserter _inserterBeltToMachine;
    private Inserter _inserterMachineToBelt;
    private Inserter _inserterBeltToOutput;

    // -- ScriptableObjects (created at runtime) --
    private MachineDefinitionSO _smelterDef;
    private RecipeSO _smeltRecipe;

    // -- Visual GameObjects --
    private GameObject _sourceGO;
    private GameObject _beltAGO;
    private GameObject _smelterGO;
    private GameObject _beltBGO;
    private GameObject _outputGO;
    private GameObject[] _inserterGOs;

    // -- Constants --
    private const string IronOre = "iron_ore";
    private const string IronIngot = "iron_ingot";
    private const string SmeltIronRecipeId = "smelt_iron";
    private const string SmelterType = "smelter";
    private const ushort DefaultSpacing = 50;

    // -- Tracking --
    private float _elapsed;
    private int _itemsProduced;

    private void Awake()
    {
        CreateDefinitions();
        CreateFactoryChain();
        CreateVisuals();

        Debug.Log($"Factory playtest started: {_startingOreCount} ore, belt speed {_beltSpeed}, craft time {_craftDuration}s");
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

        UpdateVisuals();
    }

    private void OnGUI()
    {
        float x = 10;
        float y = 10;
        float w = 320;
        float lineHeight = 22;

        GUI.Box(new Rect(x - 4, y - 4, w + 8, lineHeight * 14 + 8), "");

        DrawLine(ref y, x, w, lineHeight, "FACTORY PLAYTEST", true);
        DrawLine(ref y, x, w, lineHeight, $"Elapsed: {_elapsed:F1}s  |  Belt speed: {_beltSpeed}");

        y += 6;
        DrawLine(ref y, x, w, lineHeight, $"Source chest:  {_sourceStorage.GetTotalItemCount()} ore remaining");

        string ins1 = FormatInserter(_inserterSourceToBelt, "Src->Belt");
        DrawLine(ref y, x, w, lineHeight, ins1);

        DrawLine(ref y, x, w, lineHeight, $"Belt A:  {_beltA.ItemCount} items  |  end: {(_beltA.HasItemAtEnd ? "ready" : "clear")}");

        string ins2 = FormatInserter(_inserterBeltToMachine, "Belt->Smelt");
        DrawLine(ref y, x, w, lineHeight, ins2);

        DrawLine(ref y, x, w, lineHeight, $"Smelter:  {_smelter.Status}  |  progress: {_smelter.CraftProgress:P0}");
        DrawLine(ref y, x, w, lineHeight, $"  input: {FormatSlot(_smelter.GetInput(0))}  |  output: {FormatSlot(_smelter.GetOutput(0))}");

        string ins3 = FormatInserter(_inserterMachineToBelt, "Smelt->Belt");
        DrawLine(ref y, x, w, lineHeight, ins3);

        DrawLine(ref y, x, w, lineHeight, $"Belt B:  {_beltB.ItemCount} items  |  end: {(_beltB.HasItemAtEnd ? "ready" : "clear")}");

        string ins4 = FormatInserter(_inserterBeltToOutput, "Belt->Out");
        DrawLine(ref y, x, w, lineHeight, ins4);

        DrawLine(ref y, x, w, lineHeight, $"Output chest:  {_outputStorage.GetTotalItemCount()} ingots  ({_itemsProduced} total)");
    }

    private void OnDestroy()
    {
        if (_smelterDef != null) DestroyImmediate(_smelterDef);
        if (_smeltRecipe != null) DestroyImmediate(_smeltRecipe);
    }

    // -- Setup --

    private void CreateDefinitions()
    {
        _smelterDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        _smelterDef.machineId = "smelter_basic";
        _smelterDef.machineType = SmelterType;
        _smelterDef.displayName = "Basic Smelter";
        _smelterDef.inputBufferSize = 2;
        _smelterDef.outputBufferSize = 2;
        _smelterDef.processingSpeed = 1f;
        _smelterDef.powerConsumption = 100f;
        _smelterDef.ports = new MachinePort[0];

        _smeltRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        _smeltRecipe.recipeId = SmeltIronRecipeId;
        _smeltRecipe.displayName = "Smelt Iron";
        _smeltRecipe.inputs = new[] { new RecipeIngredient { itemId = IronOre, count = 1 } };
        _smeltRecipe.outputs = new[] { new RecipeIngredient { itemId = IronIngot, count = 1 } };
        _smeltRecipe.craftDuration = _craftDuration;
        _smeltRecipe.requiredMachineType = SmelterType;
    }

    private void CreateFactoryChain()
    {
        // Recipe lookup
        RecipeSO LookupRecipe(string id) => id == SmeltIronRecipeId ? _smeltRecipe : null;

        // Core simulation
        _simulation = new FactorySimulation(LookupRecipe);
        _simulation.BeltSpeed = _beltSpeed;

        // Storage containers
        _sourceStorage = new StorageContainer(4, 50);
        _outputStorage = new StorageContainer(4, 50);

        // Pre-fill source
        for (int i = 0; i < _startingOreCount; i++)
            _sourceStorage.TryInsert(IronOre);

        // Belts
        _beltA = new BeltSegment(_beltLengthTiles);
        _beltB = new BeltSegment(_beltLengthTiles);

        // Machine
        _smelter = new Machine(_smelterDef);
        _smelter.SetRecipe(SmeltIronRecipeId);

        // Inserters
        _inserterSourceToBelt = new Inserter(
            _sourceStorage,
            new BeltInputAdapter(_beltA, DefaultSpacing),
            _inserterSwingDuration);

        _inserterBeltToMachine = new Inserter(
            new BeltOutputAdapter(_beltA),
            new MachineInputAdapter(_smelter, 0),
            _inserterSwingDuration);

        _inserterMachineToBelt = new Inserter(
            new MachineOutputAdapter(_smelter, 0),
            new BeltInputAdapter(_beltB, DefaultSpacing),
            _inserterSwingDuration);

        _inserterBeltToOutput = new Inserter(
            new BeltOutputAdapter(_beltB),
            _outputStorage,
            _inserterSwingDuration);

        // Register everything
        _simulation.RegisterBelt(_beltA);
        _simulation.RegisterBelt(_beltB);
        _simulation.RegisterMachine(_smelter);
        _simulation.RegisterInserter(_inserterSourceToBelt);
        _simulation.RegisterInserter(_inserterBeltToMachine);
        _simulation.RegisterInserter(_inserterMachineToBelt);
        _simulation.RegisterInserter(_inserterBeltToOutput);
    }

    private void CreateVisuals()
    {
        float spacing = 3f;
        float x = 0f;

        // Source storage (blue cube)
        _sourceGO = CreatePrimitive("Source Chest", PrimitiveType.Cube, new Vector3(x, 0.5f, 0), Color.cyan);
        x += spacing;

        // Inserter 1
        var ins1GO = CreatePrimitive("Inserter: Src->Belt", PrimitiveType.Capsule, new Vector3(x, 0.3f, 0), Color.gray);
        ins1GO.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        x += spacing;

        // Belt A (stretched thin cube)
        float beltLen = _beltLengthTiles;
        _beltAGO = CreatePrimitive("Belt A", PrimitiveType.Cube,
            new Vector3(x + beltLen * 0.5f, 0.1f, 0), Color.gray);
        _beltAGO.transform.localScale = new Vector3(beltLen, 0.2f, 0.8f);
        x += beltLen + spacing;

        // Inserter 2
        var ins2GO = CreatePrimitive("Inserter: Belt->Smelt", PrimitiveType.Capsule, new Vector3(x, 0.3f, 0), Color.gray);
        ins2GO.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        x += spacing;

        // Smelter (larger orange cube)
        _smelterGO = CreatePrimitive("Smelter", PrimitiveType.Cube, new Vector3(x, 0.75f, 0), Color.yellow);
        _smelterGO.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        x += spacing;

        // Inserter 3
        var ins3GO = CreatePrimitive("Inserter: Smelt->Belt", PrimitiveType.Capsule, new Vector3(x, 0.3f, 0), Color.gray);
        ins3GO.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        x += spacing;

        // Belt B
        _beltBGO = CreatePrimitive("Belt B", PrimitiveType.Cube,
            new Vector3(x + beltLen * 0.5f, 0.1f, 0), Color.gray);
        _beltBGO.transform.localScale = new Vector3(beltLen, 0.2f, 0.8f);
        x += beltLen + spacing;

        // Inserter 4
        var ins4GO = CreatePrimitive("Inserter: Belt->Out", PrimitiveType.Capsule, new Vector3(x, 0.3f, 0), Color.gray);
        ins4GO.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        x += spacing;

        // Output storage (green cube)
        _outputGO = CreatePrimitive("Output Chest", PrimitiveType.Cube, new Vector3(x, 0.5f, 0), Color.green);

        _inserterGOs = new[] { ins1GO, ins2GO, ins3GO, ins4GO };
    }

    private void UpdateVisuals()
    {
        // Source: dim as it empties
        float srcRatio = _sourceStorage.GetTotalItemCount() / (float)_startingOreCount;
        SetColor(_sourceGO, Color.Lerp(Color.gray, Color.cyan, srcRatio));

        // Smelter: color by status
        switch (_smelter.Status)
        {
            case MachineStatus.Working:
                SetColor(_smelterGO, Color.Lerp(Color.yellow, Color.green, _smelter.CraftProgress));
                break;
            case MachineStatus.Blocked:
                SetColor(_smelterGO, Color.red);
                break;
            default:
                SetColor(_smelterGO, Color.yellow);
                break;
        }

        // Belts: color by item count
        SetColor(_beltAGO, _beltA.ItemCount > 0 ? new Color(0.5f, 0.5f, 1f) : Color.gray);
        SetColor(_beltBGO, _beltB.ItemCount > 0 ? new Color(0.5f, 0.5f, 1f) : Color.gray);

        // Output: brighten as it fills
        float outRatio = _outputStorage.GetTotalItemCount() / (float)Mathf.Max(1, _startingOreCount);
        SetColor(_outputGO, Color.Lerp(Color.gray, Color.green, outRatio));

        // Inserters: highlight when swinging
        Inserter[] inserters = { _inserterSourceToBelt, _inserterBeltToMachine, _inserterMachineToBelt, _inserterBeltToOutput };
        for (int i = 0; i < inserters.Length; i++)
        {
            Color c = inserters[i].IsSwinging ? Color.white :
                       inserters[i].HeldItemId != null ? Color.red : Color.gray;
            SetColor(_inserterGOs[i], c);
        }
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

    private static string FormatInserter(Inserter ins, string label)
    {
        string state = ins.IsSwinging ? $"swinging {ins.SwingProgress:P0}" :
                       ins.HeldItemId != null ? $"held: {ins.HeldItemId}" : "idle";
        return $"  {label}:  {state}";
    }

    private static string FormatSlot(ItemSlot slot)
    {
        return slot.IsEmpty ? "empty" : $"{slot.item.definitionId} x{slot.count}";
    }

    private static void DrawLine(ref float y, float x, float w, float h, string text, bool bold = false)
    {
        var style = bold ? GUI.skin.label : GUI.skin.label;
        if (bold)
        {
            style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
        }
        GUI.Label(new Rect(x, y, w, h), text, style);
        y += h;
    }
}
