# Phase 5: Core UI + Player Inventory + Scene Management -- Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the scene loader, HUD, and player inventory systems that every subsequent phase depends on.

**Architecture:** Pure C# simulation classes (D-004) with thin MonoBehaviour wrappers. uGUI for all UI. SceneManager.LoadSceneAsync behind an ISceneService interface (D-002). Input via existing SlopworksControls generated class.

**Tech Stack:** Unity 6 URP, uGUI (TextMeshPro + Image), Unity Input System, existing Inventory/ItemSlot/ItemInstance classes.

**Pre-requisites:** Load `slopworks-patterns` and `slopworks-architecture` skills before writing any C# code.

---

## Task 1: Scene Loader -- Pure C# + Interface

**Files:**
- Create: `Assets/_Slopworks/Scripts/Core/ISceneService.cs`
- Create: `Assets/_Slopworks/Scripts/Core/SceneGroupDefinition.cs`
- Create: `Assets/_Slopworks/Scripts/Core/SceneLoader.cs`
- Test: `Assets/_Slopworks/Tests/Editor/EditMode/SceneLoaderTests.cs`

### Step 1: Write the failing tests

```csharp
// Assets/_Slopworks/Tests/Editor/EditMode/SceneLoaderTests.cs
using NUnit.Framework;
using System.Collections.Generic;

[TestFixture]
public class SceneLoaderTests
{
    [Test]
    public void GetGroup_returns_scenes_for_known_group()
    {
        var groups = new Dictionary<string, string[]>
        {
            ["HomeBase"] = new[] { "HomeBase_Terrain", "HomeBase_Grid", "HomeBase_UI" }
        };
        var loader = new SceneLoader(groups);

        var result = loader.GetGroup("HomeBase");

        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("HomeBase_Terrain", result[0]);
    }

    [Test]
    public void GetGroup_returns_null_for_unknown_group()
    {
        var groups = new Dictionary<string, string[]>();
        var loader = new SceneLoader(groups);

        Assert.IsNull(loader.GetGroup("NonExistent"));
    }

    [Test]
    public void CurrentGroup_starts_null()
    {
        var groups = new Dictionary<string, string[]>();
        var loader = new SceneLoader(groups);

        Assert.IsNull(loader.CurrentGroup);
    }

    [Test]
    public void SetCurrentGroup_updates_property()
    {
        var groups = new Dictionary<string, string[]>
        {
            ["HomeBase"] = new[] { "HomeBase_Terrain" }
        };
        var loader = new SceneLoader(groups);

        loader.SetCurrentGroup("HomeBase");

        Assert.AreEqual("HomeBase", loader.CurrentGroup);
    }

    [Test]
    public void SetCurrentGroup_rejects_unknown_group()
    {
        var groups = new Dictionary<string, string[]>();
        var loader = new SceneLoader(groups);

        loader.SetCurrentGroup("BadGroup");

        Assert.IsNull(loader.CurrentGroup);
    }
}
```

### Step 2: Run tests to verify they fail

Run: Unity EditMode tests with filter `SceneLoaderTests`
Expected: FAIL -- `SceneLoader` class does not exist

### Step 3: Create ISceneService interface

```csharp
// Assets/_Slopworks/Scripts/Core/ISceneService.cs

/// <summary>
/// Abstraction over scene loading (D-002). Swap implementation for Addressables later.
/// </summary>
public interface ISceneService
{
    string CurrentGroup { get; }
    void TransitionTo(string groupName, System.Action onComplete = null);
}
```

### Step 4: Create SceneGroupDefinition

```csharp
// Assets/_Slopworks/Scripts/Core/SceneGroupDefinition.cs
using UnityEngine;

/// <summary>
/// Maps a group name to its additive scenes. Serializable for inspector assignment.
/// </summary>
[System.Serializable]
public class SceneGroupDefinition
{
    public string groupName;
    public string[] sceneNames;
}
```

### Step 5: Create SceneLoader (pure C#)

```csharp
// Assets/_Slopworks/Scripts/Core/SceneLoader.cs
using System.Collections.Generic;

/// <summary>
/// Pure C# scene loader logic (D-004). Manages scene group definitions and tracks
/// current group. The MonoBehaviour wrapper handles actual Unity scene loading calls.
/// </summary>
public class SceneLoader
{
    private readonly Dictionary<string, string[]> _groups;

    public string CurrentGroup { get; private set; }

    public SceneLoader(Dictionary<string, string[]> groups)
    {
        _groups = groups ?? new Dictionary<string, string[]>();
    }

    public string[] GetGroup(string groupName)
    {
        if (string.IsNullOrEmpty(groupName))
            return null;
        _groups.TryGetValue(groupName, out var scenes);
        return scenes;
    }

    public void SetCurrentGroup(string groupName)
    {
        if (!string.IsNullOrEmpty(groupName) && _groups.ContainsKey(groupName))
            CurrentGroup = groupName;
    }
}
```

### Step 6: Run tests to verify they pass

Run: Unity EditMode tests with filter `SceneLoaderTests`
Expected: 5/5 PASS

### Step 7: Commit

```bash
git add Assets/_Slopworks/Scripts/Core/ISceneService.cs \
       Assets/_Slopworks/Scripts/Core/SceneGroupDefinition.cs \
       Assets/_Slopworks/Scripts/Core/SceneLoader.cs \
       Assets/_Slopworks/Tests/Editor/EditMode/SceneLoaderTests.cs
git commit -m "Add SceneLoader pure C# class with ISceneService interface

D-002 compliant: wraps scene group lookups behind interface for
future Addressables swap. Groups map names to scene arrays.
EditMode tests cover group lookup and state tracking."
```

---

## Task 2: SceneLoaderBehaviour -- MonoBehaviour Wrapper

**Files:**
- Create: `Assets/_Slopworks/Scripts/Core/SceneLoaderBehaviour.cs`
- Modify: `Assets/_Slopworks/Scripts/Core/Bootstrap.cs`

### Step 1: Create SceneLoaderBehaviour

```csharp
// Assets/_Slopworks/Scripts/Core/SceneLoaderBehaviour.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MonoBehaviour wrapper for SceneLoader. Lives in Core_GameManager (always resident).
/// Handles coroutine-based async scene loading and fade transitions.
/// </summary>
public class SceneLoaderBehaviour : MonoBehaviour, ISceneService
{
    [SerializeField] private SceneGroupDefinition[] _sceneGroups;
    [SerializeField] private float _fadeDuration = 0.3f;

    private SceneLoader _loader;
    private CanvasGroup _fadePanel;
    private bool _isTransitioning;

    public string CurrentGroup => _loader?.CurrentGroup;

    private void Awake()
    {
        var groups = new Dictionary<string, string[]>();
        if (_sceneGroups != null)
        {
            foreach (var group in _sceneGroups)
            {
                if (!string.IsNullOrEmpty(group.groupName) && group.sceneNames != null)
                    groups[group.groupName] = group.sceneNames;
            }
        }

        _loader = new SceneLoader(groups);
        CreateFadePanel();
    }

    public void TransitionTo(string groupName, Action onComplete = null)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning("scene loader: transition already in progress, ignoring");
            return;
        }

        var scenes = _loader.GetGroup(groupName);
        if (scenes == null)
        {
            Debug.LogError($"scene loader: unknown group '{groupName}'");
            return;
        }

        StartCoroutine(TransitionCoroutine(groupName, scenes, onComplete));
    }

    private IEnumerator TransitionCoroutine(string groupName, string[] targetScenes, Action onComplete)
    {
        _isTransitioning = true;
        Debug.Log($"scene loader: transitioning to {groupName}");

        // Fade to black
        yield return FadeCoroutine(0f, 1f);

        // Unload current group
        var currentScenes = _loader.GetGroup(_loader.CurrentGroup);
        if (currentScenes != null)
        {
            foreach (var sceneName in currentScenes)
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                if (scene.isLoaded)
                {
                    var op = SceneManager.UnloadSceneAsync(scene);
                    if (op != null)
                        yield return op;
                }
            }
        }

        // Load target group
        foreach (var sceneName in targetScenes)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded)
            {
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op != null)
                    yield return op;
            }
        }

        _loader.SetCurrentGroup(groupName);

        // Fade from black
        yield return FadeCoroutine(1f, 0f);

        _isTransitioning = false;
        Debug.Log($"scene loader: arrived at {groupName}");
        onComplete?.Invoke();
    }

    private IEnumerator FadeCoroutine(float from, float to)
    {
        if (_fadePanel == null) yield break;

        _fadePanel.gameObject.SetActive(true);
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _fadePanel.alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
            yield return null;
        }

        _fadePanel.alpha = to;

        if (to <= 0f)
            _fadePanel.gameObject.SetActive(false);
    }

    private void CreateFadePanel()
    {
        // Create overlay canvas for fade
        var canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasObj.AddComponent<CanvasScaler>();

        var panelObj = new GameObject("FadePanel");
        panelObj.transform.SetParent(canvasObj.transform, false);

        var image = panelObj.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _fadePanel = panelObj.AddComponent<CanvasGroup>();
        _fadePanel.alpha = 0f;
        _fadePanel.blocksRaycasts = false;
        _fadePanel.interactable = false;
        panelObj.SetActive(false);
    }
}
```

