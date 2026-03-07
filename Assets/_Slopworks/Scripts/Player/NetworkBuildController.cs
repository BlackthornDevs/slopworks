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
    private Vector2Int _lastEdgeDir;
    private bool _lastValid;

    // Surface-based placement
    private float _surfaceY;
    private float _nudgeOffset;
    private float EffectiveY => _surfaceY + _nudgeOffset;

    // Machine/storage rotation
    private int _placeRotation;

    // Variant selection per tool type (Tab to cycle)
    private readonly int[] _variantIndex = new int[System.Enum.GetValues(typeof(BuildTool)).Length];

    // Delete mode (X key toggle, auto-exits after delete)
    private bool _deleteMode;
    private GameObject _deleteHighlight;
    private readonly List<(Renderer renderer, Material[] originals)> _deleteSavedMaterials = new();

    // Zoop mode (Z key toggle, 2-click start/end like belt)
    private bool _zoopMode;
    private bool _zoopStartSet;
    private Vector2Int _zoopStartCell;
    private Vector2Int _zoopStartDir; // for walls: edge direction at start
    private float _zoopStartSurfaceY;
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

    // Available recipes for cycling
    private static readonly string[] RecipeIds = { "smelt_iron", "smelt_copper" };

    private void Update()
    {
        if (!IsOwner) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // F key: interact with machine (set recipe)
        if (kb.fKey.wasPressedThisFrame)
            TryInteract();

        // Delete mode toggle (X key) -- works regardless of build mode
        if (kb.xKey.wasPressedThisFrame)
        {
            _deleteMode = !_deleteMode;
            _nudgeOffset = 0f;
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
            _nudgeOffset = 0f;
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

        // Surface Y detection and nudge
        UpdateSurfaceY();
        HandleNudge(kb);

        // Rotation: R key
        if (kb.rKey.wasPressedThisFrame)
        {
            _placeRotation = (_placeRotation + 90) % 360;
            Debug.Log($"build: rotation = {_placeRotation}");
        }

        // Variant cycle: Tab key
        if (kb.tabKey.wasPressedThisFrame)
            CycleVariant();

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
            if (placement.Category == BuildingCategory.Wall || placement.Category == BuildingCategory.Ramp)
                GridManager.Instance.CmdDeleteDirectional(placement.Cell, placement.SurfaceY, placement.EdgeDirection);
            else
                GridManager.Instance.CmdDelete(placement.Cell, placement.SurfaceY);
            Debug.Log($"build: deleted {placement.Category} at ({placement.Cell.x},{placement.Cell.y}) y={placement.SurfaceY:F1}");
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

    // -- Machine/Storage interaction --

    private void TryInteract()
    {
        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (!Physics.Raycast(ray, out var hit, _placementRange)) return;

        // Machine: cycle recipe
        var netMachine = hit.collider.GetComponentInParent<NetworkMachine>();
        if (netMachine != null)
        {
            string currentRecipe = netMachine.ActiveRecipeId;
            int nextIndex = 0;
            if (!string.IsNullOrEmpty(currentRecipe))
            {
                for (int i = 0; i < RecipeIds.Length; i++)
                {
                    if (RecipeIds[i] == currentRecipe)
                    {
                        nextIndex = (i + 1) % RecipeIds.Length;
                        break;
                    }
                }
            }

            netMachine.CmdSetRecipe(RecipeIds[nextIndex]);
            Debug.Log($"build: set machine recipe to {RecipeIds[nextIndex]}");
            return;
        }

        // Storage: deposit selected hotbar item
        var netStorage = hit.collider.GetComponentInParent<NetworkStorage>();
        if (netStorage != null)
        {
            var inventory = GetComponent<NetworkInventory>();
            if (inventory == null) return;

            var nob = netStorage.GetComponent<NetworkObject>();
            if (nob == null) return;

            inventory.CmdDepositIntoStorage(nob, inventory.SelectedHotbarIndex);
        }
    }

    // -- Surface Y detection and nudge --

    private void UpdateSurfaceY()
    {
        if (_zoopStartSet || _beltStartSet) return;

        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (Physics.Raycast(ray, out var hit, _placementRange, StructuralMask))
        {
            var info = hit.collider.GetComponentInParent<PlacementInfo>();
            if (info != null)
                _surfaceY = info.SurfaceY + info.ObjectHeight;
            else
                _surfaceY = hit.point.y;
        }
    }

    private void HandleNudge(Keyboard kb)
    {
        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        float step = shift ? 0.5f : 1f;

        if (kb.pageUpKey.wasPressedThisFrame)
        {
            _nudgeOffset += step;
            Debug.Log($"build: nudge +{_nudgeOffset:F1}m");
        }
        if (kb.pageDownKey.wasPressedThisFrame)
        {
            _nudgeOffset -= step;
            Debug.Log($"build: nudge {_nudgeOffset:F1}m");
        }
    }

    // -- Tool switching --

    private void SwitchTool(BuildTool tool)
    {
        if (_currentTool == tool) return;
        CancelAllPending();
        _currentTool = tool;
        _nudgeOffset = 0f;
        if (tool == BuildTool.Machine || tool == BuildTool.Storage)
            _placeRotation = 0;
        Debug.Log($"build: tool = {tool}");
    }

    private int CurrentVariant => _variantIndex[(int)_currentTool];

    private GameObject[] GetVariantsForCurrentTool()
    {
        var gm = GridManager.Instance;
        if (gm == null) return null;
        return gm.GetPrefabs(ToolToCategory(_currentTool));
    }

    private GameObject GetSelectedPrefab()
    {
        var gm = GridManager.Instance;
        if (gm == null) return null;
        return gm.GetPrefab(ToolToCategory(_currentTool), CurrentVariant);
    }

    private static BuildingCategory ToolToCategory(BuildTool tool)
    {
        return tool switch
        {
            BuildTool.Foundation => BuildingCategory.Foundation,
            BuildTool.Wall => BuildingCategory.Wall,
            BuildTool.Ramp => BuildingCategory.Ramp,
            BuildTool.Machine => BuildingCategory.Machine,
            BuildTool.Storage => BuildingCategory.Storage,
            BuildTool.Belt => BuildingCategory.Belt,
            _ => BuildingCategory.Foundation
        };
    }

    private void CycleVariant()
    {
        var variants = GetVariantsForCurrentTool();
        if (variants == null || variants.Length <= 1) return;

        int idx = (int)_currentTool;
        _variantIndex[idx] = (_variantIndex[idx] + 1) % variants.Length;
        DestroyAllGhosts(); // Destroy all ghost pools so they rebuild from new prefab
        Debug.Log($"build: variant {_variantIndex[idx] + 1}/{variants.Length} ({variants[_variantIndex[idx]].name})");
    }

    private void DestroyAllGhosts()
    {
        DestroyGhost();
        DestroyGhostPool(_ghostPool);
        DestroyGhostPool(_zoopGhosts);
        DestroyGhostPool(_rampZoopGhosts);
    }

    private static void DestroyGhostPool(List<GameObject> pool)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null) Destroy(pool[i]);
        }
        pool.Clear();
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
    private bool RaycastWallPlacement(out Vector2Int cell, out Vector2Int edgeDir)
    {
        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (!Physics.Raycast(ray, out var hit, _placementRange, StructuralMask))
        {
            cell = Vector2Int.zero;
            edgeDir = Vector2Int.up;
            return false;
        }

        var placement = hit.collider.GetComponentInParent<PlacementInfo>();
        if (placement != null && placement.Category == BuildingCategory.Foundation)
        {
            _surfaceY = placement.SurfaceY + placement.ObjectHeight;
            edgeDir = GetFacingEdgeDirection();
            cell = placement.Cell;
            return true;
        }

        _surfaceY = hit.point.y;
        cell = GridManager.Instance.Grid.WorldToCell(hit.point);
        edgeDir = GetFacingEdgeDirection();
        return true;
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
                    _zoopStartSurfaceY = EffectiveY;
                    Debug.Log($"build: foundation zoop start ({centered.x},{centered.y}) y={EffectiveY:F1} -- click end");
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
                GridManager.Instance.CmdPlace(centered, EffectiveY, 0, CurrentVariant, BuildingCategory.Foundation);
            }
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            GridManager.Instance.CmdDelete(cell, EffectiveY);
        }
    }

    private void ShowFoundationGhostSingle(Vector2Int snapped)
    {
        var gm = GridManager.Instance;
        int fs = FactoryGrid.FoundationSize;
        bool valid = gm.Grid.CanPlace(snapped, new Vector2Int(fs, fs), EffectiveY);

        while (_ghostPool.Count < 1)
            _ghostPool.Add(CreateFoundationGhost());

        _ghostPool[0].SetActive(true);
        _ghostPool[0].transform.position = gm.GetFoundationPlacementPos(snapped, EffectiveY, GetSelectedPrefab());
        ApplyGhostColor(_ghostPool[0], valid ? ValidColor : InvalidColor);

        for (int i = 1; i < _ghostPool.Count; i++)
            _ghostPool[i].SetActive(false);
    }

    private void UpdateFoundationZoopPreview(Vector2Int endCell)
    {
        var gm = GridManager.Instance;
        int fs = FactoryGrid.FoundationSize;

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
                bool valid = gm.Grid.CanPlace(origin, new Vector2Int(fs, fs), _zoopStartSurfaceY);

                _ghostPool[idx].SetActive(true);
                _ghostPool[idx].transform.position = gm.GetFoundationPlacementPos(origin, _zoopStartSurfaceY, GetSelectedPrefab());
                ApplyGhostColor(_ghostPool[idx], valid ? ValidColor : InvalidColor);
                idx++;
            }
        }

        for (int i = idx; i < _ghostPool.Count; i++)
            _ghostPool[i].SetActive(false);
    }

    private GameObject CreateFoundationGhost()
    {
        return CreateGhostFromPrefab(GetSelectedPrefab());
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
                GridManager.Instance.CmdPlace(cell, _zoopStartSurfaceY, 0, CurrentVariant, BuildingCategory.Foundation);
            }
        }
        Debug.Log($"build: foundation zoop placed at y={_zoopStartSurfaceY:F1}");
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
        bool hasHit = RaycastWallPlacement(out var cell, out var edgeDir);

        if (!hasHit && !_zoopStartSet)
        {
            DestroyGhost();
            return;
        }

        if (hasHit)
        {
            _lastWallCell = cell;
        }

        if (_zoopMode)
        {
            if (!_zoopStartSet)
            {
                if (!hasHit) return;
                UpdateWallGhost(cell, edgeDir);

                if (mouse.leftButton.wasPressedThisFrame && _lastValid)
                {
                    _zoopStartSet = true;
                    _zoopStartCell = cell;
                    _zoopStartDir = edgeDir;
                    _zoopStartSurfaceY = EffectiveY;
                    DestroyGhost();
                    Debug.Log($"build: wall zoop start ({cell.x},{cell.y}) y={EffectiveY:F1} -- click end");
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
            UpdateWallGhost(cell, edgeDir);

            if (mouse.leftButton.wasPressedThisFrame && _lastValid)
            {
                GridManager.Instance.CmdPlaceDirectional(cell, EffectiveY, edgeDir, CurrentVariant, BuildingCategory.Wall);
            }
        }

        if (mouse.rightButton.wasPressedThisFrame && !_zoopStartSet)
        {
            GridManager.Instance.CmdDeleteDirectional(cell, EffectiveY, edgeDir);
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

            bool wallExists = GridManager.Instance.HasDirectionalAt(wallCell, _zoopStartSurfaceY, _zoopStartDir);
            if (!wallExists)
                _wallZoopCells.Add(wallCell);
        }

        var gm = GridManager.Instance;
        var prefab = GetSelectedPrefab();

        while (_zoopGhosts.Count < _wallZoopCells.Count)
            _zoopGhosts.Add(CreateGhostFromPrefab(prefab));

        for (int i = 0; i < _wallZoopCells.Count; i++)
        {
            var ghost = _zoopGhosts[i];
            ghost.SetActive(true);
            gm.GetDirectionalPlacement(_wallZoopCells[i], _zoopStartSurfaceY, _zoopStartDir, prefab,
                BuildingCategory.Wall, out var pos, out var rot);
            ghost.transform.position = pos;
            ghost.transform.rotation = rot;
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
            gm.CmdPlaceDirectional(_wallZoopCells[i], _zoopStartSurfaceY, _zoopStartDir, CurrentVariant, BuildingCategory.Wall);
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
        bool hasHit = RaycastWallPlacement(out var cell, out var edgeDir);

        if (!hasHit && !_zoopStartSet)
        {
            DestroyGhost();
            return;
        }

        if (hasHit)
        {
            _lastRampCell = cell;
        }

        if (_zoopMode)
        {
            if (!_zoopStartSet)
            {
                if (!hasHit) return;
                _lastEdgeDir = edgeDir;
                UpdateRampGhost(cell, edgeDir);

                if (mouse.leftButton.wasPressedThisFrame && _lastValid)
                {
                    _zoopStartSet = true;
                    _zoopStartCell = cell;
                    _zoopStartDir = edgeDir;
                    _zoopStartSurfaceY = EffectiveY;
                    DestroyGhost();
                    Debug.Log($"build: ramp zoop start ({cell.x},{cell.y}) y={EffectiveY:F1} -- click end");
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
            _lastEdgeDir = edgeDir;
            UpdateRampGhost(cell, edgeDir);

            if (mouse.leftButton.wasPressedThisFrame && _lastValid)
            {
                GridManager.Instance.CmdPlaceDirectional(cell, EffectiveY, edgeDir, CurrentVariant, BuildingCategory.Ramp);
            }
        }

        if (mouse.rightButton.wasPressedThisFrame && !_zoopStartSet)
        {
            GridManager.Instance.CmdDeleteDirectional(cell, EffectiveY, edgeDir);
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

            bool rampExists = GridManager.Instance.HasDirectionalAt(rampCell, _zoopStartSurfaceY, _zoopStartDir);
            if (!rampExists)
                _rampZoopCells.Add(rampCell);
        }

        var gm = GridManager.Instance;
        var prefab = GetSelectedPrefab();

        while (_rampZoopGhosts.Count < _rampZoopCells.Count)
            _rampZoopGhosts.Add(CreateGhostFromPrefab(prefab));

        for (int i = 0; i < _rampZoopCells.Count; i++)
        {
            var ghost = _rampZoopGhosts[i];
            ghost.SetActive(true);
            gm.GetDirectionalPlacement(_rampZoopCells[i], _zoopStartSurfaceY, _zoopStartDir, prefab,
                BuildingCategory.Ramp, out var rampPos, out var rampRot);
            ghost.transform.position = rampPos;
            ghost.transform.rotation = rampRot;
            ApplyGhostColor(ghost, ValidColor);
        }

        for (int i = _rampZoopCells.Count; i < _rampZoopGhosts.Count; i++)
            _rampZoopGhosts[i].SetActive(false);
    }

    private void PlaceRampZoop()
    {
        var gm = GridManager.Instance;
        for (int i = 0; i < _rampZoopCells.Count; i++)
        {
            gm.CmdPlaceDirectional(_rampZoopCells[i], _zoopStartSurfaceY, _zoopStartDir, CurrentVariant, BuildingCategory.Ramp);
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

        var gm = GridManager.Instance;
        bool cellFree = !gm.HasBuildingAt(cell, EffectiveY);
        _lastValid = cellFree;

        var prefab = GetSelectedPrefab();
        if (prefab != null)
            EnsurePrefabGhost(prefab);
        else
            EnsureGhost(new Vector3(FactoryGrid.CellSize * 0.8f, 0.5f, FactoryGrid.CellSize * 0.8f));

        _ghost.transform.position = gm.GetPlacementPos(cell, EffectiveY, prefab);
        _ghost.transform.rotation = Quaternion.Euler(0f, _placeRotation, 0f);
        _ghost.SetActive(true);
        SetGhostColor(_lastValid ? new Color(0.8f, 0.5f, 0f, 0.5f) : InvalidColor);

        if (mouse.leftButton.wasPressedThisFrame && _lastValid)
        {
            gm.CmdPlace(cell, EffectiveY, _placeRotation, CurrentVariant, BuildingCategory.Machine);
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

        var gm = GridManager.Instance;
        bool cellFree = !gm.HasBuildingAt(cell, EffectiveY);
        _lastValid = cellFree;

        var prefab = GetSelectedPrefab();
        if (prefab != null)
            EnsurePrefabGhost(prefab);
        else
            EnsureGhost(new Vector3(FactoryGrid.CellSize * 0.8f, 0.4f, FactoryGrid.CellSize * 0.8f));

        _ghost.transform.position = gm.GetPlacementPos(cell, EffectiveY, prefab);
        _ghost.transform.rotation = Quaternion.Euler(0f, _placeRotation, 0f);
        _ghost.SetActive(true);
        SetGhostColor(_lastValid ? new Color(0.3f, 0.3f, 1f, 0.5f) : InvalidColor);

        if (mouse.leftButton.wasPressedThisFrame && _lastValid)
        {
            gm.CmdPlace(cell, EffectiveY, _placeRotation, CurrentVariant, BuildingCategory.Storage);
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
            _ghost.transform.position = GridManager.Instance.GetPlacementPos(cell, EffectiveY, GetSelectedPrefab());
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
                    GridManager.Instance.CmdPlaceBelt(_beltStartCell, snappedEnd, EffectiveY, CurrentVariant);
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

        var gm = GridManager.Instance;
        var startWorld = gm.GetPlacementPos(_beltStartCell, EffectiveY, GetSelectedPrefab());
        var endWorld = gm.GetPlacementPos(snappedEnd, EffectiveY, GetSelectedPrefab());
        var center = (startWorld + endWorld) * 0.5f;
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

    private void UpdateWallGhost(Vector2Int cell, Vector2Int edgeDir)
    {
        var gm = GridManager.Instance;
        bool wallExists = gm.HasDirectionalAt(cell, EffectiveY, edgeDir);
        _lastValid = !wallExists;

        EnsurePrefabGhost(GetSelectedPrefab());

        gm.GetDirectionalPlacement(cell, EffectiveY, edgeDir, GetSelectedPrefab(),
            BuildingCategory.Wall, out var pos, out var rot);
        _ghost.transform.position = pos;
        _ghost.transform.rotation = rot;
        _ghost.SetActive(true);

        SetGhostColor(_lastValid ? ValidColor : OccupiedColor);
    }

    private void UpdateRampGhost(Vector2Int cell, Vector2Int edgeDir)
    {
        var gm = GridManager.Instance;
        bool rampExists = gm.HasDirectionalAt(cell, EffectiveY, edgeDir);
        _lastValid = !rampExists;

        EnsurePrefabGhost(GetSelectedPrefab());

        gm.GetDirectionalPlacement(cell, EffectiveY, edgeDir, GetSelectedPrefab(),
            BuildingCategory.Ramp, out var pos, out var rot);
        _ghost.transform.position = pos;
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

        // Interaction prompt when looking at machines/storage
        ShowInteractionPrompt();

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

        GUILayout.BeginArea(new Rect(10, 50, 520, 22 * lineCount + 10));

        string zoopLabel = _zoopMode ? "ZOOP" : "Single";
        var variants = GetVariantsForCurrentTool();
        string variantLabel = (variants != null && variants.Length > 1)
            ? $"  |  Variant: {CurrentVariant + 1}/{variants.Length} ({variants[CurrentVariant].name})"
            : "";
        string surfaceLabel = _nudgeOffset != 0f
            ? $"Surface: {EffectiveY:F1}m (nudge: {_nudgeOffset:+0.0;-0.0}m)"
            : $"Surface: {EffectiveY:F1}m";
        GUILayout.Label($"BUILD MODE  |  Tool: {_currentTool}  |  {surfaceLabel}{variantLabel}");
        GUILayout.Label($"1:Foundation 2:Wall 3:Ramp 4:Machine 5:Storage 6:Belt  |  Mode: {zoopLabel}");
        GUILayout.Label($"Rotation: {_placeRotation}  |  [R] Rotate  [X] Delete  [Z] Zoop  [G] Grid  [Tab] Variant  [PgUp/Dn] Nudge (+Shift: 0.5m)");
        GUILayout.Label("[B] Exit  |  [Esc] Cancel  |  LMB: Place  |  RMB: Remove");

        if (_zoopStartSet)
        {
            GUILayout.Label($"{_currentTool} zoop: start ({_zoopStartCell.x},{_zoopStartCell.y}) -- click end");
        }

        if (_beltStartSet)
            GUILayout.Label($"Belt start: ({_beltStartCell.x},{_beltStartCell.y}) -- click end cell");

        GUILayout.EndArea();
    }

    private void ShowInteractionPrompt()
    {
        if (_buildMode || _deleteMode) return;

        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (!Physics.Raycast(ray, out var hit, _placementRange)) return;

        var netMachine = hit.collider.GetComponentInParent<NetworkMachine>();
        if (netMachine != null)
        {
            string recipe = string.IsNullOrEmpty(netMachine.ActiveRecipeId) ? "none" : netMachine.ActiveRecipeId;
            string status = netMachine.Status.ToString();
            float progress = netMachine.CraftProgress;

            GUILayout.BeginArea(new Rect(Screen.width / 2f - 120, Screen.height / 2f + 30, 240, 80));
            GUI.color = Color.white;
            GUILayout.Label($"Machine ({status})");
            GUILayout.Label($"Recipe: {recipe}  Progress: {progress:F1}s");
            GUILayout.Label("[F] Cycle recipe");
            GUILayout.EndArea();
            return;
        }

        var netStorage = hit.collider.GetComponentInParent<NetworkStorage>();
        if (netStorage != null)
        {
            int usedSlots = 0;
            for (int i = 0; i < netStorage.SlotCount; i++)
            {
                if (!netStorage.GetSlot(i).IsEmpty)
                    usedSlots++;
            }

            GUILayout.BeginArea(new Rect(Screen.width / 2f - 120, Screen.height / 2f + 30, 240, 60));
            GUI.color = Color.white;
            GUILayout.Label($"Storage ({usedSlots}/{netStorage.SlotCount} slots used)");
            GUILayout.Label("[F] Deposit held item");
            GUILayout.EndArea();
        }
    }

    private void OnDestroy()
    {
        CancelAllPending();
    }
}
