using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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

    private int _openFrame = -1;

    // Live status display
    private TextMeshProUGUI _statusText;
    private TextMeshProUGUI _titleText;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    private void Awake()
    {
        _recipeRegistry = FindAnyObjectByType<RecipeRegistry>();
        _itemRegistry = FindAnyObjectByType<ItemRegistry>();
        Debug.Log($"recipe ui: awake (registry={_recipeRegistry != null}, itemRegistry={_itemRegistry != null})");
        CreatePanel();
        _panel.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;

        RefreshStatus();

        if (Time.frameCount == _openFrame) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.eKey.wasPressedThisFrame)
            Close();
    }

    private void RefreshStatus()
    {
        if (_statusText == null || _currentMachine == null) return;
        var m = _currentMachine.Machine;
        if (m == null) return;

        var sb = new System.Text.StringBuilder();
        sb.Append($"Status: <b>{m.Status}</b>");
        if (m.ActiveRecipeId != null)
        {
            var activeRecipe = _recipeRegistry?.Get(m.ActiveRecipeId);
            float pct = activeRecipe != null && activeRecipe.craftDuration > 0
                ? m.CraftProgress / activeRecipe.craftDuration
                : 0f;
            sb.Append($"  |  Progress: {pct:P0}");
        }

        int inputSlots = _currentMachine.Definition.inputBufferSize;
        int outputSlots = _currentMachine.Definition.outputBufferSize;

        sb.Append("\nInput: ");
        for (int i = 0; i < inputSlots; i++)
        {
            if (i > 0) sb.Append(", ");
            var slot = m.GetInput(i);
            sb.Append(slot.IsEmpty ? "empty" : $"{slot.item.definitionId} x{slot.count}");
        }

        sb.Append("  |  Output: ");
        for (int i = 0; i < outputSlots; i++)
        {
            if (i > 0) sb.Append(", ");
            var slot = m.GetOutput(i);
            sb.Append(slot.IsEmpty ? "empty" : $"{slot.item.definitionId} x{slot.count}");
        }

        _statusText.text = sb.ToString();
    }

    public void Open(MachineBehaviour machine, PlayerInventory inventory)
    {
        _currentMachine = machine;
        _playerInventory = inventory;

        if (_titleText != null)
            _titleText.text = machine.Definition.displayName;

        PopulateRecipes();
        RefreshStatus();

        _openFrame = Time.frameCount;
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
        foreach (var entry in _recipeEntries)
            Destroy(entry);
        _recipeEntries.Clear();

        if (_currentMachine == null || _recipeRegistry == null)
        {
            Debug.Log($"recipe ui: populate skipped (machine={_currentMachine != null}, registry={_recipeRegistry != null})");
            return;
        }

        var machineType = _currentMachine.Definition.machineType;
        var recipes = _recipeRegistry.GetForMachine(machineType);
        Debug.Log($"recipe ui: found {recipes.Count} recipes for machine type '{machineType}'");

        foreach (var recipe in recipes)
        {
            Debug.Log($"recipe ui: adding entry '{recipe.displayName}'");
            CreateRecipeEntry(recipe);
        }

        // Force layout rebuild -- VerticalLayoutGroup doesn't auto-rebuild for runtime children
        var contentRect = _recipeListContent as RectTransform;
        if (contentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    private void CreateRecipeEntry(RecipeSO recipe)
    {
        var entryObj = new GameObject($"Recipe_{recipe.recipeId}");
        entryObj.transform.SetParent(_recipeListContent, false);
        _recipeEntries.Add(entryObj);

        var bg = entryObj.AddComponent<Image>();
        bool isActive = _currentMachine.Machine.ActiveRecipeId == recipe.recipeId;
        bg.color = isActive ? new Color(0.2f, 0.4f, 0.5f, 0.9f)
            : new Color(0.2f, 0.3f, 0.2f, 0.9f);

        var entryRect = bg.rectTransform;
        entryRect.sizeDelta = new Vector2(0, 60);

        var layout = entryObj.AddComponent<LayoutElement>();
        layout.preferredHeight = 60;
        layout.minHeight = 60;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(entryObj.transform, false);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = FormatRecipeText(recipe, isActive);
        text.fontSize = 13;
        text.richText = true;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 0);
        textRect.offsetMax = new Vector2(-8, 0);

        var button = entryObj.AddComponent<Button>();
        var capturedRecipe = recipe;
        button.onClick.AddListener(() => OnRecipeSelected(capturedRecipe));
    }

    private void OnRecipeSelected(RecipeSO recipe)
    {
        if (_currentMachine == null) return;

        _currentMachine.Machine.SetRecipe(recipe.recipeId);
        Debug.Log($"recipe ui: set recipe '{recipe.displayName}' on '{_currentMachine.Definition.displayName}'");
        Close();
    }

    private string FormatRecipeText(RecipeSO recipe, bool isActive = false)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<b>").Append(recipe.displayName).Append("</b>");
        if (isActive) sb.Append(" <color=#6cf>[active]</color>");
        sb.Append('\n');

        if (recipe.inputs != null)
        {
            sb.Append("In: ");
            for (int i = 0; i < recipe.inputs.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                var def = _itemRegistry?.Get(recipe.inputs[i].itemId);
                string name = def != null ? def.displayName : recipe.inputs[i].itemId;
                sb.Append($"{recipe.inputs[i].count}x {name}");
            }
        }

        if (recipe.outputs != null)
        {
            sb.Append("\nOut: ");
            for (int i = 0; i < recipe.outputs.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                var def = _itemRegistry?.Get(recipe.outputs[i].itemId);
                string name = def != null ? def.displayName : recipe.outputs[i].itemId;
                sb.Append($"{recipe.outputs[i].count}x {name}");
            }
            sb.Append($"  <color=#888>({recipe.craftDuration}s)</color>");
        }

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
        panelRect.sizeDelta = new Vector2(450, 350);

        // Title
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_panel.transform, false);
        _titleText = titleObj.AddComponent<TextMeshProUGUI>();
        _titleText.text = "Select Recipe";
        _titleText.fontSize = 18;
        _titleText.alignment = TextAlignmentOptions.Center;
        _titleText.color = Color.white;
        _titleText.raycastTarget = false;
        var titleRect = _titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 30);
        titleRect.anchoredPosition = new Vector2(0, -4);

        // Close button
        CreateCloseButton(_panel.transform);

        // Machine status area (between title and recipe list)
        var statusObj = new GameObject("Status");
        statusObj.transform.SetParent(_panel.transform, false);
        var statusBg = statusObj.AddComponent<Image>();
        statusBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        statusBg.raycastTarget = false;
        var statusBgRect = statusBg.rectTransform;
        statusBgRect.anchorMin = new Vector2(0, 1);
        statusBgRect.anchorMax = new Vector2(1, 1);
        statusBgRect.pivot = new Vector2(0.5f, 1);
        statusBgRect.sizeDelta = new Vector2(0, 50);
        statusBgRect.anchoredPosition = new Vector2(0, -34);
        statusBgRect.offsetMin = new Vector2(8, 0);
        statusBgRect.offsetMax = new Vector2(-8, 0);

        var statusTextObj = new GameObject("StatusText");
        statusTextObj.transform.SetParent(statusObj.transform, false);
        _statusText = statusTextObj.AddComponent<TextMeshProUGUI>();
        _statusText.text = "";
        _statusText.fontSize = 12;
        _statusText.richText = true;
        _statusText.color = new Color(0.8f, 0.9f, 0.8f);
        _statusText.alignment = TextAlignmentOptions.MidlineLeft;
        _statusText.raycastTarget = false;
        var statusTextRect = _statusText.rectTransform;
        statusTextRect.anchorMin = Vector2.zero;
        statusTextRect.anchorMax = Vector2.one;
        statusTextRect.offsetMin = new Vector2(8, 2);
        statusTextRect.offsetMax = new Vector2(-8, -2);

        // Content area for recipe list (below status)
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(_panel.transform, false);
        var scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(8, 8);
        scrollRect.offsetMax = new Vector2(-8, -88);

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);

        var contentRect = contentObj.AddComponent<RectTransform>();
        _recipeListContent = contentObj.transform;
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        var vertLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        vertLayout.spacing = 4;
        vertLayout.childControlWidth = true;
        vertLayout.childForceExpandWidth = true;
        vertLayout.childControlHeight = true;

        var csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void CreateCloseButton(Transform parent)
    {
        var btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(parent, false);

        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.6f, 0.15f, 0.15f, 0.9f);

        var btnRect = btnImage.rectTransform;
        btnRect.anchorMin = new Vector2(1, 1);
        btnRect.anchorMax = new Vector2(1, 1);
        btnRect.pivot = new Vector2(1, 1);
        btnRect.sizeDelta = new Vector2(28, 28);
        btnRect.anchoredPosition = new Vector2(-4, -4);

        var textObj = new GameObject("X");
        textObj.transform.SetParent(btnObj.transform, false);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "X";
        text.fontSize = 16;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(Close);
    }
}