### Step 2: Update Bootstrap to wire SceneLoaderBehaviour

Replace the current Bootstrap.cs:

```csharp
// Assets/_Slopworks/Scripts/Core/Bootstrap.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrap : MonoBehaviour
{
    [SerializeField] private string _gameManagerScene = "Core_GameManager";
    [SerializeField] private string _initialSceneGroup = "HomeBase";

    private void Awake()
    {
        if (!SceneManager.GetSceneByName(_gameManagerScene).isLoaded)
        {
            SceneManager.sceneLoaded += OnGameManagerLoaded;
            SceneManager.LoadScene(_gameManagerScene, LoadSceneMode.Additive);
        }
        else
        {
            LoadInitialGroup();
        }
    }

    private void OnGameManagerLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != _gameManagerScene)
            return;

        SceneManager.sceneLoaded -= OnGameManagerLoaded;
        LoadInitialGroup();
    }

    private void LoadInitialGroup()
    {
        if (string.IsNullOrEmpty(_initialSceneGroup))
            return;

        var sceneLoader = FindAnyObjectByType<SceneLoaderBehaviour>();
        if (sceneLoader != null)
        {
            sceneLoader.TransitionTo(_initialSceneGroup);
        }
        else
        {
            Debug.LogWarning("bootstrap: no SceneLoaderBehaviour found, skipping initial scene group load");
        }
    }
}
```

### Step 3: Commit

```bash
git add Assets/_Slopworks/Scripts/Core/SceneLoaderBehaviour.cs \
       Assets/_Slopworks/Scripts/Core/Bootstrap.cs
git commit -m "Add SceneLoaderBehaviour with fade transitions

MonoBehaviour wrapper for SceneLoader. Coroutine-based async scene
loading with black fade panel (CanvasGroup alpha). Bootstrap wires
initial scene group load after Core_GameManager is ready."
```

---

## Task 3: Add OnSlotChanged Callback to Inventory

The HUD hotbar needs to react to inventory changes. The existing `Inventory` class has no change notification.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Core/Inventory.cs`
- Test: `Assets/_Slopworks/Tests/Editor/EditMode/InventoryTests.cs` (add new test)

### Step 1: Write the failing test

Add to existing test file:

```csharp
[Test]
public void OnSlotChanged_fires_when_item_added()
{
    var inventory = new Inventory(9, _ => 64);
    int firedSlot = -1;
    inventory.OnSlotChanged += (index) => firedSlot = index;

    inventory.TryAdd(ItemInstance.Create("iron_scrap"), 1);

    Assert.AreNotEqual(-1, firedSlot);
}

[Test]
public void OnSlotChanged_fires_when_item_removed()
{
    var inventory = new Inventory(9, _ => 64);
    inventory.TryAdd(ItemInstance.Create("iron_scrap"), 5);
    int firedSlot = -1;
    inventory.OnSlotChanged += (index) => firedSlot = index;

    inventory.TryRemove("iron_scrap", 3);

    Assert.AreNotEqual(-1, firedSlot);
}
```

### Step 2: Run tests to verify they fail

Expected: FAIL -- `OnSlotChanged` does not exist on `Inventory`

### Step 3: Add OnSlotChanged to Inventory

Add the event field and fire it in `TryAdd`, `TryRemove`, and `Clear`:

```csharp
// In Inventory.cs, add after the SlotCount property:
public event Action<int> OnSlotChanged;
```

In `TryAdd`, after each slot modification (both stacking and empty-slot paths), add:
```csharp
OnSlotChanged?.Invoke(i);
```

In `TryRemove`, after each slot modification, add:
```csharp
OnSlotChanged?.Invoke(i);
```

In `Clear`, after zeroing each slot, add:
```csharp
OnSlotChanged?.Invoke(i);
```

Also add `using System;` at the top if not already present.

### Step 4: Run tests to verify they pass

Expected: All existing inventory tests + 2 new tests PASS

### Step 5: Commit

```bash
git add Assets/_Slopworks/Scripts/Core/Inventory.cs \
       Assets/_Slopworks/Tests/Editor/EditMode/InventoryTests.cs
git commit -m "Add OnSlotChanged event to Inventory class

Fires slot index on add, remove, and clear operations.
HUD hotbar and inventory UI will subscribe to this."
```

---

## Task 4: PlayerInventory MonoBehaviour

**Files:**
- Create: `Assets/_Slopworks/Scripts/Player/PlayerInventory.cs`

### Step 1: Create PlayerInventory

```csharp
// Assets/_Slopworks/Scripts/Player/PlayerInventory.cs
using UnityEngine;

/// <summary>
/// MonoBehaviour wrapper around Inventory. Owns a 45-slot inventory:
/// slots 0-8 = hotbar, slots 9-44 = main inventory.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public const int HotbarSlots = 9;
    public const int MainSlots = 36;
    public const int TotalSlots = HotbarSlots + MainSlots;

    private Inventory _inventory;
    private ItemRegistry _itemRegistry;
    private int _selectedHotbarIndex;

    public Inventory Inventory => _inventory;
    public int SelectedHotbarIndex => _selectedHotbarIndex;

    /// <summary>
    /// Returns the ItemSlot at the currently selected hotbar index.
    /// </summary>
    public ItemSlot SelectedSlot => _inventory.GetSlot(_selectedHotbarIndex);

    private void Awake()
    {
        _itemRegistry = FindAnyObjectByType<ItemRegistry>();
        _inventory = new Inventory(TotalSlots, GetMaxStackSize);
    }

    private int GetMaxStackSize(string definitionId)
    {
        if (_itemRegistry == null) return 64;
        var def = _itemRegistry.Get(definitionId);
        return def != null && def.isStackable ? def.maxStackSize : 1;
    }

    public bool TryAdd(ItemInstance item, int count)
    {
        bool result = _inventory.TryAdd(item, count);
        if (result)
            Debug.Log($"inventory: added {count}x {item.definitionId}");
        return result;
    }

    public bool TryRemove(string definitionId, int count)
    {
        bool result = _inventory.TryRemove(definitionId, count);
        if (result)
            Debug.Log($"inventory: removed {count}x {definitionId}");
        return result;
    }

    public void SelectHotbarSlot(int index)
    {
        if (index < 0 || index >= HotbarSlots) return;
        _selectedHotbarIndex = index;
        Debug.Log($"hotbar: selected slot {index}");
    }
}
```

### Step 2: Commit

```bash
git add Assets/_Slopworks/Scripts/Player/PlayerInventory.cs
git commit -m "Add PlayerInventory MonoBehaviour wrapper

