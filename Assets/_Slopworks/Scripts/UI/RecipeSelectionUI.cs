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
        foreach (var entry in _recipeEntries)
            Destroy(entry);
        _recipeEntries.Clear();

        if (_currentMachine == null || _recipeRegistry == null) return;

        var recipes = _recipeRegistry.GetForMachine(_currentMachine.Definition.machineType);
        foreach (var recipe in recipes)
            CreateRecipeEntry(recipe);
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

        var button = entryObj.AddComponent<Button>();
        button.interactable = canCraft;
        var capturedRecipe = recipe;
        button.onClick.AddListener(() => OnRecipeSelected(capturedRecipe));
    }

    private void OnRecipeSelected(RecipeSO recipe)
    {
        if (_currentMachine == null || _playerInventory == null) return;

        foreach (var input in recipe.inputs)
        {
            if (!_playerInventory.TryRemove(input.itemId, input.count))
            {
                Debug.LogWarning($"recipe ui: failed to remove {input.count}x {input.itemId}");
                return;
            }
        }

        for (int i = 0; i < recipe.inputs.Length; i++)
        {
            var input = recipe.inputs[i];
            _currentMachine.Machine.TryInsertInput(
                i % _currentMachine.Definition.inputBufferSize,
                ItemInstance.Create(input.itemId),
                input.count);
        }

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

        if (recipe.inputs != null)
        {
            for (int i = 0; i < recipe.inputs.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                var def = _itemRegistry?.Get(recipe.inputs[i].itemId);
                string name = def != null ? def.displayName : recipe.inputs[i].itemId;
                sb.Append($"{recipe.inputs[i].count}x {name}");
            }
        }

        sb.Append(" -> ");

        if (recipe.outputs != null)
        {
            for (int i = 0; i < recipe.outputs.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                var def = _itemRegistry?.Get(recipe.outputs[i].itemId);
                string name = def != null ? def.displayName : recipe.outputs[i].itemId;
                sb.Append($"{recipe.outputs[i].count}x {name}");
            }
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

        // Content area for recipe list
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
