using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OnGUI overlay for the overworld map. Opens with M key.
/// Shows nodes (home base, buildings, tower) and supply line connections.
/// Pure OnGUI for speed of implementation -- no Canvas hierarchy.
/// </summary>
public class OverworldMapUI : MonoBehaviour
{
    private OverworldMap _map;
    private SupplyLineManager _supplyLineManager;
    private Func<float> _towerPowerQuery;

    private bool _isOpen;
    private string _selectedNodeId;

    // Map area dimensions
    private const float MapWidth = 600f;
    private const float MapHeight = 400f;
    private const float NodeSize = 20f;
    private const float InfoPanelWidth = 200f;
    private const float InfoPanelHeight = 160f;

    public bool IsOpen => _isOpen;

    public void Initialize(OverworldMap map, SupplyLineManager supplyLineManager,
        Func<float> towerPowerQuery)
    {
        _map = map;
        _supplyLineManager = supplyLineManager;
        _towerPowerQuery = towerPowerQuery;
    }

    public void Toggle()
    {
        _isOpen = !_isOpen;
        if (!_isOpen)
            _selectedNodeId = null;
    }

    private void OnGUI()
    {
        if (!_isOpen || _map == null) return;

        // Center the map on screen
        float mapX = (Screen.width - MapWidth) / 2f;
        float mapY = (Screen.height - MapHeight) / 2f;

        // Background
        GUI.Box(new Rect(mapX - 10, mapY - 30, MapWidth + 20, MapHeight + 70), "OVERWORLD MAP");

        // Draw supply lines first (behind nodes)
        DrawSupplyLines(mapX, mapY);

        // Draw nodes
        foreach (var node in _map.GetNodes())
        {
            DrawNode(node, mapX, mapY);
        }

        // Draw selected node info panel
        if (_selectedNodeId != null)
        {
            var selected = _map.GetNode(_selectedNodeId);
            if (selected != null)
                DrawInfoPanel(selected, mapX, mapY);
        }

        // Supply dock summary at bottom
        DrawSupplySummary(mapX, mapY);

        // Controls hint
        GUI.Label(new Rect(mapX, mapY + MapHeight + 30, MapWidth, 20),
            "[Click] Select node  |  [Escape] Close map");
    }

    private void DrawNode(OverworldNode node, float mapX, float mapY)
    {
        float nx = mapX + node.MapX * MapWidth;
        float ny = mapY + (1f - node.MapY) * MapHeight; // flip Y for screen coords
        var rect = new Rect(nx - NodeSize / 2, ny - NodeSize / 2, NodeSize, NodeSize);

        // Click detection
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            _selectedNodeId = node.NodeId;
            Event.current.Use();
        }

        // Color and shape based on type
        var oldColor = GUI.color;
        bool isSelected = _selectedNodeId == node.NodeId;

        switch (node.NodeType)
        {
            case OverworldNodeType.HomeBase:
                GUI.color = isSelected ? Color.white : Color.green;
                GUI.Box(rect, "H");
                break;

            case OverworldNodeType.Building:
                GUI.color = node.IsActive
                    ? (isSelected ? Color.white : Color.green)
                    : (isSelected ? new Color(1f, 0.7f, 0.7f) : Color.red);
                GUI.Box(rect, "B");
                break;

            case OverworldNodeType.Tower:
                GUI.color = node.IsActive
                    ? (isSelected ? Color.white : Color.yellow)
                    : (isSelected ? new Color(1f, 1f, 0.7f) : new Color(0.5f, 0.5f, 0f));
                GUI.Box(rect, "T");
                break;
        }

        GUI.color = oldColor;