45 slots (9 hotbar + 36 main). Wraps existing Inventory class.
Resolves max stack sizes from ItemRegistry."
```

---

## Task 5: WorldItem Pickup System

**Files:**
- Create: `Assets/_Slopworks/Scripts/Player/WorldItem.cs`
- Create: `Assets/_Slopworks/Scripts/Player/ItemPickupTrigger.cs`

### Step 1: Create WorldItem

```csharp
// Assets/_Slopworks/Scripts/Player/WorldItem.cs
using UnityEngine;

/// <summary>
/// An item sitting in the world that the player can pick up.
/// Placed on the Interactable physics layer.
/// </summary>
public class WorldItem : MonoBehaviour
{
    [SerializeField] private ItemDefinitionSO _definition;
    [SerializeField] private int _count = 1;

    public ItemDefinitionSO Definition => _definition;
    public int Count => _count;

    /// <summary>
    /// Initialize at runtime (for spawning from code).
    /// </summary>
    public void Initialize(ItemDefinitionSO definition, int count)
    {
        _definition = definition;
        _count = count;
    }

    private void Start()
    {
        gameObject.layer = PhysicsLayers.Interactable;

        // Add a trigger collider if none exists
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.5f;
        }
    }

    /// <summary>
    /// Attempt to collect this item into the given inventory.
    /// Destroys the GameObject on success.
    /// </summary>
    public bool TryCollect(PlayerInventory inventory)
    {
        if (_definition == null || _count <= 0) return false;

        var instance = ItemInstance.Create(_definition.itemId);
        if (!inventory.TryAdd(instance, _count))
            return false;

        Debug.Log($"picked up {_count}x {_definition.displayName}");
        Destroy(gameObject);
        return true;
    }
}
```

### Step 2: Create ItemPickupTrigger

```csharp
// Assets/_Slopworks/Scripts/Player/ItemPickupTrigger.cs
using UnityEngine;

/// <summary>
/// Trigger zone on the player that auto-collects WorldItems on overlap.
/// Add to the player GameObject with a trigger SphereCollider.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class ItemPickupTrigger : MonoBehaviour
{
    private PlayerInventory _inventory;

    private void Awake()
    {
        _inventory = GetComponentInParent<PlayerInventory>();

        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_inventory == null) return;

        var worldItem = other.GetComponent<WorldItem>();
        if (worldItem != null)
            worldItem.TryCollect(_inventory);
    }
}
```

### Step 3: Commit

```bash
git add Assets/_Slopworks/Scripts/Player/WorldItem.cs \
       Assets/_Slopworks/Scripts/Player/ItemPickupTrigger.cs
git commit -m "Add WorldItem pickup system

WorldItem sits on Interactable layer. ItemPickupTrigger on
the player auto-collects on trigger overlap. Items go into
PlayerInventory."
```

---

## Task 6: HUD Controller -- Health Bar + Crosshair + Interaction Prompt

**Files:**
- Create: `Assets/_Slopworks/Scripts/UI/HUDController.cs`
- Create: `Assets/_Slopworks/Scripts/UI/HealthBarUI.cs`
- Create: `Assets/_Slopworks/Scripts/UI/InteractionPromptUI.cs`

### Step 1: Create HealthBarUI

```csharp
// Assets/_Slopworks/Scripts/UI/HealthBarUI.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Health bar display using a filled Image. Subscribes to HealthComponent events.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    private Image _fillImage;
    private TextMeshProUGUI _healthText;
    private HealthComponent _health;

    public void Initialize(HealthComponent health)
    {
        _health = health;
        if (_health != null)
        {
            _health.OnDamaged += _ => UpdateDisplay();
        }
        UpdateDisplay();
    }

    public void Setup(Image fillImage, TextMeshProUGUI healthText)
    {
        _fillImage = fillImage;
        _healthText = healthText;
    }

    public void UpdateDisplay()
    {
        if (_health == null) return;

        float ratio = _health.CurrentHealth / _health.MaxHealth;

        if (_fillImage != null)
        {
            _fillImage.fillAmount = ratio;
            if (ratio > 0.5f) _fillImage.color = new Color(0.2f, 0.8f, 0.2f);
            else if (ratio > 0.2f) _fillImage.color = new Color(0.9f, 0.7f, 0.1f);
            else _fillImage.color = new Color(0.9f, 0.2f, 0.2f);
        }

        if (_healthText != null)
        {
            _healthText.text = $"{Mathf.CeilToInt(_health.CurrentHealth)}/{Mathf.CeilToInt(_health.MaxHealth)}";
        }
    }
}
```

### Step 2: Create InteractionPromptUI

```csharp
// Assets/_Slopworks/Scripts/UI/InteractionPromptUI.cs
using TMPro;
using UnityEngine;

