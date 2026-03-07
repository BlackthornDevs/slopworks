using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkBuildController : NetworkBehaviour
{
    [SerializeField] private float _placementRange = 50f;

    private enum BuildTool { Foundation, Wall, Ramp, Machine, Storage, Belt }

    private Camera _camera;
    private bool _buildMode;
    private BuildTool _currentTool;
    private Vector2Int _lastCell;
    private int _lastLevel;
    private Vector2Int _lastEdgeDir;
    private bool _lastValid;

    // Level control
    private int _levelOverrideFrames;

    // Machine/storage rotation
    private int _placeRotation;

    // Delete mode (X key toggle, auto-exits after delete)
    private bool _deleteMode;
    private GameObject _deleteHighlight;
    private readonly List<(Renderer renderer, Material[] originals)> _deleteSavedMaterials = new();

    // Zoop mode (Z key toggle, 2-click start/end like belt)
    private bool _zoopMode;
    private bool _zoopStartSet;
    private Vector2Int _zoopStartCell;
    private Vector2Int _zoopStartDir; // for walls: edge direction at start
    private int _zoopStartLevel;
    private bool _zoopStartOnFoundation;
    private readonly List<GameObject> _ghostPool = new();
    private readonly List<GameObject> _zoopGhosts = new();

    // Belt 2-click
    private bool _beltStartSet;
    private Vector2Int _beltStartCell;
    private GameObject _beltGhostLine;

    // Single ghost for simple tools
    private GameObject _ghost;
    private Material _ghostMaterial;
    private GameObject _ghostPrefabSource;
    private readonly List<Material> _ghostMaterials = new();

    private GridOverlay _gridOverlay;

    private static readonly int StructuralMask =
        (1 << PhysicsLayers.Terrain) | (1 << PhysicsLayers.Structures);

    private static readonly Color ValidColor = new(0f, 1f, 0f, 0.5f);
    private static readonly Color InvalidColor = new(1f, 0f, 0f, 0.5f);
    private static readonly Color OccupiedColor = new(1f, 0.3f, 0f, 0.5f);
    private static readonly Color DeleteColor = new(1f, 0f, 0f, 0.5f);
    private static readonly Color DeleteInactiveColor = new(0.5f, 0.5f, 0.5f, 0.3f);

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner)
        {
            enabled = false;
            return;
        }
        _camera = GetComponentInChildren<Camera>();
        _gridOverlay = gameObject.AddComponent<GridOverlay>();
        _gridOverlay.Init(_camera);
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // Delete mode toggle (X key) -- works regardless of build mode
        if (kb.xKey.wasPressedThisFrame)
        {
            _deleteMode = !_deleteMode;
            if (_deleteMode)
            {
                CancelAllPending();
                Debug.Log("build: delete mode ON");
            }
            else
            {
                ClearDeleteHighlight();
                Debug.Log("build: delete mode OFF");
            }
        }

        // Delete mode takes priority
        if (_deleteMode)
        {
            HandleDeleteMode(mouse, kb);
            return;
        }

        // Toggle build mode
        if (kb.bKey.wasPressedThisFrame)
        {
            _buildMode = !_buildMode;
            Debug.Log($"build: mode {(_buildMode ? "ON" : "OFF")}");
            if (!_buildMode) CancelAllPending();
        }

        if (!_buildMode) return;

        // Tool switching: 1-6
        if (kb.digit1Key.wasPressedThisFrame) SwitchTool(BuildTool.Foundation);
        if (kb.digit2Key.wasPressedThisFrame) SwitchTool(BuildTool.Wall);
        if (kb.digit3Key.wasPressedThisFrame) SwitchTool(BuildTool.Ramp);
        if (kb.digit4Key.wasPressedThisFrame) SwitchTool(BuildTool.Machine);
        if (kb.digit5Key.wasPressedThisFrame) SwitchTool(BuildTool.Storage);
        if (kb.digit6Key.wasPressedThisFrame) SwitchTool(BuildTool.Belt);

        // Escape cancels
        if (kb.escapeKey.wasPressedThisFrame)
        {
            CancelAllPending();
            return;
        }

        // Level change: PgUp/PgDn
        HandleLevelChange(kb);
        HandleAutoLevel(mouse);

        // Rotation: R key
        if (kb.rKey.wasPressedThisFrame)
        {
            _placeRotation = (_placeRotation + 90) % 360;
            Debug.Log($"build: rotation = {_placeRotation}");
        }

        // Zoop toggle: Z key
        if (kb.zKey.wasPressedThisFrame)
        {
            _zoopMode = !_zoopMode;
            CancelZoop();
            Debug.Log($"build: zoop mode {(_zoopMode ? "ON" : "OFF")}");
        }

        // Grid overlay toggle: G key
        if (kb.gKey.wasPressedThisFrame && _gridOverlay != null)
        {
            _gridOverlay.Visible = !_gridOverlay.Visible;
            Debug.Log($"build: grid overlay {(_gridOverlay.Visible ? "ON" : "OFF")}");
        }

        // Tool-specific input
        switch (_currentTool)
        {
            case BuildTool.Foundation:
                HandleFoundationInput(mouse);
                break;
            case BuildTool.Wall:
                HandleWallInput(kb, mouse);
                break;
            case BuildTool.Ramp:
                HandleRampInput(mouse);
                break;
            case BuildTool.Machine:
                HandleMachineInput(mouse);
                break;
            case BuildTool.Storage:
                HandleStorageInput(mouse);
                break;
            case BuildTool.Belt:
                HandleBeltInput(mouse);
                break;
        }
    }

    // -- Delete mode (X key) --

    private void HandleDeleteMode(Mouse mouse, Keyboard kb)
    {
        if (kb.escapeKey.wasPressedThisFrame)
        {
            _deleteMode = false;
            ClearDeleteHighlight();
            Debug.Log("build: delete mode OFF");
            return;
        }

        // Raycast against all placed objects (terrain + structures + interactable)
        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (!Physics.Raycast(ray, out var hit, _placementRange))
        {
            ClearDeleteHighlight();
            return;
        }

        var placement = hit.collider.GetComponentInParent<PlacementInfo>();
        if (placement == null)
        {
            ClearDeleteHighlight();
            return;
        }

        // Highlight the actual placed object by tinting it red
        var target = placement.gameObject;
        if (_deleteHighlight != target)
        {
            ClearDeleteHighlight();
            _deleteHighlight = target;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                var originals = r.sharedMaterials;
                _deleteSavedMaterials.Add((r, originals));
                var tinted = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    tinted[i] = new Material(originals[i]);
                    tinted[i].color = DeleteColor;
                }
                r.sharedMaterials = tinted;
            }
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            ClearDeleteHighlight();
            switch (placement.Type)
            {
                case PlacementInfo.PlacementType.Foundation:
                    GridManager.Instance.CmdRemoveFoundation(placement.Cell, placement.Level);
                    break;
                case PlacementInfo.PlacementType.Wall:
                    GridManager.Instance.CmdRemoveWall(placement.Cell, placement.Level, placement.EdgeDirection);
                    break;
                case PlacementInfo.PlacementType.Ramp:
                    GridManager.Instance.CmdRemoveRamp(placement.Cell, placement.Level, placement.EdgeDirection);
                    break;
                default:
                    GridManager.Instance.CmdDeleteAt(placement.Cell, placement.Level);
                    break;
            }
            Debug.Log($"build: deleted {placement.Type} at ({placement.Cell.x},{placement.Cell.y}) level {placement.Level}");
        }
    }

    private void ClearDeleteHighlight()
    {
        foreach (var (renderer, originals) in _deleteSavedMaterials)
        {
            if (renderer != null)
                renderer.sharedMaterials = originals;
        }
        _deleteSavedMaterials.Clear();
        _deleteHighlight = null;
    }

    // -- Level controls --

    private void HandleLevelChange(Keyboard kb)
    {
        if (kb.pageUpKey.wasPressedThisFrame)
        {
            _lastLevel = Mathf.Min(_lastLevel + 1, FactoryGrid.MaxLevels - 1);
            _levelOverrideFrames = 90;
            Debug.Log($"build: level {_lastLevel} (manual)");
        }
        else if (kb.pageDownKey.wasPressedThisFrame)
        {
            _lastLevel = Mathf.Max(_lastLevel - 1, 0);
            _levelOverrideFrames = 90;
            Debug.Log($"build: level {_lastLevel} (manual)");
        }
    }

    private void HandleAutoLevel(Mouse mouse)
    {
        if (_levelOverrideFrames > 0)
        {
            _levelOverrideFrames--;
            return;
        }

        // Lock level during any 2-click operation (zoop or belt)
        if (_zoopStartSet || _beltStartSet) return;

        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (Physics.Raycast(ray, out var hit, _placementRange, StructuralMask))
        {
            int level = Mathf.Clamp(
                Mathf.RoundToInt(hit.point.y / FactoryGrid.LevelHeight),
                0, FactoryGrid.MaxLevels - 1);
            _lastLevel = level;
        }
    }

    // -- Tool switching --

    private void SwitchTool(BuildTool tool)
    {
        if (_currentTool == tool) return;
        CancelAllPending();
        _currentTool = tool;
        if (tool == BuildTool.Machine || tool == BuildTool.Storage)
            _placeRotation = 0;
        Debug.Log($"build: tool = {tool}");
    }

    private void CancelAllPending()
    {
        CancelZoop();
        CancelBeltPlacement();
        DestroyGhost();
    }

    private void CancelZoop()
    {
        _zoopStartSet = false;
        for (int i = 0; i < _ghostPool.Count; i++)
            _ghostPool[i].SetActive(false);
        for (int i = 0; i < _zoopGhosts.Count; i++)
            _zoopGhosts[i].SetActive(false);
        for (int i = 0; i < _rampZoopGhosts.Count; i++)
            _rampZoopGhosts[i].SetActive(false);
    }

    // -- Raycast helpers --

    private bool RaycastGrid(out Vector3 hitPoint, out Vector2Int cell)
    {
        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (!Physics.Raycast(ray, out var hit, _placementRange, StructuralMask))
        {
            hitPoint = Vector3.zero;
            cell = Vector2Int.zero;
            return false;
        }

        hitPoint = hit.point;
        cell = GridManager.Instance.Grid.WorldToCell(hit.point);
        return true;
    }

    /// <summary>
    /// Raycasts for wall placement. If aiming at a foundation, snaps to its nearest edge.
    /// Falls back to terrain-based grid placement.
    /// </summary>
    private bool _lastHitFoundation;

    private bool RaycastWallPlacement(out Vector2Int cell, out Vector2Int edgeDir, out int level)
    {
        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (!Physics.Raycast(ray, out var hit, _placementRange, StructuralMask))
        {
            cell = Vector2Int.zero;
            edgeDir = Vector2Int.up;
            level = _lastLevel;
            _lastHitFoundation = false;
            return false;
        }

        var placement = hit.collider.GetComponentInParent<PlacementInfo>();
        if (placement != null && placement.Type == PlacementInfo.PlacementType.Foundation)
        {
            _lastHitFoundation = true;
            level = placement.Level;

            // Use camera facing direction -- more intuitive than nearest-edge-to-hit-point
            edgeDir = GetFacingEdgeDirection();
            cell = placement.Cell;
            return true;
        }

        // Fallback: terrain-based placement
        // No wall-grid snapping on terrain -- wall follows the crosshair cell directly
        _lastHitFoundation = false;
        cell = GridManager.Instance.Grid.WorldToCell(hit.point);
        edgeDir = GetFacingEdgeDirection();
        level = _lastLevel;
        return true;
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

    /// <summary>
    /// Returns an edge direction based on camera facing (XZ plane).
    /// Stable for terrain placement -- doesn't flicker as crosshair moves within a cell.
    /// </summary>
    private Vector2Int GetFacingEdgeDirection()
    {
        var fwd = _camera.transform.forward;
        if (Mathf.Abs(fwd.x) > Mathf.Abs(fwd.z))
            return fwd.x > 0 ? Vector2Int.right : Vector2Int.left;
        return fwd.z > 0 ? Vector2Int.up : Vector2Int.down;
    }

    // -- Foundation: click to place 4x4 block, drag for multiple --

    private void HandleFoundationInput(Mouse mouse)
    {
        if (!RaycastGrid(out var hitPoint, out var cell))
        {
            HideFoundationGhosts();
            return;
        }

        if (_zoopMode)
        {
            if (!_zoopStartSet)
            {
                int fs = FactoryGrid.FoundationSize;
                var centered = new Vector2Int(cell.x - fs / 2, cell.y - fs / 2);
                ShowFoundationGhostSingle(centered);

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    _zoopStartSet = true;
                    _zoopStartCell = centered;
                    _zoopStartLevel = _lastLevel;
                    Debug.Log($"build: foundation zoop start ({centered.x},{centered.y}) level {_lastLevel} -- click end");
                }
            }
            else
            {
                var centered = new Vector2Int(cell.x - FactoryGrid.FoundationSize / 2, cell.y - FactoryGrid.FoundationSize / 2);
                UpdateFoundationZoopPreview(centered);

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    PlaceFoundationRect(_zoopStartCell, centered);
                    CancelZoop();
                }

                if (mouse.rightButton.wasPressedThisFrame)
                    CancelZoop();
            }
        }
        else
        {
            // Single placement: follows 1x1 grid, centered on crosshair
            int fs = FactoryGrid.FoundationSize;
            var centered = new Vector2Int(cell.x - fs / 2, cell.y - fs / 2);
            ShowFoundationGhostSingle(centered);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                GridManager.Instance.CmdPlaceFoundation(centered, _lastLevel);
            }
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            GridManager.Instance.CmdRemoveFoundation(cell, _lastLevel);
        }
    }

    private void ShowFoundationGhostSingle(Vector2Int snapped)
    {
        var grid = GridManager.Instance.Grid;
        int fs = FactoryGrid.FoundationSize;
        float blockSize = fs * FactoryGrid.CellSize;
        bool valid = grid.CanPlace(snapped, new Vector2Int(fs, fs), _lastLevel);

        while (_ghostPool.Count < 1)
            _ghostPool.Add(CreateFoundationGhost());

        float halfBlock = blockSize * 0.5f;
        var worldPos = new Vector3(
            snapped.x * FactoryGrid.CellSize + halfBlock,
            _lastLevel * FactoryGrid.LevelHeight + 0.5f,
            snapped.y * FactoryGrid.CellSize + halfBlock);

        _ghostPool[0].SetActive(true);
        _ghostPool[0].transform.position = worldPos;
        ApplyGhostColor(_ghostPool[0], valid ? ValidColor : InvalidColor);

        for (int i = 1; i < _ghostPool.Count; i++)
            _ghostPool[i].SetActive(false);
    }

    private void UpdateFoundationZoopPreview(Vector2Int endCell)
    {
        var grid = GridManager.Instance.Grid;
        int fs = FactoryGrid.FoundationSize;
        float blockSize = fs * FactoryGrid.CellSize;

        int dirX = endCell.x >= _zoopStartCell.x ? 1 : -1;
        int dirZ = endCell.y >= _zoopStartCell.y ? 1 : -1;
        int countX = Mathf.Abs(endCell.x - _zoopStartCell.x) / fs + 1;
        int countZ = Mathf.Abs(endCell.y - _zoopStartCell.y) / fs + 1;
        int needed = countX * countZ;

        while (_ghostPool.Count < needed)
            _ghostPool.Add(CreateFoundationGhost());

        int idx = 0;
        for (int nx = 0; nx < countX; nx++)
        {
            for (int nz = 0; nz < countZ; nz++)
            {
                var origin = new Vector2Int(
                    _zoopStartCell.x + nx * fs * dirX,
                    _zoopStartCell.y + nz * fs * dirZ);
                bool valid = grid.CanPlace(origin, new Vector2Int(fs, fs), _zoopStartLevel);
                float halfBlock = blockSize * 0.5f;
                var worldPos = new Vector3(
                    origin.x * FactoryGrid.CellSize + halfBlock,
                    _zoopStartLevel * FactoryGrid.LevelHeight + 0.5f,
                    origin.y * FactoryGrid.CellSize + halfBlock);

                _ghostPool[idx].SetActive(true);
                _ghostPool[idx].transform.position = worldPos;
                ApplyGhostColor(_ghostPool[idx], valid ? ValidColor : InvalidColor);
                idx++;
            }
        }

        for (int i = idx; i < _ghostPool.Count; i++)
            _ghostPool[i].SetActive(false);
    }

    private GameObject CreateFoundationGhost()
    {
        return CreateGhostFromPrefab(GridManager.Instance.FoundationPrefab);
    }

    private void PlaceFoundationRect(Vector2Int start, Vector2Int end)
    {
        int fs = FactoryGrid.FoundationSize;
        int dirX = end.x >= start.x ? 1 : -1;
        int dirZ = end.y >= start.y ? 1 : -1;
        int countX = Mathf.Abs(end.x - start.x) / fs + 1;
        int countZ = Mathf.Abs(end.y - start.y) / fs + 1;

        for (int nx = 0; nx < countX; nx++)
        {
            for (int nz = 0; nz < countZ; nz++)
            {
                var cell = new Vector2Int(
                    start.x + nx * fs * dirX,
                    start.y + nz * fs * dirZ);
                GridManager.Instance.CmdPlaceFoundation(cell, _zoopStartLevel);
            }
        }
        Debug.Log($"build: foundation zoop placed at level {_zoopStartLevel}");
    }

    private void HideFoundationGhosts()
    {
        for (int i = 0; i < _ghostPool.Count; i++)
            _ghostPool[i].SetActive(false);
    }

    // -- Wall: drag-zoop along edge --

    private Vector2Int _lastWallCell;

    private void HandleWallInput(Keyboard kb, Mouse mouse)
    {
        bool hasHit = RaycastWallPlacement(out var cell, out var edgeDir, out var level);

        if (!hasHit && !_zoopStartSet)
        {
            DestroyGhost();
            return;
        }

        if (hasHit)
        {
            _lastWallCell = cell;
            if (!_zoopStartSet)
                _lastLevel = level;
        }

        if (_zoopMode)
        {
            if (!_zoopStartSet)
            {
                if (!hasHit) return;
                UpdateWallGhost(GridManager.Instance.Grid, cell, _lastLevel, edgeDir);

                if (mouse.leftButton.wasPressedThisFrame && _lastValid)
                {
                    _zoopStartSet = true;
                    _zoopStartCell = cell;
                    _zoopStartDir = edgeDir;
                    _zoopStartLevel = _lastLevel;
                    _zoopStartOnFoundation = _lastHitFoundation;
                    DestroyGhost();
                    Debug.Log($"build: wall zoop start ({cell.x},{cell.y}) level {_lastLevel} -- click end");
                }
            }
            else
            {
                // Use last known cell if current raycast missed
                var zoopCell = hasHit ? cell : _lastWallCell;
                UpdateWallZoopPreview(zoopCell);

                if (mouse.leftButton.wasPressedThisFrame && _wallZoopCells.Count > 0)
                {
                    PlaceWallZoop();
                    CancelZoop();
                }

                if (mouse.rightButton.wasPressedThisFrame)
                    CancelZoop();
            }
        }
        else
        {
            // Single placement: click to place one wall
            UpdateWallGhost(GridManager.Instance.Grid, cell, _lastLevel, edgeDir);

            if (mouse.leftButton.wasPressedThisFrame && _lastValid)
            {
                GridManager.Instance.CmdPlaceWall(cell, _lastLevel, edgeDir, _lastHitFoundation);
            }
        }

        if (mouse.rightButton.wasPressedThisFrame && !_zoopStartSet)
        {
            GridManager.Instance.CmdRemoveWall(cell, _lastLevel, edgeDir);
        }
    }

    private readonly List<Vector2Int> _wallZoopCells = new();

    private void UpdateWallZoopPreview(Vector2Int currentCell)
    {
        _wallZoopCells.Clear();

        int ww = FactoryGrid.WallWidth;
        bool walkX = (_zoopStartDir == Vector2Int.up || _zoopStartDir == Vector2Int.down);
        int anchor = walkX ? _zoopStartCell.x : _zoopStartCell.y;
        int target = walkX ? currentCell.x : currentCell.y;
        int dir = target >= anchor ? 1 : -1;
        int count = Mathf.Abs(target - anchor) / ww + 1;

        for (int n = 0; n < count; n++)
        {
            int pos = anchor + n * ww * dir;
            var wallCell = walkX
                ? new Vector2Int(pos, _zoopStartCell.y)
                : new Vector2Int(_zoopStartCell.x, pos);

            bool wallExists = GridManager.Instance.HasWallAt(wallCell, _zoopStartLevel, _zoopStartDir);
            if (!wallExists)
                _wallZoopCells.Add(wallCell);
        }

        float wallHeight = FactoryGrid.WallHeight;
        bool isEW = (_zoopStartDir == Vector2Int.left || _zoopStartDir == Vector2Int.right);

        while (_zoopGhosts.Count < _wallZoopCells.Count)
            _zoopGhosts.Add(CreateGhostFromPrefab(GridManager.Instance.WallPrefab));

        for (int i = 0; i < _wallZoopCells.Count; i++)
        {
            var ghost = _zoopGhosts[i];
            ghost.SetActive(true);
            var wc = _wallZoopCells[i];
            var wallPos = GetWallPosition(wc, _zoopStartLevel, _zoopStartDir, _zoopStartOnFoundation, wallHeight);
            ghost.transform.position = wallPos;
            ghost.transform.rotation = isEW ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            ApplyGhostColor(ghost, ValidColor);
        }

        for (int i = _wallZoopCells.Count; i < _zoopGhosts.Count; i++)
            _zoopGhosts[i].SetActive(false);
    }

    private void PlaceWallZoop()
    {
        var gm = GridManager.Instance;
        for (int i = 0; i < _wallZoopCells.Count; i++)
        {
            gm.CmdPlaceWall(_wallZoopCells[i], _zoopStartLevel, _zoopStartDir, _zoopStartOnFoundation);
        }
        Debug.Log($"build: wall zoop placed {_wallZoopCells.Count} walls");
    }

    // -- Ramp --

    private Vector2Int _lastRampCell;
    private readonly List<Vector2Int> _rampZoopCells = new();
    private readonly List<GameObject> _rampZoopGhosts = new();

    private void HandleRampInput(Mouse mouse)
    {
        // Reuse wall raycast logic -- ramps also snap to foundation edges
        bool hasHit = RaycastWallPlacement(out var cell, out var edgeDir, out var level);

        if (!hasHit && !_zoopStartSet)
        {
            DestroyGhost();
            return;
        }

        if (hasHit)
        {
            _lastRampCell = cell;
            if (!_zoopStartSet)
                _lastLevel = level;
        }

        if (_zoopMode)
        {
            if (!_zoopStartSet)
            {
                if (!hasHit) return;
                _lastEdgeDir = edgeDir;
                UpdateRampGhost(GridManager.Instance.Grid, cell, _lastLevel, edgeDir);

                if (mouse.leftButton.wasPressedThisFrame && _lastValid)
                {
                    _zoopStartSet = true;
                    _zoopStartCell = cell;
                    _zoopStartDir = edgeDir;
                    _zoopStartLevel = _lastLevel;
                    _zoopStartOnFoundation = _lastHitFoundation;
                    DestroyGhost();
                    Debug.Log($"build: ramp zoop start ({cell.x},{cell.y}) level {_lastLevel} -- click end");
                }
            }
            else
            {
                var zoopCell = hasHit ? cell : _lastRampCell;
                UpdateRampZoopPreview(zoopCell);

                if (mouse.leftButton.wasPressedThisFrame && _rampZoopCells.Count > 0)
                {
                    PlaceRampZoop();
                    CancelZoop();
                }

                if (mouse.rightButton.wasPressedThisFrame)
                    CancelZoop();
            }
        }
        else
        {
            _lastLevel = level;
            _lastEdgeDir = edgeDir;
            UpdateRampGhost(GridManager.Instance.Grid, cell, _lastLevel, edgeDir);

            if (mouse.leftButton.wasPressedThisFrame && _lastValid)
            {
                GridManager.Instance.CmdPlaceRamp(cell, _lastLevel, edgeDir, _lastHitFoundation);
            }
        }

        if (mouse.rightButton.wasPressedThisFrame && !_zoopStartSet)
        {
            GridManager.Instance.CmdRemoveRamp(cell, _lastLevel, edgeDir);
        }
    }

    private void UpdateRampZoopPreview(Vector2Int currentCell)
    {
        _rampZoopCells.Clear();

        int ww = FactoryGrid.WallWidth;
        bool walkX = (_zoopStartDir == Vector2Int.up || _zoopStartDir == Vector2Int.down);
        int anchor = walkX ? _zoopStartCell.x : _zoopStartCell.y;
        int target = walkX ? currentCell.x : currentCell.y;
        int dir = target >= anchor ? 1 : -1;
        int count = Mathf.Abs(target - anchor) / ww + 1;

        for (int n = 0; n < count; n++)
        {
            int pos = anchor + n * ww * dir;
            var rampCell = walkX
                ? new Vector2Int(pos, _zoopStartCell.y)
                : new Vector2Int(_zoopStartCell.x, pos);

            bool rampExists = GridManager.Instance.HasRampAt(rampCell, _zoopStartLevel, _zoopStartDir);
            if (!rampExists)
                _rampZoopCells.Add(rampCell);
        }

        float rampLength = 3f * FactoryGrid.CellSize;
        float rampRise = FactoryGrid.WallHeight;
        float slopeLength = Mathf.Sqrt(rampLength * rampLength + rampRise * rampRise);

        float yAngle = 0f;
        if (_zoopStartDir == Vector2Int.right) yAngle = 90f;
        else if (_zoopStartDir == Vector2Int.down) yAngle = 180f;
        else if (_zoopStartDir == Vector2Int.left) yAngle = 270f;

        float pitch = Mathf.Atan2(rampRise, rampLength) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(-pitch, yAngle, 0f);

        while (_rampZoopGhosts.Count < _rampZoopCells.Count)
            _rampZoopGhosts.Add(CreateGhostFromPrefab(GridManager.Instance.RampPrefab));

        for (int i = 0; i < _rampZoopCells.Count; i++)
        {
            var ghost = _rampZoopGhosts[i];
            ghost.SetActive(true);
            var rc = _rampZoopCells[i];
            var rampPos = GetRampGhostPosition(rc, _zoopStartLevel, _zoopStartDir, _zoopStartOnFoundation, rot, slopeLength);
            ghost.transform.position = rampPos;
            ghost.transform.rotation = rot;
            ApplyGhostColor(ghost, ValidColor);
        }

        for (int i = _rampZoopCells.Count; i < _rampZoopGhosts.Count; i++)
            _rampZoopGhosts[i].SetActive(false);
    }

    private Vector3 GetRampGhostPosition(Vector2Int cell, int level, Vector2Int edgeDir, bool onFoundation, Quaternion rot, float slopeLength)
    {
        float cs = FactoryGrid.CellSize;
        float baseY = onFoundation
            ? level * FactoryGrid.LevelHeight + 1f
            : level * FactoryGrid.LevelHeight;

        Vector3 baseEdge;
        if (onFoundation)
        {
            int fs = FactoryGrid.FoundationSize;
            float blockSize = fs * cs;
            float halfBlock = blockSize * 0.5f;
            Vector3 blockCenter = new Vector3(
                cell.x * cs + halfBlock, baseY, cell.y * cs + halfBlock);
            baseEdge = blockCenter + new Vector3(edgeDir.x * halfBlock, 0f, edgeDir.y * halfBlock);
        }
        else
        {
            float cellCenter = 0.5f * cs;
            baseEdge = new Vector3(cell.x * cs + cellCenter, baseY, cell.y * cs + cellCenter);
        }

        Vector3 localForward = rot * Vector3.forward;
        return baseEdge + localForward * (slopeLength * 0.5f);
    }

    private void PlaceRampZoop()
    {
        var gm = GridManager.Instance;
        for (int i = 0; i < _rampZoopCells.Count; i++)
        {
            gm.CmdPlaceRamp(_rampZoopCells[i], _zoopStartLevel, _zoopStartDir, _zoopStartOnFoundation);
        }
        Debug.Log($"build: ramp zoop placed {_rampZoopCells.Count} ramps");
    }

    // -- Machine --

    private void HandleMachineInput(Mouse mouse)
    {
        if (!RaycastGrid(out _, out var cell))
        {
            DestroyGhost();
            return;
        }

        var grid = GridManager.Instance.Grid;
        bool cellFree = !GridManager.Instance.HasBuildingAt(cell, _lastLevel);
        _lastValid = cellFree;

        EnsureGhost(new Vector3(FactoryGrid.CellSize * 0.8f, 0.5f, FactoryGrid.CellSize * 0.8f));
        _ghost.transform.position = grid.CellToWorld(cell, _lastLevel) + new Vector3(0f, 0.35f, 0f);
        _ghost.transform.rotation = Quaternion.Euler(0f, _placeRotation, 0f);
        _ghost.SetActive(true);
        SetGhostColor(_lastValid ? new Color(0.8f, 0.5f, 0f, 0.5f) : InvalidColor);

        if (mouse.leftButton.wasPressedThisFrame && _lastValid)
        {
            GridManager.Instance.CmdPlaceMachine(cell, _lastLevel, _placeRotation);
        }
    }

    // -- Storage --

    private void HandleStorageInput(Mouse mouse)
    {
        if (!RaycastGrid(out _, out var cell))
        {
            DestroyGhost();
            return;
        }

        var grid = GridManager.Instance.Grid;
        bool cellFree = !GridManager.Instance.HasBuildingAt(cell, _lastLevel);
        _lastValid = cellFree;

        EnsureGhost(new Vector3(FactoryGrid.CellSize * 0.8f, 0.4f, FactoryGrid.CellSize * 0.8f));
        _ghost.transform.position = grid.CellToWorld(cell, _lastLevel) + new Vector3(0f, 0.3f, 0f);
        _ghost.transform.rotation = Quaternion.Euler(0f, _placeRotation, 0f);
        _ghost.SetActive(true);
        SetGhostColor(_lastValid ? new Color(0.3f, 0.3f, 1f, 0.5f) : InvalidColor);

        if (mouse.leftButton.wasPressedThisFrame && _lastValid)
        {
            GridManager.Instance.CmdPlaceStorage(cell, _lastLevel, _placeRotation);
        }
    }

    // -- Belt: 2-click placement --

    private void HandleBeltInput(Mouse mouse)
    {
        if (!RaycastGrid(out _, out var cell))
        {
            if (!_beltStartSet) DestroyGhost();
            return;
        }

        var grid = GridManager.Instance.Grid;

        if (!_beltStartSet)
        {
            EnsureGhost(new Vector3(0.6f, 0.08f, 0.6f));
            _ghost.transform.position = grid.CellToWorld(cell, _lastLevel) + new Vector3(0f, 0.15f, 0f);
            _ghost.transform.rotation = Quaternion.identity;
            _ghost.SetActive(true);
            SetGhostColor(new Color(1f, 1f, 0f, 0.5f));

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _beltStartSet = true;
                _beltStartCell = cell;
                DestroyGhost();
                Debug.Log($"build: belt start ({cell.x},{cell.y}) -- click end cell");
            }
        }
        else
        {
            UpdateBeltGhostLine(cell);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                var diff = cell - _beltStartCell;
                Vector2Int snappedEnd;
                if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
                    snappedEnd = new Vector2Int(cell.x, _beltStartCell.y);
                else
                    snappedEnd = new Vector2Int(_beltStartCell.x, cell.y);

                if (snappedEnd != _beltStartCell)
                    GridManager.Instance.CmdPlaceBelt(_beltStartCell, snappedEnd, _lastLevel);
                else
                    Debug.Log("build: belt start and end are the same cell");

                CancelBeltPlacement();
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelBeltPlacement();
            }
        }
    }

    private void UpdateBeltGhostLine(Vector2Int endCell)
    {
        DestroyBeltGhostLine();

        var diff = endCell - _beltStartCell;
        Vector2Int snappedEnd;
        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
            snappedEnd = new Vector2Int(endCell.x, _beltStartCell.y);
        else
            snappedEnd = new Vector2Int(_beltStartCell.x, endCell.y);

        if (snappedEnd == _beltStartCell) return;

        var grid = GridManager.Instance.Grid;
        var startWorld = grid.CellToWorld(_beltStartCell, _lastLevel);
        var endWorld = grid.CellToWorld(snappedEnd, _lastLevel);
        var center = (startWorld + endWorld) * 0.5f + Vector3.up * 0.15f;
        var d = endWorld - startWorld;
        float len = d.magnitude + FactoryGrid.CellSize;

        _beltGhostLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _beltGhostLine.name = "BeltGhost";
        var col = _beltGhostLine.GetComponent<Collider>();
        if (col != null) Destroy(col);
        _beltGhostLine.layer = PhysicsLayers.Decal;
        _beltGhostLine.transform.position = center;
        _beltGhostLine.transform.localScale = Mathf.Abs(d.x) > Mathf.Abs(d.z)
            ? new Vector3(len, 0.08f, 0.6f)
            : new Vector3(0.6f, 0.08f, len);

        var renderer = _beltGhostLine.GetComponent<Renderer>();
        var mat = new Material(renderer.sharedMaterial);
        mat.color = new Color(1f, 1f, 0f, 0.5f);
        renderer.sharedMaterial = mat;
    }

    private void DestroyBeltGhostLine()
    {
        if (_beltGhostLine != null)
        {
            Destroy(_beltGhostLine);
            _beltGhostLine = null;
        }
    }

    private void CancelBeltPlacement()
    {
        _beltStartSet = false;
        DestroyBeltGhostLine();
        DestroyGhost();
    }

    // -- Ghost helpers --

    private void UpdateWallGhost(FactoryGrid grid, Vector2Int cell, int level, Vector2Int edgeDir)
    {
        bool wallExists = GridManager.Instance.HasWallAt(cell, level, edgeDir);
        _lastValid = !wallExists;

        EnsurePrefabGhost(GridManager.Instance.WallPrefab);

        float wallHeight = FactoryGrid.WallHeight;
        Vector3 wallCenter = GetWallPosition(cell, level, edgeDir, _lastHitFoundation, wallHeight);

        _ghost.transform.position = wallCenter;
        _ghost.transform.rotation = (edgeDir == Vector2Int.left || edgeDir == Vector2Int.right)
            ? Quaternion.Euler(0f, 90f, 0f)
            : Quaternion.identity;
        _ghost.SetActive(true);

        SetGhostColor(_lastValid ? ValidColor : OccupiedColor);
    }

    /// <summary>
    /// Computes wall world position. Shared between ghost preview and zoop preview.
    /// On foundations: wall sits on the edge of a 4x4 block.
    /// On terrain: wall sits at the cell origin, centered on WallWidth span.
    /// </summary>
    private static Vector3 GetWallPosition(Vector2Int cell, int level, Vector2Int edgeDir, bool onFoundation, float wallHeight)
    {
        float cs = FactoryGrid.CellSize;
        float halfWidth = FactoryGrid.WallWidth * cs * 0.5f;
        float baseY = onFoundation
            ? level * FactoryGrid.LevelHeight + 1f
            : level * FactoryGrid.LevelHeight;

        if (onFoundation)
        {
            // On foundation: position at the edge of the 4x4 block
            int fs = FactoryGrid.FoundationSize;
            float blockSize = fs * cs;
            float halfBlock = blockSize * 0.5f;
            Vector3 blockCenter = new Vector3(
                cell.x * cs + halfBlock,
                baseY,
                cell.y * cs + halfBlock);
            return blockCenter + new Vector3(
                edgeDir.x * halfBlock,
                wallHeight * 0.5f,
                edgeDir.y * halfBlock);
        }

        // On terrain: wall centered on the crosshair cell
        float cellCenter = 0.5f * cs;
        if (edgeDir == Vector2Int.up || edgeDir == Vector2Int.down)
        {
            // Wall runs along X, face at cell center Z
            return new Vector3(
                cell.x * cs + cellCenter,
                baseY + wallHeight * 0.5f,
                cell.y * cs + cellCenter);
        }
        // Wall runs along Z, face at cell center X
        return new Vector3(
            cell.x * cs + cellCenter,
            baseY + wallHeight * 0.5f,
            cell.y * cs + cellCenter);
    }

    private void UpdateRampGhost(FactoryGrid grid, Vector2Int cell, int level, Vector2Int edgeDir)
    {
        bool rampExists = GridManager.Instance.HasRampAt(cell, level, edgeDir);
        _lastValid = !rampExists && level + 1 < FactoryGrid.MaxLevels;

        EnsurePrefabGhost(GridManager.Instance.RampPrefab);

        float rampLength = 3f * FactoryGrid.CellSize;
        float rampRise = FactoryGrid.WallHeight;
        float slopeLength = Mathf.Sqrt(rampLength * rampLength + rampRise * rampRise);

        float yAngle = 0f;
        if (edgeDir == Vector2Int.right) yAngle = 90f;
        else if (edgeDir == Vector2Int.down) yAngle = 180f;
        else if (edgeDir == Vector2Int.left) yAngle = 270f;

        float pitch = Mathf.Atan2(rampRise, rampLength) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(-pitch, yAngle, 0f);

        float cs = FactoryGrid.CellSize;
        float baseY = _lastHitFoundation
            ? level * FactoryGrid.LevelHeight + 1f
            : level * FactoryGrid.LevelHeight;

        Vector3 baseEdge;
        if (_lastHitFoundation)
        {
            int fs = FactoryGrid.FoundationSize;
            float blockSize = fs * cs;
            float halfBlock = blockSize * 0.5f;
            Vector3 blockCenter = new Vector3(
                cell.x * cs + halfBlock, baseY, cell.y * cs + halfBlock);
            baseEdge = blockCenter + new Vector3(edgeDir.x * halfBlock, 0f, edgeDir.y * halfBlock);
        }
        else
        {
            float cellCenter = 0.5f * cs;
            baseEdge = new Vector3(cell.x * cs + cellCenter, baseY, cell.y * cs + cellCenter);
        }

        Vector3 localForward = rot * Vector3.forward;
        Vector3 rampCenter = baseEdge + localForward * (slopeLength * 0.5f);

        _ghost.transform.position = rampCenter;
        _ghost.transform.rotation = rot;
        _ghost.SetActive(true);

        SetGhostColor(_lastValid ? ValidColor : (rampExists ? OccupiedColor : InvalidColor));
    }

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
            _ghost.transform.localScale = scale;
            _ghostPrefabSource = null;
        }
    }

    private void EnsurePrefabGhost(GameObject prefab)
    {
        if (_ghost != null && _ghostPrefabSource == prefab) return;
        DestroyGhost();
        _ghost = CreateGhostFromPrefab(prefab);
        _ghostPrefabSource = prefab;
    }

    private GameObject CreateGhostFromPrefab(GameObject prefab)
    {
        var ghost = Instantiate(prefab);
        ghost.name = prefab.name + "_Ghost";

        // Strip network components (behaviours first, then object)
        foreach (var nb in ghost.GetComponentsInChildren<NetworkBehaviour>())
            DestroyImmediate(nb);
        foreach (var nob in ghost.GetComponentsInChildren<NetworkObject>())
            DestroyImmediate(nob);

        // Disable colliders
        foreach (var col in ghost.GetComponentsInChildren<Collider>())
            col.enabled = false;

        // Set ghost layer
        foreach (var t in ghost.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = PhysicsLayers.Decal;

        // Clone materials so we can tint them without affecting the prefab
        foreach (var r in ghost.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = new Material(mats[i]);
            r.sharedMaterials = mats;
        }

        ghost.SetActive(false);
        return ghost;
    }

    private void ApplyGhostColor(GameObject ghost, Color color)
    {
        foreach (var r in ghost.GetComponentsInChildren<Renderer>())
        {
            foreach (var mat in r.sharedMaterials)
                mat.color = color;
        }
    }

    private void SetGhostColor(Color color)
    {
        if (_ghost == null) return;

        if (_ghostMaterial != null)
        {
            _ghostMaterial.color = color;
        }
        else
        {
            ApplyGhostColor(_ghost, color);
        }
    }

    private void DestroyGhost()
    {
        if (_ghost != null)
        {
            Destroy(_ghost);
            _ghost = null;
            _ghostMaterial = null;
            _ghostPrefabSource = null;
        }
    }

    // -- OnGUI --

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

        if (_deleteMode)
        {
            GUILayout.BeginArea(new Rect(10, 50, 420, 50));
            GUILayout.Label("DELETE MODE  |  Click to delete  |  X or Esc to cancel");
            GUILayout.EndArea();
            return;
        }

        if (!_buildMode) return;

        int lineCount = 6;
        if (_zoopStartSet) lineCount++;
        if (_beltStartSet) lineCount++;

        GUILayout.BeginArea(new Rect(10, 50, 420, 22 * lineCount + 10));

        string levelMode = _levelOverrideFrames > 0 ? "(manual)" : "(auto)";
        string zoopLabel = _zoopMode ? "ZOOP" : "Single";
        GUILayout.Label($"BUILD MODE  |  Tool: {_currentTool}  |  Level: {_lastLevel} ({_lastLevel * FactoryGrid.LevelHeight}m) {levelMode}");
        GUILayout.Label($"1:Foundation 2:Wall 3:Ramp 4:Machine 5:Storage 6:Belt  |  Mode: {zoopLabel}");
        GUILayout.Label($"Rotation: {_placeRotation}  |  [R] Rotate  [X] Delete  [Z] Zoop  [G] Grid  [PgUp/Dn] Level");
        GUILayout.Label("[B] Exit  |  [Esc] Cancel  |  LMB: Place  |  RMB: Remove");

        if (_zoopStartSet)
        {
            GUILayout.Label($"{_currentTool} zoop: start ({_zoopStartCell.x},{_zoopStartCell.y}) -- click end");
        }

        if (_beltStartSet)
            GUILayout.Label($"Belt start: ({_beltStartCell.x},{_beltStartCell.y}) -- click end cell");

        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        CancelAllPending();
    }
}
