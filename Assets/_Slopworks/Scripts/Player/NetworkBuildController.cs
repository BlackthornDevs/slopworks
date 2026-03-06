using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkBuildController : NetworkBehaviour
{
    [SerializeField] private float _placementRange = 50f;

    private enum BuildTool { Foundation, Wall, Ramp, Delete }

    private Camera _camera;
    private bool _buildMode;
    private BuildTool _currentTool;
    private GameObject _ghost;
    private Vector2Int _lastCell;
    private int _lastLevel;
    private Vector2Int _lastEdgeDir;
    private bool _lastValid;

    private static readonly int StructuralMask =
        (1 << PhysicsLayers.Terrain) | (1 << PhysicsLayers.Structures);

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner)
        {
            enabled = false;
            return;
        }
        _camera = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            _buildMode = !_buildMode;
            Debug.Log($"build: mode {(_buildMode ? "ON" : "OFF")}");
            if (!_buildMode) DestroyGhost();
        }

        if (!_buildMode) return;

        // Tool switching: 1-4
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SwitchTool(BuildTool.Foundation);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SwitchTool(BuildTool.Wall);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) SwitchTool(BuildTool.Ramp);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) SwitchTool(BuildTool.Delete);

        UpdatePreview();

        if (Mouse.current.leftButton.wasPressedThisFrame && _lastValid)
        {
            var gm = GridManager.Instance;
            switch (_currentTool)
            {
                case BuildTool.Foundation:
                    gm.CmdPlaceFoundation(_lastCell, _lastLevel);
                    break;
                case BuildTool.Wall:
                    gm.CmdPlaceWall(_lastCell, _lastLevel, _lastEdgeDir);
                    break;
                case BuildTool.Ramp:
                    gm.CmdPlaceRamp(_lastCell, _lastLevel, _lastEdgeDir);
                    break;
            }
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            var gm = GridManager.Instance;
            switch (_currentTool)
            {
                case BuildTool.Foundation:
                    gm.CmdRemoveFoundation(_lastCell, _lastLevel);
                    break;
                case BuildTool.Wall:
                    gm.CmdRemoveWall(_lastCell, _lastLevel, _lastEdgeDir);
                    break;
                case BuildTool.Ramp:
                    gm.CmdRemoveRamp(_lastCell, _lastLevel, _lastEdgeDir);
                    break;
                case BuildTool.Delete:
                    gm.CmdRemoveFoundation(_lastCell, _lastLevel);
                    break;
            }
        }
    }

    private void SwitchTool(BuildTool tool)
    {
        if (_currentTool == tool) return;
        _currentTool = tool;
        DestroyGhost();
        Debug.Log($"build: tool = {tool}");
    }

    private void UpdatePreview()
    {
        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (!Physics.Raycast(ray, out var hit, _placementRange, StructuralMask))
        {
            if (_ghost != null) _ghost.SetActive(false);
            _lastValid = false;
            return;
        }

        var grid = GridManager.Instance.Grid;
        var cell = grid.WorldToCell(hit.point);
        int level = Mathf.RoundToInt(hit.point.y / FactoryGrid.LevelHeight);
        level = Mathf.Clamp(level, 0, FactoryGrid.MaxLevels - 1);

        _lastCell = cell;
        _lastLevel = level;

        // Detect edge direction for wall/ramp tools
        if (_currentTool == BuildTool.Wall || _currentTool == BuildTool.Ramp)
        {
            _lastEdgeDir = GetEdgeDirection(hit.point, cell);
        }

        switch (_currentTool)
        {
            case BuildTool.Foundation:
                UpdateFoundationGhost(grid, cell, level);
                break;
            case BuildTool.Wall:
                UpdateWallGhost(grid, cell, level);
                break;
            case BuildTool.Ramp:
                UpdateRampGhost(grid, cell, level);
                break;
            case BuildTool.Delete:
                UpdateDeleteGhost(grid, cell, level);
                break;
        }
    }

    private Vector2Int GetEdgeDirection(Vector3 hitPoint, Vector2Int cell)
    {
        Vector3 cellCenter = GridManager.Instance.Grid.CellToWorld(cell, 0);
        float dx = hitPoint.x - cellCenter.x;
        float dz = hitPoint.z - cellCenter.z;

        if (Mathf.Abs(dx) > Mathf.Abs(dz))
            return dx > 0 ? Vector2Int.right : Vector2Int.left;
        return dz > 0 ? Vector2Int.up : Vector2Int.down;
    }

    private void UpdateFoundationGhost(FactoryGrid grid, Vector2Int cell, int level)
    {
        bool occupied = grid.GetAt(cell, level) != null;
        _lastValid = !occupied;

        EnsureGhost(new Vector3(FactoryGrid.CellSize, 0.1f, FactoryGrid.CellSize));

        _ghost.transform.position = grid.CellToWorld(cell, level);
        _ghost.transform.rotation = Quaternion.identity;
        _ghost.SetActive(true);

        SetGhostColor(_lastValid ? ValidColor : (occupied ? OccupiedColor : InvalidColor));
    }

    private void UpdateWallGhost(FactoryGrid grid, Vector2Int cell, int level)
    {
        var foundation = grid.GetAt(cell, level);
        bool hasFoundation = foundation != null && foundation.IsStructural;
        bool wallExists = hasFoundation && GridManager.Instance.HasWallAt(cell, level, _lastEdgeDir);
        _lastValid = hasFoundation && !wallExists;

        float wallHeight = FactoryGrid.LevelHeight;
        float wallThickness = 0.1f;
        EnsureGhost(new Vector3(FactoryGrid.CellSize, wallHeight, wallThickness));

        Vector3 cellCenter = grid.CellToWorld(cell, level);
        float halfCell = FactoryGrid.CellSize * 0.5f;
        Vector3 wallPos = cellCenter
            + new Vector3(_lastEdgeDir.x * halfCell, wallHeight * 0.5f, _lastEdgeDir.y * halfCell);

        _ghost.transform.position = wallPos;
        _ghost.transform.rotation = (_lastEdgeDir == Vector2Int.left || _lastEdgeDir == Vector2Int.right)
            ? Quaternion.Euler(0f, 90f, 0f)
            : Quaternion.identity;
        _ghost.SetActive(true);

        SetGhostColor(_lastValid ? ValidColor : (wallExists ? OccupiedColor : InvalidColor));
    }

    private void UpdateRampGhost(FactoryGrid grid, Vector2Int cell, int level)
    {
        var foundation = grid.GetAt(cell, level);
        bool hasFoundation = foundation != null && foundation.IsStructural;
        bool rampExists = hasFoundation && GridManager.Instance.HasRampAt(cell, level, _lastEdgeDir);
        _lastValid = hasFoundation && !rampExists && level + 1 < FactoryGrid.MaxLevels;

        float rampLength = 3f * FactoryGrid.CellSize;
        float slopeLength = Mathf.Sqrt(rampLength * rampLength + FactoryGrid.LevelHeight * FactoryGrid.LevelHeight);
        EnsureGhost(new Vector3(FactoryGrid.CellSize, 0.1f, slopeLength));

        float yAngle = 0f;
        if (_lastEdgeDir == Vector2Int.right) yAngle = 90f;
        else if (_lastEdgeDir == Vector2Int.down) yAngle = 180f;
        else if (_lastEdgeDir == Vector2Int.left) yAngle = 270f;

        float pitch = Mathf.Atan2(FactoryGrid.LevelHeight, rampLength) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(-pitch, yAngle, 0f);

        // Position so base edge sits at foundation edge at ground level
        Vector3 cellCenter = grid.CellToWorld(cell, level);
        float halfCell = FactoryGrid.CellSize * 0.5f;
        Vector3 baseEdge = cellCenter + new Vector3(_lastEdgeDir.x * halfCell, 0f, _lastEdgeDir.y * halfCell);

        // Offset from base edge to center of tilted slab along the slope direction
        Vector3 localForward = rot * Vector3.forward;
        Vector3 rampCenter = baseEdge + localForward * (slopeLength * 0.5f);

        _ghost.transform.position = rampCenter;
        _ghost.transform.rotation = rot;
        _ghost.SetActive(true);

        SetGhostColor(_lastValid ? ValidColor : (rampExists ? OccupiedColor : InvalidColor));
    }

    private void UpdateDeleteGhost(FactoryGrid grid, Vector2Int cell, int level)
    {
        bool occupied = grid.GetAt(cell, level) != null;
        _lastValid = occupied;

        EnsureGhost(new Vector3(FactoryGrid.CellSize, 0.15f, FactoryGrid.CellSize));

        _ghost.transform.position = grid.CellToWorld(cell, level);
        _ghost.transform.rotation = Quaternion.identity;
        _ghost.SetActive(true);

        SetGhostColor(occupied ? new Color(1f, 0f, 0f, 0.5f) : new Color(0.5f, 0.5f, 0.5f, 0.3f));
    }

    private static readonly Color ValidColor = new(0f, 1f, 0f, 0.5f);
    private static readonly Color InvalidColor = new(1f, 0f, 0f, 0.5f);
    private static readonly Color OccupiedColor = new(1f, 0.3f, 0f, 0.5f);

    private Material _ghostMaterial;

    private void EnsureGhost(Vector3 scale)
    {
        if (_ghost == null)
        {
            _ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ghost.GetComponent<Collider>().enabled = false;
            _ghost.layer = PhysicsLayers.Decal;
            var renderer = _ghost.GetComponent<Renderer>();
            _ghostMaterial = new Material(renderer.sharedMaterial);
            renderer.sharedMaterial = _ghostMaterial;
        }
        _ghost.transform.localScale = scale;
    }

    private void SetGhostColor(Color color)
    {
        if (_ghostMaterial != null)
            _ghostMaterial.color = color;
    }

    private void DestroyGhost()
    {
        if (_ghost != null)
        {
            Destroy(_ghost);
            _ghost = null;
            _ghostMaterial = null;
        }
    }

    private void OnGUI()
    {
        if (!IsOwner) return;

        // Crosshair
        float cx = Screen.width / 2f;
        float cy = Screen.height / 2f;
        float size = 12f;
        float thickness = 2f;
        GUI.DrawTexture(new Rect(cx - size, cy - thickness / 2, size * 2, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - thickness / 2, cy - size, thickness, size * 2), Texture2D.whiteTexture);

        if (!_buildMode) return;

        GUILayout.BeginArea(new Rect(10, 50, 300, 120));
        GUILayout.Label($"BUILD MODE  |  Tool: {_currentTool}");
        GUILayout.Label("1: Foundation  2: Wall  3: Ramp  4: Delete");
        GUILayout.Label($"Cell: ({_lastCell.x}, {_lastCell.y})  Level: {_lastLevel}");
        if (_currentTool == BuildTool.Wall || _currentTool == BuildTool.Ramp)
            GUILayout.Label($"Edge: ({_lastEdgeDir.x}, {_lastEdgeDir.y})");
        GUILayout.Label("LMB: Place  |  RMB: Remove  |  B: Exit");
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        DestroyGhost();
    }
}