/// <summary>
/// Shows "Press E to ..." below the crosshair when looking at an interactable.
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    private TextMeshProUGUI _promptText;
    private Camera _playerCamera;

    public void Setup(TextMeshProUGUI promptText, Camera playerCamera)
    {
        _promptText = promptText;
        _playerCamera = playerCamera;
    }

    private void Update()
    {
        if (_promptText == null || _playerCamera == null) return;

        var ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
        if (Physics.Raycast(ray, out var hit, 3f, PhysicsLayers.InteractMask))
        {
            var interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                _promptText.text = interactable.GetInteractionPrompt();
                _promptText.gameObject.SetActive(true);
                return;
            }
        }

        _promptText.gameObject.SetActive(false);
    }
}
```

### Step 3: Create HUDController

```csharp
// Assets/_Slopworks/Scripts/UI/HUDController.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Master HUD controller. Creates all HUD elements at runtime on the Canvas.
/// Wires to player systems (health, inventory, interaction).
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private HealthBehaviour _playerHealth;
    [SerializeField] private PlayerInventory _playerInventory;
    [SerializeField] private Camera _playerCamera;

    // UI components (created at runtime)
    private HealthBarUI _healthBar;
    private InteractionPromptUI _interactionPrompt;
    private Image _crosshairImage;
    private TextMeshProUGUI _buildModeText;
    private TextMeshProUGUI _waveWarningText;

    // Hotbar (created at runtime)
    private HotbarSlotUI[] _hotbarSlots;

    private void Start()
    {
        CreateCrosshair();
        CreateHealthBar();
        CreateInteractionPrompt();
        CreateBuildModeIndicator();
        CreateWaveWarning();
        CreateHotbar();

        WireReferences();
    }

    /// <summary>
    /// Runtime initialization (for playtest scenes that create the player at runtime).
    /// </summary>
    public void Initialize(HealthBehaviour health, PlayerInventory inventory, Camera cam)
    {
        _playerHealth = health;
        _playerInventory = inventory;
        _playerCamera = cam;
        WireReferences();
    }

    private void WireReferences()
    {
        if (_playerHealth != null)
            _healthBar?.Initialize(_playerHealth.Health);

        if (_playerCamera != null)
            _interactionPrompt?.Setup(_interactionPrompt.GetComponentInChildren<TextMeshProUGUI>(), _playerCamera);

        if (_playerInventory != null && _hotbarSlots != null)
        {
            for (int i = 0; i < _hotbarSlots.Length; i++)
                _hotbarSlots[i].Bind(_playerInventory, i);
        }
    }

    private void Update()
    {
        UpdateBuildModeIndicator();
        UpdateHotbarSelection();
        _healthBar?.UpdateDisplay();
    }

    public void ShowWaveWarning(string message)
    {
        if (_waveWarningText != null)
        {
            _waveWarningText.text = message;
            _waveWarningText.gameObject.SetActive(true);
            CancelInvoke(nameof(HideWaveWarning));
            Invoke(nameof(HideWaveWarning), 3f);
        }
    }

    private void HideWaveWarning()
    {
        if (_waveWarningText != null)
            _waveWarningText.gameObject.SetActive(false);
    }

    private void UpdateBuildModeIndicator()
    {
        // BuildModeController is a pure C# class, not a MonoBehaviour.
        // The BuildModeBehaviour wrapper would set this. For now, leave hidden.
        // Will be wired when build mode behaviour exposes IsInBuildMode.
    }

    private void UpdateHotbarSelection()
    {
        if (_playerInventory == null || _hotbarSlots == null) return;

        for (int i = 0; i < _hotbarSlots.Length; i++)
            _hotbarSlots[i].SetSelected(i == _playerInventory.SelectedHotbarIndex);
    }

    // ---- UI Creation Methods ----

    private void CreateCrosshair()
    {
        var obj = new GameObject("Crosshair");
        obj.transform.SetParent(transform, false);
        _crosshairImage = obj.AddComponent<Image>();
        _crosshairImage.color = new Color(1f, 1f, 1f, 0.7f);
        _crosshairImage.raycastTarget = false;
        var rect = _crosshairImage.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(4, 4);
        rect.anchoredPosition = Vector2.zero;
    }

    private void CreateHealthBar()
    {
        // Background
        var bgObj = new GameObject("HealthBarBG");
        bgObj.transform.SetParent(transform, false);
        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        bgImage.raycastTarget = false;
        var bgRect = bgImage.rectTransform;
        bgRect.anchorMin = new Vector2(0, 1);
        bgRect.anchorMax = new Vector2(0, 1);
        bgRect.pivot = new Vector2(0, 1);
        bgRect.sizeDelta = new Vector2(200, 24);
        bgRect.anchoredPosition = new Vector2(16, -16);

        // Fill
        var fillObj = new GameObject("HealthBarFill");
        fillObj.transform.SetParent(bgObj.transform, false);
        var fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.8f, 0.2f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillAmount = 1f;
        fillImage.raycastTarget = false;
        var fillRect = fillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2, 2);
        fillRect.offsetMax = new Vector2(-2, -2);

        // Text
        var textObj = new GameObject("HealthText");
        textObj.transform.SetParent(bgObj.transform, false);
        var healthText = textObj.AddComponent<TextMeshProUGUI>();
        healthText.fontSize = 14;
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.color = Color.white;
        healthText.raycastTarget = false;
        var textRect = healthText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        _healthBar = bgObj.AddComponent<HealthBarUI>();
        _healthBar.Setup(fillImage, healthText);
    }

    private void CreateInteractionPrompt()
    {
        var obj = new GameObject("InteractionPrompt");
        obj.transform.SetParent(transform, false);

        var text = obj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 16;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(400, 30);
        rect.anchoredPosition = new Vector2(0, -30);

        obj.SetActive(false);

        _interactionPrompt = obj.AddComponent<InteractionPromptUI>();
        _interactionPrompt.Setup(text, _playerCamera);
    }

    private void CreateBuildModeIndicator()
    {
        var obj = new GameObject("BuildModeIndicator");
        obj.transform.SetParent(transform, false);

        _buildModeText = obj.AddComponent<TextMeshProUGUI>();
        _buildModeText.fontSize = 18;
        _buildModeText.alignment = TextAlignmentOptions.Center;
        _buildModeText.color = new Color(1f, 0.9f, 0.3f);
        _buildModeText.text = "BUILD MODE";
        _buildModeText.raycastTarget = false;

        var rect = _buildModeText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1);
        rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(200, 30);
        rect.anchoredPosition = new Vector2(0, -16);

        obj.SetActive(false);
    }

    private void CreateWaveWarning()
    {
        var obj = new GameObject("WaveWarning");
        obj.transform.SetParent(transform, false);

        _waveWarningText = obj.AddComponent<TextMeshProUGUI>();
        _waveWarningText.fontSize = 28;
        _waveWarningText.alignment = TextAlignmentOptions.Center;
        _waveWarningText.color = new Color(1f, 0.3f, 0.3f);
        _waveWarningText.raycastTarget = false;

        var rect = _waveWarningText.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1);
        rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(600, 40);
        rect.anchoredPosition = new Vector2(0, -60);

        obj.SetActive(false);
    }

    private void CreateHotbar()
    {
        var containerObj = new GameObject("HotbarContainer");
        containerObj.transform.SetParent(transform, false);

        var containerRect = containerObj.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0);
        containerRect.anchorMax = new Vector2(0.5f, 0);
        containerRect.pivot = new Vector2(0.5f, 0);
        containerRect.sizeDelta = new Vector2(PlayerInventory.HotbarSlots * 56, 56);
        containerRect.anchoredPosition = new Vector2(0, 16);

        var layout = containerObj.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        _hotbarSlots = new HotbarSlotUI[PlayerInventory.HotbarSlots];
        for (int i = 0; i < PlayerInventory.HotbarSlots; i++)
        {
            _hotbarSlots[i] = HotbarSlotUI.Create(containerObj.transform, i);
        }
    }
}
```

### Step 4: Commit

```bash
git add Assets/_Slopworks/Scripts/UI/HUDController.cs \
       Assets/_Slopworks/Scripts/UI/HealthBarUI.cs \
       Assets/_Slopworks/Scripts/UI/InteractionPromptUI.cs
git commit -m "Add HUDController with health bar, crosshair, interaction prompt

Runtime-created uGUI elements. Health bar with fill color coding.
Interaction prompt shows on IInteractable raycast. Crosshair at
screen center. Wave warning and build mode indicator stubs."
```

---

## Task 7: HotbarSlotUI

**Files:**
- Create: `Assets/_Slopworks/Scripts/UI/HotbarSlotUI.cs`

### Step 1: Create HotbarSlotUI

```csharp
// Assets/_Slopworks/Scripts/UI/HotbarSlotUI.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI element for a single hotbar slot. Shows item icon and count.
/// Highlights when selected.
/// </summary>
public class HotbarSlotUI : MonoBehaviour
{
    private Image _background;
    private Image _iconImage;
    private TextMeshProUGUI _countText;
    private TextMeshProUGUI _keyText;
    private int _slotIndex;
    private PlayerInventory _playerInventory;
    private ItemRegistry _itemRegistry;

    private static readonly Color NormalColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
    private static readonly Color SelectedColor = new Color(0.3f, 0.5f, 0.8f, 0.9f);
    private static readonly Color EmptyIconColor = new Color(1f, 1f, 1f, 0f);

    public static HotbarSlotUI Create(Transform parent, int index)
    {
        var obj = new GameObject($"HotbarSlot_{index}");
        obj.transform.SetParent(parent, false);

        // Background
        var bg = obj.AddComponent<Image>();
        bg.color = NormalColor;
        bg.raycastTarget = false;

        // Icon
        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(obj.transform, false);
        var icon = iconObj.AddComponent<Image>();
        icon.color = EmptyIconColor;
        icon.raycastTarget = false;
        var iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.1f, 0.1f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        // Count text
        var countObj = new GameObject("Count");
        countObj.transform.SetParent(obj.transform, false);
        var countText = countObj.AddComponent<TextMeshProUGUI>();
        countText.fontSize = 12;
        countText.alignment = TextAlignmentOptions.BottomRight;
        countText.color = Color.white;
        countText.raycastTarget = false;
        var countRect = countText.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = new Vector2(2, 2);
        countRect.offsetMax = new Vector2(-2, -2);

        // Key number text
        var keyObj = new GameObject("Key");
        keyObj.transform.SetParent(obj.transform, false);
        var keyText = keyObj.AddComponent<TextMeshProUGUI>();
        keyText.fontSize = 10;
        keyText.alignment = TextAlignmentOptions.TopLeft;
        keyText.color = new Color(1f, 1f, 1f, 0.5f);
        keyText.text = (index + 1).ToString();
        keyText.raycastTarget = false;
        var keyRect = keyText.rectTransform;
        keyRect.anchorMin = Vector2.zero;
        keyRect.anchorMax = Vector2.one;
        keyRect.offsetMin = new Vector2(2, 2);
        keyRect.offsetMax = new Vector2(-2, -2);

        var slot = obj.AddComponent<HotbarSlotUI>();
        slot._background = bg;
        slot._iconImage = icon;
        slot._countText = countText;
        slot._keyText = keyText;
        slot._slotIndex = index;

        return slot;
    }

