using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkBuildController : NetworkBehaviour
{
    [SerializeField] private float _placementRange = 50f;

    private enum BuildTool { Foundation, Wall, Ramp, Machine, Storage, Belt }
    private enum PlacementMode { None, Grid, Snap }

    private Camera _camera;
    private bool _buildMode;
    private BuildTool _currentTool;
    // Surface-based placement
    private float _surfaceY;
    private float _nudgeOffset;
    private float EffectiveY => _surfaceY + _nudgeOffset;

    // Machine/storage rotation
    private int _placeRotation;

    // Unified placement state
    private PlacementMode _placementMode;
    private BuildingSnapPoint _activeSnapPoint;

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
        (1 << PhysicsLayers.Terrain) | (1 << PhysicsLayers.Structures) | (1 << PhysicsLayers.SnapPoints);

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
        if (_currentTool == BuildTool.Belt)
            HandleBeltInput(mouse);
        else
            HandleBuildInput(mouse);
    }

    // -- Unified build input (all tools except belt) --

    private void HandleBuildInput(Mouse mouse)
    {
        if (!RaycastPlacement(out var hit))
        {
            HideGhost();
            return;
        }

        var prefab = GetSelectedPrefab();
        if (prefab == null)
        {
            HideGhost();
            return;
        }

        Vector3 ghostPos;
        Quaternion ghostRot;

        if (_placementMode == PlacementMode.Snap && _activeSnapPoint != null)
        {
            int effectiveRotation;
            if (Mathf.Abs(_activeSnapPoint.Normal.y) > 0.9f)
            {
                effectiveRotation = _placeRotation;
            }
            else
            {
                var targetInfo = _activeSnapPoint.GetComponentInParent<PlacementInfo>();
                bool isWallOnWall = targetInfo != null
                    && targetInfo.Category == BuildingCategory.Wall
                    && _currentTool == BuildTool.Wall;
                bool isRampOnRamp = targetInfo != null
                    && targetInfo.Category == BuildingCategory.Ramp
                    && _currentTool == BuildTool.Ramp;

                int baseYaw;
                if (isWallOnWall || isRampOnRamp)
                {
                    baseYaw = Mathf.RoundToInt(_activeSnapPoint.transform.root.eulerAngles.y);
                }
                else
                {
                    float autoYaw = Mathf.Atan2(_activeSnapPoint.Normal.x, _activeSnapPoint.Normal.z) * Mathf.Rad2Deg;
                    baseYaw = Mathf.RoundToInt(autoYaw);
                }

                effectiveRotation = (baseYaw + _placeRotation) % 360;
            }

            var result = GridManager.GetSnapPlacementPosition(
                _activeSnapPoint, prefab, effectiveRotation, _surfaceY);
            ghostPos = result.position + new Vector3(0f, _nudgeOffset, 0f);
            ghostRot = result.rotation;
        }
        else
        {
            var effectiveHit = new Vector3(hit.point.x, EffectiveY, hit.point.z);
            var result = GridManager.GetGridPlacementPosition(
                effectiveHit, prefab, _placeRotation);
            ghostPos = result.position;
            ghostRot = result.rotation;
        }

        // Zoop: grid-mode batch placement
        var cell = GridManager.Instance.Grid.WorldToCell(ghostPos);
        if (_zoopMode && _placementMode == PlacementMode.Grid)
        {
            HandleZoopInput(mouse, ghostPos, ghostRot, cell);
            return;
        }

        // Show ghost
        EnsurePrefabGhost(prefab);
        _ghost.transform.position = ghostPos;
        _ghost.transform.rotation = ghostRot;
        _ghost.SetActive(true);

        // Validity check
        var category = ToolToCategory(_currentTool);
        float placeSurfaceY = _surfaceY + _nudgeOffset;
        bool valid = !GridManager.Instance.HasBuildingAt(cell, placeSurfaceY);
        SetGhostColor(valid ? ValidColor : InvalidColor);

        // Place on left click
        if (mouse.leftButton.wasPressedThisFrame && valid)
        {
            int rotDeg = Mathf.RoundToInt(ghostRot.eulerAngles.y);

            if (category == BuildingCategory.Wall || category == BuildingCategory.Ramp)
            {
                var dir = RotationToDirection(rotDeg);
                GridManager.Instance.CmdPlaceDirectional(cell, placeSurfaceY, dir, CurrentVariant, category, ghostPos, ghostRot);
            }
            else
            {
                GridManager.Instance.CmdPlace(cell, placeSurfaceY, rotDeg, CurrentVariant, category, ghostPos);
            }
            Debug.Log($"build: placed {category} at ({cell.x},{cell.y}) y={placeSurfaceY:F1}");
        }

        // Delete on right click
        if (mouse.rightButton.wasPressedThisFrame)
        {
            var hitCell = GridManager.Instance.Grid.WorldToCell(hit.point);
            GridManager.Instance.CmdDelete(hitCell, placeSurfaceY);
        }
    }

    private void HideGhost()
    {
        if (_ghost != null) _ghost.SetActive(false);
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

    private static Vector2Int RotationToDirection(int rotDeg)
    {
        return rotDeg switch
        {
            0 => Vector2Int.up,
            90 => Vector2Int.right,
            180 => Vector2Int.down,
            270 => Vector2Int.left,
            _ => Vector2Int.up
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
        // Destroy zoop ghosts -- pool may contain ghosts from a different tool
        DestroyGhostPool(_zoopGhosts);
    }

    // -- Zoop (batch placement) --

    private void HandleZoopInput(Mouse mouse, Vector3 currentPos, Quaternion currentRot, Vector2Int currentCell)
    {
        var prefab = GetSelectedPrefab();
        if (prefab == null) return;

        if (!_zoopStartSet)
        {
            // Show single ghost at current position
            EnsurePrefabGhost(prefab);
            _ghost.transform.position = currentPos;
            _ghost.transform.rotation = currentRot;
            _ghost.SetActive(true);
            SetGhostColor(ValidColor);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _zoopStartSet = true;
                _zoopStartCell = currentCell;
                _zoopStartSurfaceY = EffectiveY;
                Debug.Log($"build: zoop start ({currentCell.x},{currentCell.y})");
            }
            return;
        }

        // Show preview line from start to current
        UpdateZoopPreview(currentCell, currentRot);

        if (mouse.leftButton.wasPressedThisFrame)
        {
            PlaceZoopLine(_zoopStartCell, currentCell, currentRot);
            CancelZoop();
        }
        if (mouse.rightButton.wasPressedThisFrame)
            CancelZoop();
    }

    private void UpdateZoopPreview(Vector2Int endCell, Quaternion rot)
    {
        var prefab = GetSelectedPrefab();
        if (prefab == null) return;

        var cells = GetZoopCells(_zoopStartCell, endCell);

        // Ensure enough ghosts in pool
        while (_zoopGhosts.Count < cells.Count)
            _zoopGhosts.Add(CreateGhostFromPrefab(prefab));

        for (int i = 0; i < _zoopGhosts.Count; i++)
        {
            if (i < cells.Count)
            {
                float cs = FactoryGrid.CellSize;
                var worldHit = new Vector3(cells[i].x * cs + cs * 0.5f, _zoopStartSurfaceY, cells[i].y * cs + cs * 0.5f);
                var result = GridManager.GetGridPlacementPosition(worldHit, prefab, Mathf.RoundToInt(rot.eulerAngles.y));
                _zoopGhosts[i].transform.position = result.position;
                _zoopGhosts[i].transform.rotation = result.rotation;
                _zoopGhosts[i].SetActive(true);
                ApplyGhostColor(_zoopGhosts[i], ValidColor);
            }
            else
            {
                _zoopGhosts[i].SetActive(false);
            }
        }

        // Hide single ghost during preview
        if (_ghost != null) _ghost.SetActive(false);
    }

    private List<Vector2Int> GetZoopCells(Vector2Int start, Vector2Int end)
    {
        var cells = new List<Vector2Int>();
        int dx = end.x - start.x;
        int dz = end.y - start.y;

        // Walk along dominant axis
        if (Mathf.Abs(dx) >= Mathf.Abs(dz))
        {
            int step = dx >= 0 ? 1 : -1;
            var prefab = GetSelectedPrefab();
            var extents = GridManager.GetPrefabExtents(prefab);
            int footprintCells = Mathf.Max(1, Mathf.RoundToInt(extents.x * 2f / FactoryGrid.CellSize));

            for (int x = start.x; step > 0 ? x <= end.x : x >= end.x; x += step * footprintCells)
                cells.Add(new Vector2Int(x, start.y));
        }
        else
        {
            int step = dz >= 0 ? 1 : -1;
            var prefab = GetSelectedPrefab();
            var extents = GridManager.GetPrefabExtents(prefab);
            int footprintCells = Mathf.Max(1, Mathf.RoundToInt(extents.z * 2f / FactoryGrid.CellSize));

            for (int z = start.y; step > 0 ? z <= end.y : z >= end.y; z += step * footprintCells)
                cells.Add(new Vector2Int(start.x, z));
        }

        return cells;
    }

    private void PlaceZoopLine(Vector2Int start, Vector2Int end, Quaternion rot)
    {
        var prefab = GetSelectedPrefab();
        if (prefab == null) return;

        var category = ToolToCategory(_currentTool);
        int rotDeg = Mathf.RoundToInt(rot.eulerAngles.y);
        var cells = GetZoopCells(start, end);

        foreach (var cell in cells)
        {
            float cs = FactoryGrid.CellSize;
            var worldHit = new Vector3(cell.x * cs + cs * 0.5f, _zoopStartSurfaceY, cell.y * cs + cs * 0.5f);
            var result = GridManager.GetGridPlacementPosition(worldHit, prefab, rotDeg);

            if (category == BuildingCategory.Wall || category == BuildingCategory.Ramp)
            {
                var dir = RotationToDirection(rotDeg);
                GridManager.Instance.CmdPlaceDirectional(cell, _zoopStartSurfaceY, dir, CurrentVariant, category, result.position, result.rotation);
            }
            else
            {
                GridManager.Instance.CmdPlace(cell, _zoopStartSurfaceY, rotDeg, CurrentVariant, category, result.position);
            }
        }

        Debug.Log($"build: zoop placed {cells.Count} {category}");
    }

    // -- Raycast helpers --

    /// <summary>
    /// Unified raycast for all build tools. Sets _placementMode and _activeSnapPoint
    /// based on what the ray hits (snap point on existing building vs terrain grid).
    /// </summary>
    private bool RaycastPlacement(out RaycastHit hit)
    {
        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (!Physics.Raycast(ray, out hit, _placementRange, StructuralMask))
        {
            _placementMode = PlacementMode.None;
            _activeSnapPoint = null;
            return false;
        }

        // Check if we hit a snap point collider directly
        // Skip during zoop -- zoop always uses grid placement
        if (!_zoopMode)
        {
            var directSnap = hit.collider.GetComponent<BuildingSnapPoint>();
            if (directSnap == null)
                directSnap = hit.collider.GetComponentInParent<BuildingSnapPoint>();

            var placement = directSnap != null
                ? directSnap.GetComponentInParent<PlacementInfo>()
                : hit.collider.GetComponentInParent<PlacementInfo>();

            // Direct hit on snap point sphere
            BuildingSnapPoint chosen = directSnap;

            // Fallback: hit building mesh, find nearest snap point
            if (chosen == null && placement != null)
                chosen = BuildingSnapPoint.FindNearest(placement.gameObject, hit.point, hit.normal);

            if (chosen != null && placement != null)
            {
                _placementMode = PlacementMode.Snap;
                _activeSnapPoint = chosen;
                _surfaceY = ComputeSnapSurfaceY(chosen, placement);
                return true;
            }
        }

        // Terrain hit -- grid mode
        _placementMode = PlacementMode.Grid;
        _activeSnapPoint = null;
        _surfaceY = hit.point.y;
        return true;
    }

    private float ComputeSnapSurfaceY(BuildingSnapPoint snap, PlacementInfo placement)
    {
        // The snap point's world Y already encodes the correct attachment height.
        // Top snap on a foundation = foundation top. Mid snap = face center. etc.
        // No category-specific logic needed.
        return snap.transform.position.y;
    }

    // -- Belt: 2-click placement --

    private void HandleBeltInput(Mouse mouse)
    {
        if (!RaycastPlacement(out var hit))
        {
            if (!_beltStartSet) DestroyGhost();
            return;
        }

        var cell = GridManager.Instance.Grid.WorldToCell(hit.point);

        if (!_beltStartSet)
        {
            EnsureGhost(new Vector3(0.6f, 0.08f, 0.6f));
            _ghost.transform.position = BeltCellCenter(cell, EffectiveY);
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

        var startWorld = BeltCellCenter(_beltStartCell, EffectiveY);
        var endWorld = BeltCellCenter(snappedEnd, EffectiveY);
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

    /// <summary>
    /// Returns the world-space center of a cell at the given surface Y, offset by belt half-height.
    /// </summary>
    private static Vector3 BeltCellCenter(Vector2Int cell, float surfaceY)
    {
        float cs = FactoryGrid.CellSize;
        return new Vector3(
            cell.x * cs + cs * 0.5f,
            surfaceY + 0.04f,
            cell.y * cs + cs * 0.5f);
    }

    // -- Ghost helpers --

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

        int lineCount = 7;
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

        if (_placementMode == PlacementMode.Snap && _activeSnapPoint != null)
            GUILayout.Label($"Snap: {_activeSnapPoint.Normal:F1} on {_activeSnapPoint.transform.parent?.name}");
        else
            GUILayout.Label("Mode: Grid");

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