        // Label below node
        var labelRect = new Rect(nx - 50, ny + NodeSize / 2 + 2, 100, 20);
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 10 };
        GUI.Label(labelRect, node.DisplayName, style);
    }

    private void DrawSupplyLines(float mapX, float mapY)
    {
        if (_supplyLineManager == null) return;

        // OnGUI can't draw arbitrary lines easily, so draw dashed representation
        // using small boxes along the path
        var nodes = _map.GetNodes();
        var nodePositions = new Dictionary<string, Vector2>();

        foreach (var node in nodes)
        {
            float nx = mapX + node.MapX * MapWidth;
            float ny = mapY + (1f - node.MapY) * MapHeight;
            nodePositions[node.NodeId] = new Vector2(nx, ny);
        }

        // Find home base position (destination for all supply lines)
        if (!nodePositions.TryGetValue("home_base", out var homePos))
            return;

        var oldColor = GUI.color;
        GUI.color = new Color(0.5f, 1f, 0.5f, 0.6f);

        foreach (var node in nodes)
        {
            if (node.NodeType != OverworldNodeType.Building) continue;
            if (!node.IsActive) continue;
            if (!nodePositions.TryGetValue(node.NodeId, out var buildingPos)) continue;

            // Draw dashed line as small boxes
            int steps = 10;
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                float px = Mathf.Lerp(buildingPos.x, homePos.x, t);
                float py = Mathf.Lerp(buildingPos.y, homePos.y, t);
                GUI.Box(new Rect(px - 2, py - 2, 4, 4), GUIContent.none);
            }
        }

        GUI.color = oldColor;
    }

    private void DrawInfoPanel(OverworldNode node, float mapX, float mapY)
    {
        float panelX = mapX + MapWidth + 15;
        float panelY = mapY;

        // If panel would go off-screen, put it on the left
        if (panelX + InfoPanelWidth > Screen.width)
            panelX = mapX - InfoPanelWidth - 15;

        GUI.Box(new Rect(panelX, panelY, InfoPanelWidth, InfoPanelHeight), node.DisplayName);

        float ly = panelY + 20;
        float lh = 18;
        float lx = panelX + 5;
        float lw = InfoPanelWidth - 10;

        // Type
        GUI.Label(new Rect(lx, ly, lw, lh), $"Type: {node.NodeType}");
        ly += lh;

        // Status
        string status = node.IsActive ? "Active" : "Inactive";
        GUI.Label(new Rect(lx, ly, lw, lh), $"Status: {status}");
        ly += lh;

        // Building-specific info
        if (node.NodeType == OverworldNodeType.Building && node.BuildingState != null)
        {
            var bs = node.BuildingState;
            string claimStatus = bs.IsClaimed ? "Claimed" : $"MEP: {bs.RestoredCount}/{bs.RequiredMEPCount}";
            GUI.Label(new Rect(lx, ly, lw, lh), claimStatus);
            ly += lh;

            // Supply line info
            if (_supplyLineManager != null && bs.IsClaimed)
            {
                var lines = _supplyLineManager.GetLinesForSource(bs.BuildingId);
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    GUI.Label(new Rect(lx, ly, lw, lh),
                        $"Line {i + 1}: {line.InFlightCount} in transit");
                    ly += lh;
                    GUI.Label(new Rect(lx, ly, lw, lh),
                        $"  Delivered: {line.TotalDelivered}");
                    ly += lh;
                }
            }
        }

        // Tower-specific info
        if (node.NodeType == OverworldNodeType.Tower && _towerPowerQuery != null)
        {
            float power = _towerPowerQuery();
            GUI.Label(new Rect(lx, ly, lw, lh), $"Power: {power:F0}%");
        }
    }

    private void DrawSupplySummary(float mapX, float mapY)
    {
        if (_supplyLineManager == null) return;

        float summaryY = mapY + MapHeight + 5;
        string summary = $"Supply lines: {_supplyLineManager.LineCount}" +
                         $"  |  In transit: {_supplyLineManager.TotalInFlight}" +
                         $"  |  Total delivered: {_supplyLineManager.TotalDelivered}";
        GUI.Label(new Rect(mapX, summaryY, MapWidth, 20), summary);
    }
}