    public void Bind(PlayerInventory inventory, int slotIndex)
    {
        _playerInventory = inventory;
        _slotIndex = slotIndex;
        _itemRegistry = FindAnyObjectByType<ItemRegistry>();

        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged += OnSlotChanged;

        Refresh();
    }

    private void OnDestroy()
    {
        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged -= OnSlotChanged;
    }

    private void OnSlotChanged(int index)
    {
        if (index == _slotIndex)
            Refresh();
    }

    public void SetSelected(bool selected)
    {
        if (_background != null)
            _background.color = selected ? SelectedColor : NormalColor;
    }

    private void Refresh()
    {
        if (_playerInventory == null) return;

        var slot = _playerInventory.Inventory.GetSlot(_slotIndex);
        if (slot.IsEmpty)
        {
            _iconImage.color = EmptyIconColor;
            _iconImage.sprite = null;
            _countText.text = "";
        }
        else
        {
            var def = _itemRegistry?.Get(slot.item.definitionId);
            _iconImage.sprite = def?.icon;
            _iconImage.color = def?.icon != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            _countText.text = slot.count > 1 ? slot.count.ToString() : "";
        }
    }
}
```

### Step 2: Commit

```bash
git add Assets/_Slopworks/Scripts/UI/HotbarSlotUI.cs
git commit -m "Add HotbarSlotUI component

Shows item icon and stack count per slot. Subscribes to
Inventory.OnSlotChanged for reactive updates. Highlights
on selection. Key number label in corner."
```

---

## Task 8: Inventory UI Panel

**Files:**
- Create: `Assets/_Slopworks/Scripts/UI/InventoryUI.cs`
- Create: `Assets/_Slopworks/Scripts/UI/InventorySlotUI.cs`

### Step 1: Create InventorySlotUI

```csharp
// Assets/_Slopworks/Scripts/UI/InventorySlotUI.cs
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI element for a single inventory slot. Clickable for item movement.
/// </summary>
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    private Image _iconImage;
    private TextMeshProUGUI _countText;
    private int _slotIndex;
    private InventoryUI _parent;

    private static readonly Color EmptyIconColor = new Color(1f, 1f, 1f, 0f);

    public static InventorySlotUI Create(Transform parent, int slotIndex, InventoryUI inventoryUI)
    {
        var obj = new GameObject($"Slot_{slotIndex}");
        obj.transform.SetParent(parent, false);

        var bg = obj.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        // Icon
        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(obj.transform, false);
        var icon = iconObj.AddComponent<Image>();
        icon.color = EmptyIconColor;
        icon.raycastTarget = false;
        var iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.1f, 0.1f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        // Count
        var countObj = new GameObject("Count");
        countObj.transform.SetParent(obj.transform, false);
        var countText = countObj.AddComponent<TextMeshProUGUI>();
        countText.fontSize = 12;
        countText.alignment = TextAlignmentOptions.BottomRight;
        countText.color = Color.white;
        countText.raycastTarget = false;
        var countRect = countText.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = new Vector2(2, 2);
        countRect.offsetMax = new Vector2(-2, -2);

        var slot = obj.AddComponent<InventorySlotUI>();
        slot._iconImage = icon;
        slot._countText = countText;
        slot._slotIndex = slotIndex;
        slot._parent = inventoryUI;

        return slot;
    }

    public void Refresh(ItemSlot data, ItemRegistry registry)
    {
        if (data.IsEmpty)
        {
            _iconImage.color = EmptyIconColor;
            _iconImage.sprite = null;
            _countText.text = "";
        }
        else
        {
            var def = registry?.Get(data.item.definitionId);
            _iconImage.sprite = def?.icon;
            _iconImage.color = def?.icon != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            _countText.text = data.count > 1 ? data.count.ToString() : "";
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _parent?.OnSlotClicked(_slotIndex);
    }
}
```

### Step 2: Create InventoryUI

```csharp
// Assets/_Slopworks/Scripts/UI/InventoryUI.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full inventory panel. Toggled with Tab (InventoryOpen action).
/// Shows 36 main slots in a 9x4 grid and 9 hotbar slots below.
/// Click to pick up/place items between slots.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    private PlayerInventory _playerInventory;
    private ItemRegistry _itemRegistry;
    private InventorySlotUI[] _slots;
    private GameObject _panel;
    private SlopworksControls _controls;

    // Held item state (click slot to pick up, click another to place)
    private int _heldFromSlot = -1;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    public void Initialize(PlayerInventory inventory)
    {
        _playerInventory = inventory;
        _itemRegistry = FindAnyObjectByType<ItemRegistry>();
        _controls = new SlopworksControls();
        _controls.Exploration.Enable();

        CreatePanel();

        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged += OnSlotChanged;

        _panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_playerInventory?.Inventory != null)
            _playerInventory.Inventory.OnSlotChanged -= OnSlotChanged;
        _controls?.Dispose();
    }

    private void Update()
    {
        if (_controls != null && _controls.Exploration.InventoryOpen.WasPressedThisFrame())
            Toggle();
    }

    public void Toggle()
    {
        if (_panel == null) return;

        bool opening = !_panel.activeSelf;
        _panel.SetActive(opening);

        if (opening)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshAll();
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _heldFromSlot = -1;
        }

        Debug.Log($"inventory ui: {(opening ? "opened" : "closed")}");
    }

    public void OnSlotClicked(int slotIndex)
    {
        if (_playerInventory == null) return;

        var inventory = _playerInventory.Inventory;

        if (_heldFromSlot < 0)
        {
            // Pick up from this slot
            var slot = inventory.GetSlot(slotIndex);
            if (!slot.IsEmpty)
            {
                _heldFromSlot = slotIndex;
                Debug.Log($"inventory ui: picked up from slot {slotIndex}");
            }
        }
        else
        {
            // Place into this slot -- swap the two slots
            SwapSlots(_heldFromSlot, slotIndex);
            _heldFromSlot = -1;
        }
    }

    private void SwapSlots(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex)
        {
            _heldFromSlot = -1;
            return;
        }

        var inventory = _playerInventory.Inventory;
        var fromSlot = inventory.GetSlot(fromIndex);
        var toSlot = inventory.GetSlot(toIndex);

        // Use internal swap: clear both, then set both
        // Inventory doesn't have a direct swap method, so we use the array access
        // workaround: remove from both, add to both in swapped positions
        // For simplicity, we directly swap via reflection-free approach:
        // This requires adding a SetSlot method to Inventory.
        // For now, log the action. SetSlot will be added in a follow-up step.
        Debug.Log($"inventory ui: swapped slot {fromIndex} <-> {toIndex}");
    }

    private void OnSlotChanged(int index)
    {
        if (!IsOpen || _slots == null) return;
        if (index >= 0 && index < _slots.Length)
        {
            var slotData = _playerInventory.Inventory.GetSlot(index);
            _slots[index].Refresh(slotData, _itemRegistry);
        }
    }

    private void RefreshAll()
    {
        if (_playerInventory == null || _slots == null) return;

        for (int i = 0; i < _slots.Length; i++)
        {
            var slotData = _playerInventory.Inventory.GetSlot(i);
            _slots[i].Refresh(slotData, _itemRegistry);
        }
    }

    private void CreatePanel()
    {
        _panel = new GameObject("InventoryPanel");
        _panel.transform.SetParent(transform, false);

        // Semi-transparent background
        var panelImage = _panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        var panelRect = panelImage.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520, 360);

        // Title
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_panel.transform, false);
        var titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
        titleText.text = "Inventory";
        titleText.fontSize = 18;
        titleText.alignment = TMPro.TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.raycastTarget = false;
        var titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 30);
        titleRect.anchoredPosition = new Vector2(0, -4);

        // Grid container for all slots
        var gridObj = new GameObject("SlotGrid");
        gridObj.transform.SetParent(_panel.transform, false);
        var gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.sizeDelta = new Vector2(9 * 52 + 8 * 4, 5 * 52 + 4 * 4);
        gridRect.anchoredPosition = new Vector2(0, -10);

        var grid = gridObj.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(52, 52);
        grid.spacing = new Vector2(4, 4);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 9;

        // Create all 45 slots (0-8 hotbar, 9-44 main)
        _slots = new InventorySlotUI[PlayerInventory.TotalSlots];
        for (int i = 0; i < PlayerInventory.TotalSlots; i++)
        {
            _slots[i] = InventorySlotUI.Create(gridObj.transform, i, this);
        }
    }
}
```

### Step 3: Commit

```bash
git add Assets/_Slopworks/Scripts/UI/InventoryUI.cs \
       Assets/_Slopworks/Scripts/UI/InventorySlotUI.cs
