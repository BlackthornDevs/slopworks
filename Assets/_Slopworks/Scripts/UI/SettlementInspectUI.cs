using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SettlementInspectUI : MonoBehaviour
{
    private SettlementBuilding _building;
    private GameObject _panel;
    private Text _titleText;
    private Text _statusText;
    private Text _requirementsText;
    private Button _actionButton;
    private Text _actionButtonText;
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        CreateUI();
        _panel.SetActive(false);
    }

    public void Open(SettlementBuilding building)
    {
        _building = building;
        _isOpen = true;
        _panel.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        _building = null;
        _isOpen = false;
        _panel.SetActive(false);
    }

    private void Update()
    {
        if (!_isOpen) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            Close();
    }

    public void Refresh()
    {
        if (_building == null) return;

        _titleText.text = _building.Definition.displayName;

        if (!_building.IsClaimed)
        {
            _statusText.text = $"Repair progress: {_building.RepairLevel} / {_building.Definition.MaxRepairLevel}";
            var reqs = _building.GetRepairRequirements();
            _requirementsText.text = FormatRequirements("Materials needed:", reqs);
            _actionButtonText.text = "Deliver materials";
            _actionButton.gameObject.SetActive(true);
            _actionButton.onClick.RemoveAllListeners();
            _actionButton.onClick.AddListener(OnDeliverRepairMaterials);
        }
        else if (_building.UpgradeTier < (_building.Definition.upgradeTiers?.Length ?? 0))
        {
            _statusText.text = $"Claimed | Tier {_building.UpgradeTier} | Workers: {_building.WorkerCount}/{_building.MaxWorkerSlots}";
            var reqs = _building.GetUpgradeRequirements();
            _requirementsText.text = FormatRequirements("Upgrade materials:", reqs);
            _actionButtonText.text = "Upgrade";
            _actionButton.gameObject.SetActive(true);
            _actionButton.onClick.RemoveAllListeners();
            _actionButton.onClick.AddListener(OnDeliverUpgradeMaterials);
        }
        else
        {
            _statusText.text = $"Fully upgraded | Workers: {_building.WorkerCount}/{_building.MaxWorkerSlots}";
            _requirementsText.text = "All upgrades complete.";
            _actionButton.gameObject.SetActive(false);
        }
    }

    private string FormatRequirements(string header, (string itemId, int amount)[] reqs)
    {
        if (reqs.Length == 0) return "";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(header);
        foreach (var (itemId, amount) in reqs)
            sb.AppendLine($"  {itemId}: {amount}");
        return sb.ToString();
    }

    private void OnDeliverRepairMaterials()
    {
        // TODO: check player inventory, deduct materials, advance repair
        // for now, just advance directly for testing
        _building.AdvanceRepair();
        Refresh();
        Debug.Log($"settlement ui: delivered repair materials to {_building.BuildingId}");
    }

    private void OnDeliverUpgradeMaterials()
    {
        // TODO: check player inventory, deduct materials, advance upgrade
        _building.AdvanceUpgrade();
        Refresh();
        Debug.Log($"settlement ui: delivered upgrade materials to {_building.BuildingId}");
    }

    private void CreateUI()
    {
        // canvas
        var canvasGo = new GameObject("SettlementInspectCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // panel background
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(canvasGo.transform, false);
        var panelRect = _panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.3f, 0.2f);
        panelRect.anchorMax = new Vector2(0.7f, 0.8f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImage = _panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);

        // title
        _titleText = CreateText(_panel.transform, "Title", 24, TextAnchor.UpperCenter,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, -20), new Vector2(-20, -60));

        // status
        _statusText = CreateText(_panel.transform, "Status", 16, TextAnchor.UpperLeft,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, -70), new Vector2(-20, -100));

        // requirements
        _requirementsText = CreateText(_panel.transform, "Requirements", 14, TextAnchor.UpperLeft,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, -110), new Vector2(-20, -250));

        // action button
        var btnGo = new GameObject("ActionButton");
        btnGo.transform.SetParent(_panel.transform, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.3f, 0f);
        btnRect.anchorMax = new Vector2(0.7f, 0f);
        btnRect.offsetMin = new Vector2(0, 20);
        btnRect.offsetMax = new Vector2(0, 60);
        var btnImage = btnGo.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.5f, 0.3f, 1f);
        _actionButton = btnGo.AddComponent<Button>();

        _actionButtonText = CreateText(btnGo.transform, "ButtonText", 16, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }
}
