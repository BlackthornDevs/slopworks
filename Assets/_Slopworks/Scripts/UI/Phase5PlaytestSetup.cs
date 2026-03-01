using System.Collections;
using UnityEngine;

/// <summary>
/// Phase 5 playtest. Drop on an empty GameObject, hit Play.
/// Creates: player with inventory, WorldItems to pick up, a smelter to interact with,
/// full HUD, and inventory UI. All runtime-created, no prefab dependencies.
/// </summary>
public class Phase5PlaytestSetup : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int _worldItemCount = 5;

    private ItemDefinitionSO _ironScrapDef;
    private ItemDefinitionSO _ironIngotDef;
    private MachineDefinitionSO _smelterDef;
    private RecipeSO _smeltIronRecipe;

    private void Start()
    {
        Debug.Log("phase 5 playtest: starting setup");

        CreateDefinitions();
        CreateRegistries();
        CreateGround();
        var player = CreatePlayer();
        CreateWorldItems();
        CreateSmelter();
        CreateHUD(player);

        Debug.Log("phase 5 playtest: setup complete");
        Debug.Log("controls: WASD=move, Mouse=look, Space=jump, Shift=sprint");
        Debug.Log("controls: 1-9=hotbar, Tab=inventory, E=interact");
        Debug.Log("controls: walk over cubes to pick up items, E on smelter to set recipe");
    }

    private void CreateDefinitions()
    {
        _ironScrapDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        _ironScrapDef.itemId = "iron_scrap";
        _ironScrapDef.displayName = "Iron Scrap";
        _ironScrapDef.description = "Raw iron scrap, ready for smelting.";
        _ironScrapDef.category = ItemCategory.RawMaterial;
        _ironScrapDef.isStackable = true;
        _ironScrapDef.maxStackSize = 64;

        _ironIngotDef = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        _ironIngotDef.itemId = "iron_ingot";
        _ironIngotDef.displayName = "Iron Ingot";
        _ironIngotDef.description = "Smelted iron, used for crafting.";
        _ironIngotDef.category = ItemCategory.Component;
        _ironIngotDef.isStackable = true;
        _ironIngotDef.maxStackSize = 64;

        _smelterDef = ScriptableObject.CreateInstance<MachineDefinitionSO>();
        _smelterDef.machineId = "smelter";
        _smelterDef.displayName = "Smelter";
        _smelterDef.machineType = "smelter";
        _smelterDef.size = new Vector2Int(2, 2);
        _smelterDef.inputBufferSize = 2;
        _smelterDef.outputBufferSize = 1;
        _smelterDef.processingSpeed = 1f;

        _smeltIronRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        _smeltIronRecipe.recipeId = "smelt_iron";
        _smeltIronRecipe.displayName = "Smelt Iron";
        _smeltIronRecipe.requiredMachineType = "smelter";
        _smeltIronRecipe.craftDuration = 3f;
        _smeltIronRecipe.inputs = new[]
        {
            new RecipeIngredient { itemId = "iron_scrap", count = 2 }
        };
        _smeltIronRecipe.outputs = new[]
        {
            new RecipeIngredient { itemId = "iron_ingot", count = 1 }
        };

        Debug.Log("phase 5 playtest: definitions created");
    }

    private void CreateRegistries()
    {
        var registryObj = new GameObject("Registries");

        // ItemRegistry
        var itemRegistry = registryObj.AddComponent<ItemRegistry>();
        var itemsField = typeof(ItemRegistry).GetField("_items",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        itemsField?.SetValue(itemRegistry, new[] { _ironScrapDef, _ironIngotDef });
        var itemAwake = typeof(ItemRegistry).GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        itemAwake?.Invoke(itemRegistry, null);

        // RecipeRegistry
        var recipeRegistry = registryObj.AddComponent<RecipeRegistry>();
        var recipesField = typeof(RecipeRegistry).GetField("_recipes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        recipesField?.SetValue(recipeRegistry, new[] { _smeltIronRecipe });
        var recipeAwake = typeof(RecipeRegistry).GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        recipeAwake?.Invoke(recipeRegistry, null);

        Debug.Log("phase 5 playtest: registries initialized");
    }

    private void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(10, 1, 10);
        ground.layer = PhysicsLayers.Terrain;

        var renderer = ground.GetComponent<Renderer>();
        renderer.material.color = new Color(0.3f, 0.35f, 0.25f);

        Debug.Log("phase 5 playtest: ground created");
    }

    private GameObject CreatePlayer()
    {
        var player = new GameObject("Player");
        player.layer = PhysicsLayers.Player;
        player.transform.position = new Vector3(0, 1.5f, 0);

        var capsule = player.AddComponent<CapsuleCollider>();
        capsule.radius = 0.3f;
        capsule.height = 1.8f;
        capsule.center = new Vector3(0, 0.9f, 0);

        var rb = player.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Camera
        var camObj = new GameObject("PlayerCamera");
        camObj.transform.SetParent(player.transform, false);
        camObj.transform.localPosition = new Vector3(0, 1.6f, 0);
        var cam = camObj.AddComponent<Camera>();
        camObj.AddComponent<AudioListener>();

        // Components (PlayerInventory before PlayerController so Awake finds it)
        player.AddComponent<PlayerInventory>();
        player.AddComponent<PlayerController>();
        var healthBehaviour = player.AddComponent<HealthBehaviour>();

        // Pickup trigger (child)
        var pickupObj = new GameObject("PickupTrigger");
        pickupObj.transform.SetParent(player.transform, false);
        pickupObj.layer = PhysicsLayers.Player;
        pickupObj.AddComponent<ItemPickupTrigger>();

        // Pre-load items after Awake runs
        StartCoroutine(PreloadInventory(player.GetComponent<PlayerInventory>()));

        Debug.Log("phase 5 playtest: player created at (0, 1.5, 0)");
        return player;
    }

    private IEnumerator PreloadInventory(PlayerInventory inventory)
    {
        yield return null;
        inventory.TryAdd(ItemInstance.Create("iron_scrap"), 10);
        Debug.Log("phase 5 playtest: preloaded 10x iron scrap into inventory");
    }

    private void CreateWorldItems()
    {
        for (int i = 0; i < _worldItemCount; i++)
        {
            float x = Random.Range(-8f, 8f);
            float z = Random.Range(-8f, 8f);
            var pos = new Vector3(x, 0.3f, z);

            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"WorldItem_IronScrap_{i}";
            obj.transform.position = pos;
            obj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            var renderer = obj.GetComponent<Renderer>();
            renderer.material.color = new Color(0.6f, 0.4f, 0.2f);

            // Remove default box collider immediately so WorldItem.Start adds a trigger sphere
            DestroyImmediate(obj.GetComponent<BoxCollider>());

            var worldItem = obj.AddComponent<WorldItem>();
            worldItem.Initialize(_ironScrapDef, Random.Range(1, 4));

            Debug.Log($"phase 5 playtest: world item at ({x:F1}, 0.3, {z:F1})");
        }
    }

    private void CreateSmelter()
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = "Smelter";
        obj.transform.position = new Vector3(4, 0.5f, 4);
        obj.transform.localScale = new Vector3(2, 1, 2);

        var renderer = obj.GetComponent<Renderer>();
        renderer.material.color = new Color(0.8f, 0.4f, 0.1f);

        obj.layer = PhysicsLayers.Interactable;

        var machineBehaviour = obj.AddComponent<MachineBehaviour>();
        var defField = typeof(MachineBehaviour).GetField("_definition",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        defField?.SetValue(machineBehaviour, _smelterDef);
        var awakeMethod = typeof(MachineBehaviour).GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        awakeMethod?.Invoke(machineBehaviour, null);

        Debug.Log("phase 5 playtest: smelter created at (4, 0.5, 4)");
    }

    private void CreateHUD(GameObject player)
    {
        var canvasObj = new GameObject("HUDCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        canvasObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        canvasObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        var hudController = canvasObj.AddComponent<HUDController>();
        canvasObj.AddComponent<RecipeSelectionUI>();
        var inventoryUI = canvasObj.AddComponent<InventoryUI>();

        StartCoroutine(WireHUD(hudController, inventoryUI, player));
    }

    private IEnumerator WireHUD(HUDController hud, InventoryUI inventoryUI, GameObject player)
    {
        yield return null;

        var health = player.GetComponent<HealthBehaviour>();
        var inventory = player.GetComponent<PlayerInventory>();
        var cam = player.GetComponentInChildren<Camera>();

        hud.Initialize(health, inventory, cam);
        inventoryUI.Initialize(inventory);

        Debug.Log("phase 5 playtest: HUD wired to player");
    }
}