git commit -m "Add inventory UI panel with slot grid

Full-screen panel toggled with Tab. 45 slots in 9-column grid.
Click-to-select slot movement. Subscribes to Inventory.OnSlotChanged
for reactive updates."
```

---

## Task 9: Add SetSlot to Inventory for UI Slot Swapping

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Core/Inventory.cs`
- Modify: `Assets/_Slopworks/Scripts/UI/InventoryUI.cs`
- Test: `Assets/_Slopworks/Tests/Editor/EditMode/InventoryTests.cs`

### Step 1: Write the failing test

```csharp
[Test]
public void SetSlot_replaces_contents_and_fires_event()
{
    var inventory = new Inventory(9, _ => 64);
    inventory.TryAdd(ItemInstance.Create("iron"), 5);

    int firedSlot = -1;
    inventory.OnSlotChanged += (i) => firedSlot = i;

    var newSlot = new ItemSlot { item = ItemInstance.Create("copper"), count = 3 };
    inventory.SetSlot(0, newSlot);

    Assert.AreEqual(0, firedSlot);
    Assert.AreEqual("copper", inventory.GetSlot(0).item.definitionId);
    Assert.AreEqual(3, inventory.GetSlot(0).count);
}

[Test]
public void SwapSlots_exchanges_two_slots()
{
    var inventory = new Inventory(9, _ => 64);
    inventory.TryAdd(ItemInstance.Create("iron"), 5);

    inventory.SwapSlots(0, 1);

    Assert.IsTrue(inventory.GetSlot(0).IsEmpty);
    Assert.AreEqual("iron", inventory.GetSlot(1).item.definitionId);
    Assert.AreEqual(5, inventory.GetSlot(1).count);
}
```

### Step 2: Add SetSlot and SwapSlots to Inventory

```csharp
// Add to Inventory.cs:

/// <summary>
/// Directly sets a slot's contents. Used by UI for drag-and-drop operations.
/// </summary>
public void SetSlot(int index, ItemSlot slot)
{
    if (index < 0 || index >= _slots.Length)
        throw new ArgumentOutOfRangeException(nameof(index));

    _slots[index] = slot;
    OnSlotChanged?.Invoke(index);
}

/// <summary>
/// Swaps the contents of two slots.
/// </summary>
public void SwapSlots(int indexA, int indexB)
{
    if (indexA < 0 || indexA >= _slots.Length)
        throw new ArgumentOutOfRangeException(nameof(indexA));
    if (indexB < 0 || indexB >= _slots.Length)
        throw new ArgumentOutOfRangeException(nameof(indexB));

    var temp = _slots[indexA];
    _slots[indexA] = _slots[indexB];
    _slots[indexB] = temp;

    OnSlotChanged?.Invoke(indexA);
    OnSlotChanged?.Invoke(indexB);
}
```

### Step 3: Update InventoryUI.SwapSlots to use the real method

Replace the `SwapSlots` method body in `InventoryUI.cs`:

```csharp
private void SwapSlots(int fromIndex, int toIndex)
{
    if (fromIndex == toIndex)
    {
        _heldFromSlot = -1;
        return;
    }

    _playerInventory.Inventory.SwapSlots(fromIndex, toIndex);
    Debug.Log($"inventory ui: swapped slot {fromIndex} <-> {toIndex}");
}
```

### Step 4: Run tests, verify pass

Expected: All inventory tests pass including 2 new ones.

### Step 5: Commit

```bash
git add Assets/_Slopworks/Scripts/Core/Inventory.cs \
       Assets/_Slopworks/Scripts/UI/InventoryUI.cs \
       Assets/_Slopworks/Tests/Editor/EditMode/InventoryTests.cs
git commit -m "Add SetSlot and SwapSlots to Inventory

Direct slot manipulation for UI drag-and-drop. Both methods
fire OnSlotChanged events. InventoryUI now uses SwapSlots
for click-to-move item operations."
```

---

## Task 10: Player Input Wiring for Inventory and Hotbar

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/PlayerController.cs`

### Step 1: Add hotbar and inventory input handling to PlayerController

Add to PlayerController after the existing fields:

```csharp
private PlayerInventory _playerInventory;
```

In `Awake`, add:

```csharp
_playerInventory = GetComponent<PlayerInventory>();
```

Add a new method called in `Update`:

```csharp
private void HandleHotbarInput()
{
    if (_playerInventory == null) return;

    // Number keys 1-9 for hotbar selection
    for (int i = 0; i < 9; i++)
    {
        if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1 + i))
        {
            _playerInventory.SelectHotbarSlot(i);
            break;
        }
    }
}
```

Note: Hotbar number key selection uses legacy Input.GetKeyDown since the Input Actions asset doesn't have individual number key bindings. This is acceptable for hotbar -- the main gameplay actions use the proper Input System.

Call `HandleHotbarInput()` at the end of `Update()`.

### Step 2: Commit

```bash
git add Assets/_Slopworks/Scripts/Player/PlayerController.cs
git commit -m "Wire hotbar number key input in PlayerController

Number keys 1-9 select hotbar slots via PlayerInventory."
```

---

## Task 11: Machine Interaction -- Recipe Selection UI

**Files:**
- Create: `Assets/_Slopworks/Scripts/UI/RecipeSelectionUI.cs`
- Modify: `Assets/_Slopworks/Scripts/Automation/MachineBehaviour.cs`

### Step 1: Make MachineBehaviour implement IInteractable

```csharp
// Assets/_Slopworks/Scripts/Automation/MachineBehaviour.cs
using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around the Machine simulation class.
/// Implements IInteractable for player interaction (recipe selection).
/// </summary>
public class MachineBehaviour : MonoBehaviour, IInteractable
{
    [SerializeField] private MachineDefinitionSO _definition;

    private Machine _machine;

    public Machine Machine => _machine;
    public MachineDefinitionSO Definition => _definition;

    private void Awake()
    {
        if (_definition == null)
        {
            Debug.LogError("MachineBehaviour: missing machine definition", this);
            return;
        }

        _machine = new Machine(_definition);
        gameObject.layer = PhysicsLayers.Interactable;
    }

