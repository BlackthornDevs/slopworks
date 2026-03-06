using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Elevator floor selection panel for the tower. Shows floor buttons colored by state
/// (cleared=green, unvisited=grey, boss=red locked/unlocked). Status text shows
/// fragment count and tier. Extract button banks loot and returns to home base.
/// </summary>
public class TowerElevatorUI : MonoBehaviour
{
    private GameObject _panel;
    private Transform _floorListContent;
    private TextMeshProUGUI _statusText;
    private readonly List<GameObject> _floorEntries = new();

    private TowerController _controller;
    private PlayerInventory _playerInventory;
    private Action<int> _onFloorSelected;
    private Action _onExtract;
    private int _openFrame = -1;

    public bool IsOpen => _panel != null && _panel.activeSelf;

    private void Awake()
    {
        CreatePanel();
        _panel.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;

        if (Time.frameCount == _openFrame) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            Close();
    }

    public void Open(TowerController controller, PlayerInventory playerInventory, Action<int> onFloorSelected, Action onExtract)
    {
        _controller = controller;
        _playerInventory = playerInventory;
        _onFloorSelected = onFloorSelected;
        _onExtract = onExtract;

        PopulateFloors();
        RefreshStatus();

        _openFrame = Time.frameCount;
        _panel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("tower elevator ui: opened");
    }

    public void Close()
    {
        _panel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _controller = null;

        Debug.Log("tower elevator ui: closed");
    }

    private int GetCarriedFragments()
    {
        if (_playerInventory == null) return 0;
        return _playerInventory.Inventory.GetCount(PlaytestContext.KeyFragment);
    }

    private void RefreshStatus()
    {
        if (_statusText == null || _controller == null) return;

        var building = _controller.CurrentBuilding;
        int required = building != null ? building.requiredFragments : 0;
        int carriedFrags = GetCarriedFragments();
        bool bossUnlocked = _controller.UnlockBoss(carriedFrags);

        _statusText.text =
            $"Tier: {_controller.CurrentTier}  |  " +
            $"Fragments: {carriedFrags} carried, {_controller.BankedFragments} banked / {required} needed\n" +
            $"Boss: {(bossUnlocked ? "<color=#4f4>UNLOCKED</color>" : "<color=#f44>LOCKED</color>")}";
    }

    private void PopulateFloors()
    {
        foreach (var entry in _floorEntries)
            Destroy(entry);
        _floorEntries.Clear();

        if (_controller == null || _controller.CurrentBuilding == null) return;

        int chunkCount = _controller.CurrentBuilding.chunks.Count;
        int bossIndex = _controller.CurrentBuilding.bossChunkIndex;

        for (int i = 0; i < chunkCount; i++)
        {
            CreateFloorEntry(i, i == bossIndex);
        }

        var contentRect = _floorListContent as RectTransform;
        if (contentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    private void CreateFloorEntry(int floorIndex, bool isBoss)
    {
        var entryObj = new GameObject($"Floor_{floorIndex}");
        entryObj.transform.SetParent(_floorListContent, false);
        _floorEntries.Add(entryObj);

        var bg = entryObj.AddComponent<Image>();
        bool cleared = _controller.IsChunkCleared(floorIndex);
        bool bossLocked = isBoss && !_controller.UnlockBoss(GetCarriedFragments());

        if (cleared)
            bg.color = new Color(0.15f, 0.45f, 0.15f, 0.9f);
        else if (isBoss)
            bg.color = bossLocked
                ? new Color(0.4f, 0.15f, 0.15f, 0.9f)
                : new Color(0.6f, 0.2f, 0.2f, 0.9f);
        else
            bg.color = new Color(0.25f, 0.25f, 0.3f, 0.9f);

        var entryRect = bg.rectTransform;
        entryRect.sizeDelta = new Vector2(0, 40);

        var layout = entryObj.AddComponent<LayoutElement>();
        layout.preferredHeight = 40;
        layout.minHeight = 40;

        string label;
        if (isBoss)
            label = bossLocked ? $"Floor {floorIndex + 1} - BOSS [LOCKED]" : $"Floor {floorIndex + 1} - BOSS";
        else if (cleared)
            label = $"Floor {floorIndex + 1} [CLEARED]";
        else
            label = $"Floor {floorIndex + 1}";

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(entryObj.transform, false);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 16;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12, 0);
        textRect.offsetMax = new Vector2(-12, 0);

        if (!bossLocked)
        {
            var button = entryObj.AddComponent<Button>();
            int capturedIndex = floorIndex;
            button.onClick.AddListener(() =>
            {
                Debug.Log($"tower elevator ui: clicked floor {capturedIndex} (display: Floor {capturedIndex + 1})");
                Close();
                _onFloorSelected?.Invoke(capturedIndex);
            });
        }
    }

    private void CreatePanel()
    {
        _panel = new GameObject("TowerElevatorPanel");
        _panel.transform.SetParent(transform, false);

        var panelImage = _panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        var panelRect = panelImage.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(400, 500);

        // Title
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_panel.transform, false);
        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "Tower Elevator";
        titleText.fontSize = 20;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        titleText.raycastTarget = false;
        var titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 30);
        titleRect.anchoredPosition = new Vector2(0, -4);

        // Close button
        CreateCloseButton(_panel.transform);

        // Status area
        var statusObj = new GameObject("Status");
        statusObj.transform.SetParent(_panel.transform, false);
        var statusBg = statusObj.AddComponent<Image>();
        statusBg.color = new Color(0.12f, 0.12f, 0.16f, 0.9f);
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

        // Floor list scroll area
        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(_panel.transform, false);
        var scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(8, 56);
        scrollRect.offsetMax = new Vector2(-8, -88);

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        _floorListContent = contentObj.transform;
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

        // Extract button at bottom
        CreateExtractButton(_panel.transform);
    }

    private void CreateExtractButton(Transform parent)
    {
        var btnObj = new GameObject("ExtractButton");
        btnObj.transform.SetParent(parent, false);

        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.15f, 0.5f, 0.15f, 0.9f);

        var btnRect = btnImage.rectTransform;
        btnRect.anchorMin = new Vector2(0, 0);
        btnRect.anchorMax = new Vector2(1, 0);
        btnRect.pivot = new Vector2(0.5f, 0);
        btnRect.sizeDelta = new Vector2(0, 44);
        btnRect.anchoredPosition = new Vector2(0, 8);
        btnRect.offsetMin = new Vector2(8, 8);
        btnRect.offsetMax = new Vector2(-8, 52);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "EXTRACT (bank loot and return)";
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
        btn.onClick.AddListener(() =>
        {
            Close();
            _onExtract?.Invoke();
        });
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