    public string GetInteractionPrompt()
    {
        return $"press E to configure {_definition.displayName}";
    }

    public void Interact(GameObject player)
    {
        var recipeUI = FindAnyObjectByType<RecipeSelectionUI>();
        if (recipeUI != null)
        {
            recipeUI.Open(this, player.GetComponent<PlayerInventory>());
        }
    }
}
```

### Step 2: Create RecipeSelectionUI

```csharp
// Assets/_Slopworks/Scripts/UI/RecipeSelectionUI.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Recipe selection panel that opens when interacting with a machine.
/// Shows available recipes for the machine type. Player selects a recipe,
/// ingredients are checked against player inventory, and machine starts crafting.
/// </summary>
public class RecipeSelectionUI : MonoBehaviour
{
    private GameObject _panel;
    private Transform _recipeListContent;
    private MachineBehaviour _currentMachine;
    private PlayerInventory _playerInventory;
    private RecipeRegistry _recipeRegistry;
    private ItemRegistry _itemRegistry;
    private readonly List<GameObject> _recipeEntries = new();

    public bool IsOpen => _panel != null && _panel.activeSelf;

    private void Awake()
    {
        _recipeRegistry = FindAnyObjectByType<RecipeRegistry>();
        _itemRegistry = FindAnyObjectByType<ItemRegistry>();
        CreatePanel();
        _panel.SetActive(false);
    }

    private void Update()
    {
        if (IsOpen && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    public void Open(MachineBehaviour machine, PlayerInventory inventory)
    {
        _currentMachine = machine;
        _playerInventory = inventory;

        PopulateRecipes();

        _panel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log($"recipe ui: opened for {machine.Definition.displayName}");
    }

    public void Close()
    {
        _panel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _currentMachine = null;

        Debug.Log("recipe ui: closed");
    }

    private void PopulateRecipes()
    {
        // Clear existing entries
        foreach (var entry in _recipeEntries)
            Destroy(entry);
        _recipeEntries.Clear();

        if (_currentMachine == null || _recipeRegistry == null) return;

        var recipes = _recipeRegistry.GetForMachine(_currentMachine.Definition.machineType);
        foreach (var recipe in recipes)
        {
            CreateRecipeEntry(recipe);
        }
    }

    private void CreateRecipeEntry(RecipeSO recipe)
    {
        var entryObj = new GameObject($"Recipe_{recipe.recipeId}");
        entryObj.transform.SetParent(_recipeListContent, false);
        _recipeEntries.Add(entryObj);

        var bg = entryObj.AddComponent<Image>();
        bool canCraft = CanCraftRecipe(recipe);
        bg.color = canCraft ? new Color(0.2f, 0.3f, 0.2f, 0.9f) : new Color(0.3f, 0.2f, 0.2f, 0.5f);

        var layout = entryObj.AddComponent<LayoutElement>();
        layout.preferredHeight = 40;

        // Recipe name and info
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(entryObj.transform, false);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = FormatRecipeText(recipe);
        text.fontSize = 14;
        text.color = canCraft ? Color.white : new Color(0.6f, 0.6f, 0.6f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 0);
        textRect.offsetMax = new Vector2(-8, 0);

        // Click handler
        var button = entryObj.AddComponent<Button>();
        button.interactable = canCraft;
        var capturedRecipe = recipe;
        button.onClick.AddListener(() => OnRecipeSelected(capturedRecipe));
    }

    private void OnRecipeSelected(RecipeSO recipe)
    {
        if (_currentMachine == null || _playerInventory == null) return;

        // Deduct ingredients from player inventory
        foreach (var input in recipe.inputs)
        {
            if (!_playerInventory.TryRemove(input.itemId, input.count))
            {
                Debug.LogWarning($"recipe ui: failed to remove {input.count}x {input.itemId}");
                return;
            }
        }

        // Insert ingredients into machine input buffer
        for (int i = 0; i < recipe.inputs.Length; i++)
        {
            var input = recipe.inputs[i];
            _currentMachine.Machine.TryInsertInput(
                i % _currentMachine.Definition.inputBufferSize,
                ItemInstance.Create(input.itemId),
                input.count);
        }

        // Set the machine recipe
        _currentMachine.Machine.SetRecipe(recipe.recipeId);

        Debug.Log($"recipe ui: set recipe {recipe.displayName} on {_currentMachine.Definition.displayName}");
        Close();
    }

    private bool CanCraftRecipe(RecipeSO recipe)
    {
        if (_playerInventory == null || recipe.inputs == null) return false;

        foreach (var input in recipe.inputs)
        {
            if (_playerInventory.Inventory.GetCount(input.itemId) < input.count)
                return false;
        }
        return true;
    }

    private string FormatRecipeText(RecipeSO recipe)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(recipe.displayName).Append(": ");

        for (int i = 0; i < recipe.inputs.Length; i++)
        {
            if (i > 0) sb.Append(" + ");
            var def = _itemRegistry?.Get(recipe.inputs[i].itemId);
            string name = def != null ? def.displayName : recipe.inputs[i].itemId;
            sb.Append($"{recipe.inputs[i].count}x {name}");
        }

        sb.Append(" -> ");

        for (int i = 0; i < recipe.outputs.Length; i++)
        {
            if (i > 0) sb.Append(" + ");
            var def = _itemRegistry?.Get(recipe.outputs[i].itemId);
            string name = def != null ? def.displayName : recipe.outputs[i].itemId;
            sb.Append($"{recipe.outputs[i].count}x {name}");
        }

        sb.Append($" ({recipe.craftDuration}s)");
        return sb.ToString();
    }

    private void CreatePanel()
    {
        _panel = new GameObject("RecipePanel");
        _panel.transform.SetParent(transform, false);

        var panelImage = _panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        var panelRect = panelImage.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(450, 300);

        // Title
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_panel.transform, false);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Select Recipe";
        titleText.fontSize = 18;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.raycastTarget = false;
        var titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 30);
        titleRect.anchoredPosition = new Vector2(0, -4);

        // Scroll area for recipes
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(_panel.transform, false);
        var scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(8, 8);
        scrollRect.offsetMax = new Vector2(-8, -36);

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);
        _recipeListContent = contentObj.transform;

        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        var vertLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        vertLayout.spacing = 4;
        vertLayout.childControlWidth = true;
        vertLayout.childForceExpandWidth = true;
        vertLayout.childControlHeight = false;

        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
}
```

### Step 3: Commit

```bash
git add Assets/_Slopworks/Scripts/UI/RecipeSelectionUI.cs \
       Assets/_Slopworks/Scripts/Automation/MachineBehaviour.cs
git commit -m "Add recipe selection UI and machine interaction

MachineBehaviour implements IInteractable. Press E to open
recipe panel. Shows available recipes for the machine type.
Checks player inventory for ingredients, deducts on selection,
loads machine input buffer and sets active recipe."
```

---

## Task 12: Player Interaction Raycast

The player needs to fire a raycast each frame to detect IInteractable objects and trigger interaction on E press.

**Files:**
- Modify: `Assets/_Slopworks/Scripts/Player/PlayerController.cs`

### Step 1: Add interaction handling to PlayerController

Add fields:

```csharp
private IInteractable _currentInteractable;
```

Add method called in `Update`:

```csharp
private void HandleInteraction()
{
    // Raycast from camera center
    var ray = new Ray(_cameraTransform.position, _cameraTransform.forward);
    if (Physics.Raycast(ray, out var hit, 3f, PhysicsLayers.InteractMask))
    {
        _currentInteractable = hit.collider.GetComponent<IInteractable>();
    }
    else
    {
        _currentInteractable = null;
    }

    if (_currentInteractable != null && _controls.Exploration.Interact.WasPressedThisFrame())
    {
        _currentInteractable.Interact(gameObject);
    }
}
```

Call `HandleInteraction()` in `Update()`.

### Step 2: Commit

```bash
git add Assets/_Slopworks/Scripts/Player/PlayerController.cs
git commit -m "Add interaction raycast to PlayerController

Raycasts from camera each frame on InteractMask layer.
Fires IInteractable.Interact on E press."
```

---

## Task 13: Phase 5 Playtest Scene

**Files:**
- Create: `Assets/_Slopworks/Scripts/UI/Phase5PlaytestSetup.cs`

### Step 1: Create the playtest bootstrapper

```csharp
// Assets/_Slopworks/Scripts/UI/Phase5PlaytestSetup.cs
using UnityEngine;

/// <summary>
/// Phase 5 playtest. Drop on an empty GameObject, hit Play.
/// Creates: player with inventory, WorldItems to pick up, a smelter to interact with,
/// full HUD, and scene transition zone.
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
        // ItemRegistry
        var registryObj = new GameObject("Registries");
        var itemRegistry = registryObj.AddComponent<ItemRegistry>();
        // Use reflection to set the serialized array since we're creating at runtime
        var itemsField = typeof(ItemRegistry).GetField("_items",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        itemsField?.SetValue(itemRegistry, new[] { _ironScrapDef, _ironIngotDef });
        // Manually trigger Awake logic
        var awakeMethod = typeof(ItemRegistry).GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        awakeMethod?.Invoke(itemRegistry, null);

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

        // Capsule collider
        var capsule = player.AddComponent<CapsuleCollider>();
        capsule.radius = 0.3f;
        capsule.height = 1.8f;
        capsule.center = new Vector3(0, 0.9f, 0);

        // Rigidbody
        var rb = player.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Camera
        var camObj = new GameObject("PlayerCamera");
        camObj.transform.SetParent(player.transform, false);
        camObj.transform.localPosition = new Vector3(0, 1.6f, 0);
        var cam = camObj.AddComponent<Camera>();
        camObj.AddComponent<AudioListener>();

        // PlayerController
        player.AddComponent<PlayerController>();

        // Health
        var healthBehaviour = player.AddComponent<HealthBehaviour>();

        // Inventory
        var inventory = player.AddComponent<PlayerInventory>();

        // Pickup trigger (child object)
        var pickupObj = new GameObject("PickupTrigger");
        pickupObj.transform.SetParent(player.transform, false);
        pickupObj.layer = PhysicsLayers.Player;
        pickupObj.AddComponent<ItemPickupTrigger>();

        // Pre-load some items into inventory
        // Need to wait a frame for Awake to run
        StartCoroutine(PreloadInventory(inventory));

        Debug.Log("phase 5 playtest: player created at (0, 1.5, 0)");
        return player;
    }

    private System.Collections.IEnumerator PreloadInventory(PlayerInventory inventory)
    {
        yield return null; // Wait for Awake

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

            // Remove default box collider, WorldItem.Start adds a trigger sphere
            Destroy(obj.GetComponent<BoxCollider>());

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

        // Set up as interactable
        obj.layer = PhysicsLayers.Interactable;

        // Add MachineBehaviour -- need to set definition via reflection since it's runtime
        var machineBehaviour = obj.AddComponent<MachineBehaviour>();
        var defField = typeof(MachineBehaviour).GetField("_definition",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        defField?.SetValue(machineBehaviour, _smelterDef);
        // Re-trigger Awake logic
        var awakeMethod = typeof(MachineBehaviour).GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        awakeMethod?.Invoke(machineBehaviour, null);

        Debug.Log("phase 5 playtest: smelter created at (4, 0.5, 4)");
    }

    private void CreateHUD(GameObject player)
    {
        // Create HUD Canvas
        var canvasObj = new GameObject("HUDCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var hudController = canvasObj.AddComponent<HUDController>();

        // RecipeSelectionUI (needs to be on the canvas)
        canvasObj.AddComponent<RecipeSelectionUI>();

        // InventoryUI
        var inventoryUI = canvasObj.AddComponent<InventoryUI>();

        // Wire after one frame (let Awake/Start run)
        StartCoroutine(WireHUD(hudController, inventoryUI, player));
    }

    private System.Collections.IEnumerator WireHUD(HUDController hud, InventoryUI inventoryUI,
        GameObject player)
    {
        yield return null; // Wait for Awake/Start

        var health = player.GetComponent<HealthBehaviour>();
        var inventory = player.GetComponent<PlayerInventory>();
        var cam = player.GetComponentInChildren<Camera>();

        hud.Initialize(health, inventory, cam);
        inventoryUI.Initialize(inventory);

        Debug.Log("phase 5 playtest: HUD wired to player");
    }
}
```

### Step 2: Commit

```bash
git add Assets/_Slopworks/Scripts/UI/Phase5PlaytestSetup.cs
git commit -m "Add Phase 5 playtest scene bootstrapper

Creates player, world items, smelter, HUD, and inventory UI
at runtime. Tests: pickup, inventory management, hotbar selection,
machine interaction, recipe selection. All runtime-created, no
prefab dependencies."
```

---

## Task 14: Create Playtest Scene in Unity

This task requires Unity editor interaction via MCP.

### Step 1: Create the playtest scene

Use MCP Unity to create the scene and set it up:
- Create scene: `Phase5Playtest` in `Assets/_Slopworks/Scenes/`
- Add an empty GameObject named `PlaytestSetup`
- Add `Phase5PlaytestSetup` component to it
- Save scene

### Step 2: Run EditMode tests

Run all EditMode tests to verify nothing is broken:
Expected: All existing tests pass + new SceneLoader and Inventory tests pass

### Step 3: Commit

```bash
git add Assets/_Slopworks/Scenes/Phase5Playtest.unity
git commit -m "Add Phase5Playtest scene

Drop-in playtest for Phase 5 systems. Hit Play to test
inventory, HUD, pickup, and machine interaction."
```

---

## Task 15: Manual Playtest Verification

Hit Play in the Phase5Playtest scene and verify:

- [ ] Player spawns, WASD movement works, mouse look works
- [ ] Walk over brown cubes -- items auto-collect into inventory
- [ ] Hotbar at bottom shows collected items
- [ ] Number keys 1-9 switch selected hotbar slot (highlight changes)
- [ ] Tab opens inventory panel showing all 45 slots
- [ ] Click slot to pick up, click another to swap
- [ ] Tab or Escape closes inventory, cursor relocks
- [ ] Health bar in top-left shows 100/100
- [ ] Walk near orange smelter cube, see "press E to configure Smelter"
- [ ] Press E, recipe panel opens showing "Smelt Iron: 2x Iron Scrap -> 1x Iron Ingot"
- [ ] Click recipe, ingredients deducted from inventory, panel closes
- [ ] Console logs confirm all actions
- [ ] Crosshair visible at screen center

Fix any issues found during playtest. Commit fixes as needed.

### Final commit

```bash
git commit -m "Phase 5 complete: scene loader, HUD, inventory, crafting

Scene loader with ISceneService interface and fade transitions.
HUD with health bar, hotbar, crosshair, interaction prompt.
Player inventory with 45 slots, pickup, and slot management.
Machine interaction with recipe selection UI.
All EditMode tests passing. Playtest scene verified."
```

---

## Execution Notes

- **slopworks-patterns skill** must be loaded before writing any C# code
- **slopworks-architecture skill** should be consulted for any design questions
- All pure C# classes follow D-004 (no MonoBehaviour dependency, testable in EditMode)
- UI is uGUI, created at runtime (code-generated, not prefab files)
- The playtest scene follows the established pattern (PortNodePlaytestSetup, StructuralPlaytestSetup)
- Total files created: ~15 new files, ~4 modified files
- Total estimated commits: 14
