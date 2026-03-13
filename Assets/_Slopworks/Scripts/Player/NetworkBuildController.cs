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
    private Vector3 _zoopStartPos;
    private Quaternion _zoopStartRot;
    private float _zoopStartSurfaceY;
    private readonly List<GameObject> _ghostPool = new();
    private readonly List<GameObject> _zoopGhosts = new();

    // Belt spline placement
    private enum BeltPlacementState { Idle, PickingStart, Dragging }
    private BeltPlacementState _beltState = BeltPlacementState.Idle;
    private Vector3 _beltStartPos;       // preview position (at anchor height for free endpoints)
    private Vector3 _beltStartGroundPos; // raw ground position sent to server
    private Vector3 _beltStartDir;
    private bool _beltStartFromPort;
    private bool _beltEndFromPort;
    private BeltRoutingMode _beltRoutingMode = BeltRoutingMode.Default;
    private GameObject _beltPreviewLine;
    private LineRenderer _beltLineRenderer;

    // Belt support ghosts
    private GameObject _beltStartSupportGhost;
    private GameObject _beltEndSupportGhost;

    // Single ghost for simple tools
    private GameObject _ghost;
    private Material _ghostMaterial;
    private GameObject _ghostPrefabSource;
    private readonly List<Material> _ghostMaterials = new();

    private GridOverlay _gridOverlay;

    // Snap mode toggle: center snaps (default) vs edge snaps (scroll wheel)
    private bool _edgeSnapMode;
    // Machine/Storage snap target: false = foundation/grid (default), true = peer machine/storage
    private bool _peerSnapMode;
    private GameObject _snapHighlight;

    private static readonly int StructuralMask =
        (1 << PhysicsLayers.Terrain) | (1 << PhysicsLayers.Structures) | (1 << PhysicsLayers.SnapPoints);
    private static readonly int DeleteMask =
        (1 << PhysicsLayers.Structures) | (1 << PhysicsLayers.Interactable);

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

        // Tab key: toggle belt routing mode when on Belt tool, otherwise cycle variant
        if (kb.tabKey.wasPressedThisFrame)
        {
            if (_currentTool == BuildTool.Belt)
            {
                _beltRoutingMode = _beltRoutingMode switch
                {
                    BeltRoutingMode.Default => BeltRoutingMode.Straight,
                    BeltRoutingMode.Straight => BeltRoutingMode.Curved,
                    _ => BeltRoutingMode.Default
                };
                Debug.Log($"belt: routing mode = {_beltRoutingMode}");
            }
            else
            {
                CycleVariant();
            }
        }

        // Snap mode toggle: scroll wheel swaps snap filter
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.1f)
        {
            if (_currentTool == BuildTool.Machine || _currentTool == BuildTool.Storage)
            {
                _peerSnapMode = !_peerSnapMode;
                Debug.Log($"build: snap target = {(_peerSnapMode ? "MACHINE/STORAGE" : "FOUNDATION")}");
            }
            else
            {
                _edgeSnapMode = !_edgeSnapMode;
                Debug.Log($"build: snap filter = {(_edgeSnapMode ? "EDGE" : "CENTER")}");
            }
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
        if (_currentTool == BuildTool.Belt)
            HandleBeltInput(mouse);
        else
            HandleBuildInput(mouse);
    }

    // -- Unified build input (all tools except belt) --

    private void HandleBuildInput(Mouse mouse)
    {
        // Zoop after start: ray-plane intersection at anchor height.
        // MaxZoopCount in GetZoopCells prevents runaway ghost creation.
        if (_zoopMode && _zoopStartSet)
        {
            var ray = new Ray(_camera.transform.position, _camera.transform.forward);
            var plane = new Plane(Vector3.up, _zoopStartPos);
            Vector3 planePoint;
            if (plane.Raycast(ray, out float enter) && enter < _placementRange)
            {
                planePoint = ray.GetPoint(enter);
            }
            else
            {
                planePoint = _zoopStartPos + Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized * _placementRange;
            }

            var endCell = GridManager.Instance.Grid.WorldToCell(
                new Vector3(Mathf.Round(planePoint.x), _zoopStartPos.y, Mathf.Round(planePoint.z)));
            HandleZoopInput(mouse, _zoopStartPos, _zoopStartRot, endCell);
            return;
        }

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
            var targetInfo = _activeSnapPoint.GetComponentInParent<PlacementInfo>();
            var ghostCategory = ToolToCategory(_currentTool);
            var targetCategory = targetInfo != null ? targetInfo.Category : BuildingCategory.Foundation;

            int effectiveRotation;
            if (Mathf.Abs(_activeSnapPoint.Normal.y) > 0.9f)
            {
                effectiveRotation = _placeRotation;
            }
            else
            {
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
                _activeSnapPoint, prefab, effectiveRotation, _surfaceY, ghostCategory, targetCategory);
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

        // Zoop first click: uses whatever placement mode is active (grid or snap)
        var cell = GridManager.Instance.Grid.WorldToCell(ghostPos);
        if (_zoopMode)
        {
            HandleZoopInput(mouse, ghostPos, ghostRot, cell);
            return;
        }

        // Show ghost
        EnsurePrefabGhost(prefab);
        _ghost.transform.position = ghostPos;
        _ghost.transform.rotation = ghostRot;
        _ghost.SetActive(true);

        // Validity check -- derive surfaceY from ghost position so bottom snaps
        // don't collide with the existing building's record key
        var category = ToolToCategory(_currentTool);
        float placeSurfaceY = ghostPos.y - GridManager.GetPrefabBaseOffset(prefab);
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
        HideSnapHighlight();
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

        // Raycast against placed buildings only (not snap points or terrain)
        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (!Physics.Raycast(ray, out var hit, _placementRange, DeleteMask))
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
            if (placement.Category == BuildingCategory.Belt || placement.Category == BuildingCategory.Support)
            {
                var nob = placement.GetComponent<NetworkObject>();
                if (nob != null)
                    CmdDeleteBelt(nob);
            }
            else if (placement.Category == BuildingCategory.Wall || placement.Category == BuildingCategory.Ramp)
                GridManager.Instance.CmdDeleteDirectional(placement.Cell, placement.SurfaceY, placement.EdgeDirection);
            else
                GridManager.Instance.CmdDelete(placement.Cell, placement.SurfaceY);
            Debug.Log($"build: deleted {placement.Category} at ({placement.Cell.x},{placement.Cell.y}) y={placement.SurfaceY:F1}");
        }
    }

    [ServerRpc]
    private void CmdDeleteBelt(NetworkObject nob)
    {
        if (NetworkObject != null && !IsServerInitialized) return;
        if (nob == null || !nob.IsSpawned) return;
        ServerManager.Despawn(nob);
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
        if (_zoopStartSet || _beltState == BeltPlacementState.Dragging) return;

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
        _peerSnapMode = false;
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
        if (_beltStartSupportGhost != null) { Destroy(_beltStartSupportGhost); _beltStartSupportGhost = null; }
        if (_beltEndSupportGhost != null) { Destroy(_beltEndSupportGhost); _beltEndSupportGhost = null; }
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
        HideSnapHighlight();
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
                _zoopStartPos = currentPos;
                _zoopStartRot = currentRot;
                _zoopStartSurfaceY = EffectiveY;
                Debug.Log($"build: zoop start ({currentCell.x},{currentCell.y}) pos={currentPos}");
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

    /// <summary>
    /// Snap-based zoop: chain ghosts by snapping each to the previous one's
    /// snap point along the zoop direction. No cell math or footprint calculations.
    /// </summary>
    private void UpdateZoopPreview(Vector2Int endCell, Quaternion rot)
    {
        var prefab = GetSelectedPrefab();
        if (prefab == null) return;

        // Determine zoop direction from mouse movement (plane intersection delta)
        Vector3 delta = new Vector3(endCell.x - _zoopStartCell.x, 0f, endCell.y - _zoopStartCell.y);
        if (delta.sqrMagnitude < 0.01f)
        {
            // No movement -- show just the anchor ghost
            while (_zoopGhosts.Count < 1)
                _zoopGhosts.Add(CreateGhostFromPrefab(prefab));
            _zoopGhosts[0].transform.position = _zoopStartPos;
            _zoopGhosts[0].transform.rotation = _zoopStartRot;
            _zoopGhosts[0].SetActive(true);
            ApplyGhostColor(_zoopGhosts[0], ValidColor);
            for (int j = 1; j < _zoopGhosts.Count; j++)
                _zoopGhosts[j].SetActive(false);
            if (_ghost != null) _ghost.SetActive(false);
            return;
        }

        // Pick zoop direction.
        // Walls: force along length axis (perpendicular to facing) so they
        // chain side-by-side, never face-to-face.
        Vector3 zoopDir;
        if (_currentTool == BuildTool.Wall)
        {
            int wallRot = Mathf.RoundToInt(_zoopStartRot.eulerAngles.y) % 360;
            bool facesZ = wallRot == 0 || wallRot == 180;
            // Length axis is perpendicular to facing
            if (facesZ)
                zoopDir = new Vector3(Mathf.Sign(delta.x) != 0 ? Mathf.Sign(delta.x) : 1f, 0f, 0f);
            else
                zoopDir = new Vector3(0f, 0f, Mathf.Sign(delta.z) != 0 ? Mathf.Sign(delta.z) : 1f);
        }
        else if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
        {
            zoopDir = new Vector3(Mathf.Sign(delta.x), 0f, 0f);
        }
        else
        {
            zoopDir = new Vector3(0f, 0f, Mathf.Sign(delta.z));
        }

        // How many ghosts: distance along dominant axis / snap step size.
        // We discover the step size from the first snap chain link.
        int rotDeg = Mathf.RoundToInt(_zoopStartRot.eulerAngles.y);

        // Build chain: each ghost snaps to the previous ghost's snap point
        var positions = new List<Vector3> { _zoopStartPos };
        var rotations = new List<Quaternion> { _zoopStartRot };

        for (int i = 0; i < MaxZoopCount - 1; i++)
        {
            var prevPos = positions[positions.Count - 1];
            var prevRot = rotations[rotations.Count - 1];

            // Find the snap point on the prefab that faces the zoop direction
            var snapPoint = FindZoopSnapPoint(prefab, zoopDir, prevRot);
            if (snapPoint == null) break;

            // Get the world position of that snap point on the previous ghost
            Vector3 snapWorldPos = prevPos + prevRot * snapPoint.transform.localPosition;

            // Place next ghost by snapping to that point
            var (nextPos, nextRot) = GridManager.GetSnapPlacementPosition(
                snapPoint, prefab, rotDeg, 0f);
            // GetSnapPlacementPosition uses the snap's actual world transform,
            // but we need it relative to the previous ghost. Compute manually:
            // Find the ghost's matching snap (opposite normal)
            var ghostSnap = FindZoopSnapPoint(prefab, -zoopDir, prevRot);
            if (ghostSnap == null) break;

            Vector3 ghostSnapLocal = ghostSnap.transform.localPosition;
            nextPos = snapWorldPos - prevRot * ghostSnapLocal;

            // Check if we've passed the target along the zoop axis
            float distFromStart = Vector3.Dot(nextPos - _zoopStartPos, zoopDir);
            float targetDist = Vector3.Dot(delta, zoopDir);
            if (targetDist < 0f) targetDist = -targetDist;
            if (distFromStart > targetDist + 0.1f) break;

            positions.Add(nextPos);
            rotations.Add(prevRot);
        }

        // Ensure enough ghosts
        while (_zoopGhosts.Count < positions.Count)
            _zoopGhosts.Add(CreateGhostFromPrefab(prefab));

        for (int i = 0; i < _zoopGhosts.Count; i++)
        {
            if (i < positions.Count)
            {
                _zoopGhosts[i].transform.position = positions[i];
                _zoopGhosts[i].transform.rotation = rotations[i];
                _zoopGhosts[i].SetActive(true);
                ApplyGhostColor(_zoopGhosts[i], ValidColor);
            }
            else
            {
                _zoopGhosts[i].SetActive(false);
            }
        }

        if (_ghost != null) _ghost.SetActive(false);
    }

    private const int MaxZoopCount = 5;

    /// <summary>
    /// Find the snap point on a prefab whose normal (in world space given rotation)
    /// most closely matches the desired world direction.
    /// </summary>
    private static BuildingSnapPoint FindZoopSnapPoint(GameObject prefab, Vector3 worldDir, Quaternion rot)
    {
        var snaps = prefab.GetComponentsInChildren<BuildingSnapPoint>();
        BuildingSnapPoint best = null;
        float bestScore = -1f;

        // We want the snap whose local normal, rotated into world, aligns with worldDir
        Vector3 desiredLocal = Quaternion.Inverse(rot) * worldDir;

        foreach (var s in snaps)
        {
            float dot = Vector3.Dot(s.Normal, desiredLocal);
            if (dot < 0.5f) continue;

            // Prefer HighEdge/LowEdge over cardinal snaps for ramp chaining.
            // These encode slope geometry that cardinal _Bot snaps don't.
            float edgeBonus = s.gameObject.name.Contains("Edge") ? 0.1f : 0f;
            float score = dot + edgeBonus;

            if (score > bestScore)
            {
                bestScore = score;
                best = s;
            }
        }

        return best;
    }

    /// <summary>
    /// Place the zoop chain using the same snap-to-snap logic as the preview.
    /// Ghosts already hold the correct positions -- just read from them.
    /// </summary>
    private void PlaceZoopLine(Vector2Int start, Vector2Int end, Quaternion rot)
    {
        var prefab = GetSelectedPrefab();
        if (prefab == null) return;

        var category = ToolToCategory(_currentTool);

        int placed = 0;
        for (int i = 0; i < _zoopGhosts.Count; i++)
        {
            if (!_zoopGhosts[i].activeSelf) continue;

            var pos = _zoopGhosts[i].transform.position;
            var ghostRot = _zoopGhosts[i].transform.rotation;
            int rotDeg = Mathf.RoundToInt(ghostRot.eulerAngles.y);
            var cell = GridManager.Instance.Grid.WorldToCell(pos);
            float surfaceY = pos.y - GridManager.GetPrefabBaseOffset(prefab);

            if (category == BuildingCategory.Wall || category == BuildingCategory.Ramp)
            {
                var dir = RotationToDirection(rotDeg);
                GridManager.Instance.CmdPlaceDirectional(cell, surfaceY, dir, CurrentVariant, category, pos, ghostRot);
            }
            else
            {
                GridManager.Instance.CmdPlace(cell, surfaceY, rotDeg, CurrentVariant, category, pos);
            }
            placed++;
        }

        Debug.Log($"build: zoop placed {placed} {category}");
    }

    // -- Raycast helpers --

    /// <summary>
    /// Unified raycast for all build tools. Sets _placementMode and _activeSnapPoint
    /// based on what the ray hits (snap point on existing building vs terrain grid).
    /// Uses RaycastAll to find all snap sphere hits, then filters by center/edge mode.
    /// </summary>
    private bool RaycastPlacement(out RaycastHit hit)
    {
        var ray = new Ray(_camera.transform.position, _camera.transform.forward);

        // Allow snap detection for zoop's first click (before start is locked),
        // then skip it once zooping so preview stays grid-based.
        if (!_zoopMode || !_zoopStartSet)
        {
            // RaycastAll to catch all overlapping snap spheres along the ray
            var hits = Physics.RaycastAll(ray, _placementRange, StructuralMask);
            if (hits.Length > 0)
            {
                // Sort by distance so we process closest first
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                // Find the best snap point that matches our center/edge filter
                BuildingSnapPoint bestSnap = null;
                PlacementInfo bestPlacement = null;
                float bestDist = float.MaxValue;
                RaycastHit bestHit = default;

                foreach (var h in hits)
                {
                    var snap = h.collider.GetComponent<BuildingSnapPoint>();
                    if (snap == null) continue;
                    if (!MatchesSnapFilter(snap)) continue;

                    float dist = h.distance;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestSnap = snap;
                        bestPlacement = snap.GetComponentInParent<PlacementInfo>();
                        bestHit = h;
                    }
                }

                if (bestSnap != null && bestPlacement != null)
                {
                    hit = bestHit;
                    _placementMode = PlacementMode.Snap;
                    _activeSnapPoint = bestSnap;
                    _surfaceY = ComputeSnapSurfaceY(bestSnap, bestPlacement);
                    UpdateSnapHighlight();
                    return true;
                }

                // No snap matched filter -- check if we hit a building mesh
                // and find nearest filtered snap on that building
                foreach (var h in hits)
                {
                    var placement = h.collider.GetComponentInParent<PlacementInfo>();
                    if (placement == null) continue;

                    var chosen = FindNearestFiltered(placement.gameObject, h.point, h.normal);
                    if (chosen != null)
                    {
                        hit = h;
                        _placementMode = PlacementMode.Snap;
                        _activeSnapPoint = chosen;
                        _surfaceY = ComputeSnapSurfaceY(chosen, placement);
                        UpdateSnapHighlight();
                        return true;
                    }
                }

                // Fall through to terrain/grid from first hit
                hit = hits[0];
                if (hit.collider.gameObject.layer == PhysicsLayers.Terrain)
                {
                    _placementMode = PlacementMode.Grid;
                    _activeSnapPoint = null;
                    HideSnapHighlight();
                    _surfaceY = hit.point.y;
                    return true;
                }
            }
        }

        // Single raycast fallback (zoop mode or no RaycastAll hits)
        if (!Physics.Raycast(ray, out hit, _placementRange, StructuralMask))
        {
            _placementMode = PlacementMode.None;
            _activeSnapPoint = null;
            HideSnapHighlight();
            return false;
        }

        // Terrain hit -- grid mode
        _placementMode = PlacementMode.Grid;
        _activeSnapPoint = null;
        HideSnapHighlight();
        _surfaceY = hit.point.y;
        return true;
    }

    /// <summary>
    /// Returns true if the snap point matches the current snap filter.
    /// Structural tools: scroll toggles center (Mid/Center) vs edge (Top/Bot cardinal).
    /// Machine/Storage tools: scroll toggles foundation mode (_Top snaps on structural)
    ///                        vs peer mode (any snap on other machines/storage).
    /// </summary>
    private bool MatchesSnapFilter(BuildingSnapPoint snap)
    {
        var name = snap.gameObject.name;

        if (_currentTool == BuildTool.Machine || _currentTool == BuildTool.Storage)
        {
            var targetInfo = snap.GetComponentInParent<PlacementInfo>();

            if (_peerSnapMode)
            {
                // Peer mode: only snap to other machines/storage
                return targetInfo != null
                    && (targetInfo.Category == BuildingCategory.Machine
                        || targetInfo.Category == BuildingCategory.Storage);
            }

            // Foundation mode: only _Top snaps on foundations
            return targetInfo != null
                && targetInfo.Category == BuildingCategory.Foundation
                && name.Contains("Top");
        }

        if (!_edgeSnapMode)
        {
            return name.Contains("Mid") || name.Contains("Center");
        }
        else
        {
            return !name.Contains("Mid") && !name.Contains("Center");
        }
    }

    /// <summary>
    /// Find the nearest snap point on a building that matches the current center/edge filter.
    /// Optionally filters by hit normal to prefer same-face snaps.
    /// </summary>
    private BuildingSnapPoint FindNearestFiltered(GameObject building, Vector3 worldPoint, Vector3 hitNormal)
    {
        var points = building.GetComponentsInChildren<BuildingSnapPoint>();
        BuildingSnapPoint nearest = null;
        float bestDist = float.MaxValue;

        // First pass: same face + filter match
        foreach (var p in points)
        {
            if (!MatchesSnapFilter(p)) continue;
            if (Vector3.Dot(p.Normal, hitNormal) < 0.5f) continue;

            float dist = Vector3.Distance(p.transform.position, worldPoint);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = p;
            }
        }

        // Fallback: any filtered snap on the building
        if (nearest == null)
        {
            foreach (var p in points)
            {
                if (!MatchesSnapFilter(p)) continue;
                float dist = Vector3.Distance(p.transform.position, worldPoint);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = p;
                }
            }
        }

        return nearest;
    }

    private void HideSnapHighlight()
    {
        if (_snapHighlight != null)
            _snapHighlight.SetActive(false);
    }

    private void UpdateSnapHighlight()
    {
        if (_activeSnapPoint == null)
        {
            if (_snapHighlight != null) _snapHighlight.SetActive(false);
            return;
        }

        if (_snapHighlight == null)
        {
            _snapHighlight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _snapHighlight.name = "SnapHighlight";
            var col = _snapHighlight.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            _snapHighlight.layer = PhysicsLayers.Decal;
            _snapHighlight.transform.localScale = Vector3.one * 0.3f;
            var r = _snapHighlight.GetComponent<Renderer>();
            var mat = new Material(r.sharedMaterial);
            mat.color = new Color(0f, 1f, 1f, 0.7f);
            r.sharedMaterial = mat;
        }

        _snapHighlight.transform.position = _activeSnapPoint.transform.position;
        _snapHighlight.SetActive(true);
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
        var cam = Camera.main;
        if (cam == null) return;

        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        switch (_beltState)
        {
            case BeltPlacementState.Idle:
            case BeltPlacementState.PickingStart:
                HandleBeltPickStart(mouse, ray);
                break;
            case BeltPlacementState.Dragging:
                HandleBeltDragging(mouse, ray);
                break;
        }
    }

    private void HandleBeltPickStart(Mouse mouse, Ray ray)
    {
        // Preview ghost support at cursor before clicking
        if (TryResolveBeltEndpoint(ray, true, out var previewPos, out var previewDir, out var previewFromPort))
        {
            if (!previewFromPort)
            {
                previewPos.x = Mathf.Round(previewPos.x);
                previewPos.z = Mathf.Round(previewPos.z);

                _beltStartSupportGhost = EnsureSupportGhost(_beltStartSupportGhost);
                if (_beltStartSupportGhost != null)
                {
                    _beltStartSupportGhost.transform.position = previewPos;
                    _beltStartSupportGhost.transform.rotation = Quaternion.LookRotation(previewDir);
                    _beltStartSupportGhost.SetActive(true);
                    ApplyGhostColor(_beltStartSupportGhost, ValidColor);
                }

                // Show direction line from ghost support
                if (_beltPreviewLine == null)
                {
                    _beltPreviewLine = new GameObject("BeltPreview");
                    _beltLineRenderer = _beltPreviewLine.AddComponent<LineRenderer>();
                    _beltLineRenderer.startWidth = 0.15f;
                    _beltLineRenderer.endWidth = 0.15f;
                    _beltLineRenderer.useWorldSpace = true;
                    var shader = Shader.Find("Sprites/Default");
                    var mat = new Material(shader);
                    mat.color = Color.white;
                    _beltLineRenderer.material = mat;
                }
                _beltLineRenderer.positionCount = 2;
                var anchorPos = new Vector3(previewPos.x,
                    previewPos.y + GridManager.Instance.SupportAnchorHeight,
                    previewPos.z);
                _beltLineRenderer.SetPosition(0, anchorPos);
                _beltLineRenderer.SetPosition(1, anchorPos + previewDir * 1f);
                _beltLineRenderer.startColor = ValidColor;
                _beltLineRenderer.endColor = ValidColor;
                _beltPreviewLine.SetActive(true);
            }
            else
            {
                // Over a port -- hide support ghost and direction line
                if (_beltStartSupportGhost != null)
                    _beltStartSupportGhost.SetActive(false);
                if (_beltPreviewLine != null)
                    _beltPreviewLine.SetActive(false);
            }
        }
        else
        {
            if (_beltStartSupportGhost != null)
                _beltStartSupportGhost.SetActive(false);
            if (_beltPreviewLine != null)
                _beltPreviewLine.SetActive(false);
        }

        if (!mouse.leftButton.wasPressedThisFrame) return;

        if (TryResolveBeltEndpoint(ray, true, out var pos, out var dir, out var fromPort))
        {
            if (!fromPort)
            {
                pos.x = Mathf.Round(pos.x);
                pos.z = Mathf.Round(pos.z);
            }
            _beltStartGroundPos = pos;
            _beltStartPos = fromPort ? pos : new Vector3(pos.x, pos.y + GridManager.Instance.SupportAnchorHeight, pos.z);
            _beltStartDir = dir;
            _beltStartFromPort = fromPort;
            _beltState = BeltPlacementState.Dragging;

            // Show ghost support at start if placing on ground
            if (!fromPort)
            {
                _beltStartSupportGhost = EnsureSupportGhost(_beltStartSupportGhost);
                if (_beltStartSupportGhost != null)
                {
                    _beltStartSupportGhost.transform.position = pos;
                    _beltStartSupportGhost.transform.rotation = Quaternion.LookRotation(dir);
                    _beltStartSupportGhost.SetActive(true);
                    ApplyGhostColor(_beltStartSupportGhost, ValidColor);
                }
            }

            if (_beltPreviewLine == null)
            {
                _beltPreviewLine = new GameObject("BeltPreview");
                _beltLineRenderer = _beltPreviewLine.AddComponent<LineRenderer>();
                _beltLineRenderer.startWidth = 0.15f;
                _beltLineRenderer.endWidth = 0.15f;
                _beltLineRenderer.useWorldSpace = true;
                var shader = Shader.Find("Sprites/Default");
                var mat = new Material(shader);
                mat.color = Color.white;
                _beltLineRenderer.material = mat;
            }
            _beltLineRenderer.positionCount = 30;
            _beltPreviewLine.SetActive(true);
        }
    }

    private void HandleBeltDragging(Mouse mouse, Ray ray)
    {
        if (TryResolveBeltEndpoint(ray, false, out var endPos, out var endDir, out var endFromPort))
        {
            var startDir = _beltStartDir;  // R-key direction, never overridden
            bool isValid;

            // Grid snap free endpoints
            if (!endFromPort)
            {
                endPos.x = Mathf.Round(endPos.x);
                endPos.z = Mathf.Round(endPos.z);
            }

            var endGroundPos = endPos;
            if (!endFromPort)
                endPos.y += GridManager.Instance.SupportAnchorHeight;

            // Derive end direction from spatial relationship (all modes)
            if (!endFromPort)
                endDir = BeltRouteBuilder.DeriveEndDirection(_beltStartPos, startDir, endPos);

            // Zero endDir = straight backward placement, always invalid
            if (!endFromPort && endDir.sqrMagnitude < 0.001f)
            {
                isValid = false;
            }
            else if (_beltRoutingMode == BeltRoutingMode.Default)
            {
                // Default mode: build the actual route and validate the real geometry.
                // No duplicate math -- the route IS the source of truth.
                var validation = BeltPlacementValidator.Validate(
                    _beltStartPos, startDir, endPos, endDir);
                isValid = validation.IsValid;

                if (isValid)
                {
                    var testWaypoints = BeltRouteBuilder.Build(
                        _beltStartPos, startDir, endPos, endDir, BeltRoutingMode.Default);
                    isValid = BeltRouteBuilder.ValidateRoute(testWaypoints,
                        BeltRouteBuilder.MaxRampAngle, BeltPlacementValidator.MaxLength);
                }
            }
            else
            {
                // Straight/Curved: endpoint validation, then turn geometry checks
                var validation = BeltPlacementValidator.Validate(
                    _beltStartPos, startDir, endPos, endDir);
                isValid = validation.IsValid;

                // Additional validation for turn geometry and elevation
                if (isValid && !endFromPort)
                {
                    var axis = BeltRouteBuilder.SnapToCardinal(startDir);
                    var delta = new Vector3(endPos.x - _beltStartPos.x, 0, endPos.z - _beltStartPos.z);
                    float signedAlong = Vector3.Dot(delta, axis);
                    float alongDist = Mathf.Abs(signedAlong);
                    var cross = delta - signedAlong * axis;
                    float crossDistVal = cross.magnitude;

                    // Detect U-turn: endDir opposes startDir
                    bool isUturn = Vector3.Dot(BeltRouteBuilder.SnapToCardinal(endDir), axis) < -0.5f;

                    if (isUturn)
                    {
                        float minCross = BeltRouteBuilder.MinSegLength * 2;
                        float endpointDist = Vector3.Distance(_beltStartPos, endPos);
                        isValid = crossDistVal >= minCross
                               && endpointDist <= BeltPlacementValidator.MaxLength;
                    }
                    else if (crossDistVal < 0.1f)
                    {
                        isValid = alongDist >= BeltRouteBuilder.MinSegLength * 2;
                    }
                    else
                    {
                        if (_beltRoutingMode == BeltRoutingMode.Curved)
                        {
                            isValid = alongDist >= BeltRouteBuilder.MinSegLength * 2
                                   && crossDistVal >= BeltRouteBuilder.MinSegLength * 2;
                        }
                        else
                        {
                            float minLeg = Mathf.Min(BeltRouteBuilder.TurnRadius,
                                               alongDist - BeltRouteBuilder.MinSegLength)
                                         + BeltRouteBuilder.MinSegLength;
                            isValid = alongDist >= minLeg && crossDistVal >= minLeg;
                        }
                    }

                    if (isValid)
                    {
                        float heightDiff = Mathf.Abs(endPos.y - _beltStartPos.y);
                        if (heightDiff > 0.01f)
                        {
                            float idealRamp = 1.5f * heightDiff / Mathf.Tan(BeltRouteBuilder.MaxRampAngle * Mathf.Deg2Rad);
                            float actualRadius = crossDistVal >= 0.1f
                                ? Mathf.Min(BeltRouteBuilder.TurnRadius,
                                    alongDist - BeltRouteBuilder.MinSegLength,
                                    crossDistVal - BeltRouteBuilder.MinSegLength)
                                : 0f;
                            if (_beltRoutingMode == BeltRoutingMode.Curved)
                                actualRadius = 0f;
                            float availableForRamp = alongDist - actualRadius;
                            float minAlongForElevation = idealRamp;
                            if (crossDistVal >= 0.1f && _beltRoutingMode != BeltRoutingMode.Curved)
                                minAlongForElevation += BeltRouteBuilder.MinPostRampLength;
                            if (availableForRamp < minAlongForElevation)
                                isValid = false;
                        }
                    }
                }
            }

            // Ghost support at end position
            if (!endFromPort)
            {
                _beltEndSupportGhost = EnsureSupportGhost(_beltEndSupportGhost);
                if (_beltEndSupportGhost != null)
                {
                    _beltEndSupportGhost.transform.position = endGroundPos;
                    _beltEndSupportGhost.transform.rotation = Quaternion.LookRotation(endDir);
                    _beltEndSupportGhost.SetActive(true);
                }
            }
            else if (_beltEndSupportGhost != null)
            {
                _beltEndSupportGhost.SetActive(false);
            }

            // Tint ghosts and line renderer based on validity
            var color = isValid ? Color.green : Color.red;
            _beltLineRenderer.startColor = color;
            _beltLineRenderer.endColor = color;
            if (_beltStartSupportGhost != null && _beltStartSupportGhost.activeSelf)
                ApplyGhostColor(_beltStartSupportGhost, color);
            if (_beltEndSupportGhost != null && _beltEndSupportGhost.activeSelf)
                ApplyGhostColor(_beltEndSupportGhost, color);

            // Preview line (at anchor height) -- all modes use waypoints
            {
                var waypoints = BeltRouteBuilder.Build(_beltStartPos, startDir, endPos, endDir, _beltRoutingMode);
                float routeLen = BeltRouteBuilder.ComputeRouteLength(waypoints);
                for (int i = 0; i < 30; i++)
                {
                    float t = (float)i / 29;
                    _beltLineRenderer.SetPosition(i,
                        BeltRouteBuilder.EvaluateRoute(waypoints, routeLen, t));
                }
            }

            if (mouse.leftButton.wasPressedThisFrame && isValid)
            {
                Debug.Log($"belt: placing {_beltRoutingMode} from {_beltStartGroundPos} to {endGroundPos}");
                GridManager.Instance.CmdPlaceBelt(
                    _beltStartFromPort ? _beltStartPos : _beltStartGroundPos,
                    startDir,
                    endFromPort ? endPos : endGroundPos,
                    endDir,
                    routingMode: (byte)_beltRoutingMode,
                    startFromPort: _beltStartFromPort,
                    endFromPort: endFromPort);

                _beltState = BeltPlacementState.Idle;
                _beltPreviewLine.SetActive(false);
                HideSupportGhosts();
            }

            if (!isValid && mouse.leftButton.wasPressedThisFrame)
                Debug.Log($"belt: placement rejected -- not enough room for turn");
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            _beltState = BeltPlacementState.Idle;
            if (_beltPreviewLine != null)
                _beltPreviewLine.SetActive(false);
            HideSupportGhosts();
        }
    }

    /// <summary>Max raycast range for belt endpoint detection.</summary>
    private const float BeltRaycastRange = 50f;

    /// <summary>
    /// Layers valid for support placement (terrain and foundations only).
    /// Belts, walls, ramps, machines are NOT valid support surfaces.
    /// </summary>
    private static bool IsValidSupportSurface(RaycastHit hit)
    {
        int layer = hit.collider.gameObject.layer;

        // Terrain and grid plane are always valid
        if (layer == PhysicsLayers.Terrain || layer == PhysicsLayers.GridPlane || layer == PhysicsLayers.BIM_Static)
            return true;

        // Structures layer: only foundations are valid
        if (layer == PhysicsLayers.Structures)
        {
            var info = hit.collider.GetComponentInParent<PlacementInfo>();
            return info != null && (info.Category == BuildingCategory.Foundation || info.Category == BuildingCategory.Ramp);
        }

        return false;
    }

    /// <summary>
    /// Resolve belt endpoint from raycast. isStart=true means we're picking the
    /// start of the belt (needs Output port), isStart=false means end (needs Input port).
    /// Direction always points in the belt's flow direction at that endpoint.
    /// </summary>
    private bool TryResolveBeltEndpoint(Ray ray, bool isStart, out Vector3 pos, out Vector3 dir, out bool fromPort)
    {
        pos = Vector3.zero;
        dir = Vector3.forward;
        fromPort = false;

        if (!Physics.Raycast(ray, out var hit, BeltRaycastRange,
            PhysicsLayers.StructuralPlacementMask |
            (1 << PhysicsLayers.BeltPorts)))
            return false;

        // Direct hit on a BeltPort
        var beltPort = hit.collider.GetComponentInParent<BeltPort>();
        if (beltPort != null)
        {
            pos = beltPort.WorldPosition;
            dir = GetPortFlowDirection(beltPort, isStart);
            fromPort = true;
            return true;
        }

        // Check for nearby BeltPort within snap radius (port colliders are small)
        var nearbyPort = FindNearbyPort(hit.point, isStart, 0.6f);
        if (nearbyPort != null)
        {
            pos = nearbyPort.WorldPosition;
            dir = GetPortFlowDirection(nearbyPort, isStart);
            fromPort = true;
            return true;
        }

        // Snap anchor on existing support
        var snapAnchor = hit.collider.GetComponentInParent<BeltSnapAnchor>();
        if (snapAnchor != null)
        {
            // If anchor already has a belt port, it's occupied -- reject
            if (HasExistingBeltPort(snapAnchor.WorldPosition, 0.6f))
                return false;

            pos = snapAnchor.WorldPosition;
            dir = snapAnchor.WorldDirection;
            fromPort = true;
            return true;
        }

        // Ground/structure fallback -- server will spawn support here.
        // Only allow on terrain and foundations, not on belts or other structures.
        if (!IsValidSupportSurface(hit))
            return false;

        pos = hit.point;
        if (isStart)
        {
            // Use R-key rotation for cardinal start direction
            dir = Quaternion.Euler(0, _placeRotation, 0) * Vector3.forward;
        }
        else
        {
            var toEnd = hit.point - _beltStartPos;
            toEnd.y = 0;
            dir = toEnd.normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        }
        return true;
    }

    /// <summary>
    /// Get the belt's spline tangent direction at this port.
    /// Belt ports: use transform.forward (set from spline tangent at creation).
    /// Machine/storage ports: use center-to-port vector (no rotation setup needed).
    /// Start: tangent points away from connection. End: tangent points toward connection.
    /// </summary>
    private static Vector3 GetPortFlowDirection(BeltPort port, bool isStart)
    {
        var parent = port.transform.parent;
        if (parent == null)
            return isStart ? port.WorldDirection : -port.WorldDirection;

        // Belt ports store the spline tangent direction in transform.forward.
        // Using this gives C1 continuity when chaining belts.
        if (parent.GetComponent<NetworkBeltSegment>() != null)
            return isStart ? port.WorldDirection : -port.WorldDirection;

        // Machine/storage: derive direction from physical position relative to center.
        var awayFromMachine = port.WorldPosition - parent.position;
        awayFromMachine.y = 0;
        if (awayFromMachine.sqrMagnitude < 0.001f)
            awayFromMachine = port.transform.forward;
        else
            awayFromMachine = awayFromMachine.normalized;

        return isStart ? awayFromMachine : -awayFromMachine;
    }

    private static BeltPort FindNearbyPort(Vector3 position, bool isStart, float radius)
    {
        var colliders = Physics.OverlapSphere(position, radius, 1 << PhysicsLayers.BeltPorts);

        // Prefer direction-compatible port: start needs Output, end needs Input
        var wantDir = isStart ? BeltPortDirection.Output : BeltPortDirection.Input;

        BeltPort compatible = null;
        float compatibleDist = float.MaxValue;
        BeltPort any = null;
        float anyDist = float.MaxValue;

        foreach (var col in colliders)
        {
            var port = col.GetComponentInParent<BeltPort>();
            if (port == null) continue;
            float dist = Vector3.Distance(position, port.WorldPosition);

            if (port.Direction == wantDir && dist < compatibleDist)
            {
                compatibleDist = dist;
                compatible = port;
            }
            if (dist < anyDist)
            {
                anyDist = dist;
                any = port;
            }
        }

        // Return compatible port if found, otherwise any port (caller handles direction)
        return compatible ?? any;
    }

    /// <summary>
    /// Check if any belt port exists near a position (regardless of direction).
    /// Used to detect occupied snap anchors.
    /// </summary>
    private static bool HasExistingBeltPort(Vector3 position, float radius)
    {
        var colliders = Physics.OverlapSphere(position, radius, 1 << PhysicsLayers.BeltPorts);
        foreach (var col in colliders)
        {
            if (col.GetComponentInParent<BeltPort>() != null)
                return true;
        }
        return false;
    }

    private void CancelBeltPlacement()
    {
        _beltState = BeltPlacementState.Idle;
        if (_beltPreviewLine != null)
            _beltPreviewLine.SetActive(false);
        if (_beltStartSupportGhost != null) _beltStartSupportGhost.SetActive(false);
        if (_beltEndSupportGhost != null) _beltEndSupportGhost.SetActive(false);
        DestroyGhost();
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

    private GameObject EnsureSupportGhost(GameObject existing)
    {
        if (existing != null) return existing;
        var supportPrefab = GridManager.Instance.GetPrefab(BuildingCategory.Support, 0);
        if (supportPrefab == null) return null;
        return CreateGhostFromPrefab(supportPrefab);
    }

    private void HideSupportGhosts()
    {
        if (_beltStartSupportGhost != null) _beltStartSupportGhost.SetActive(false);
        if (_beltEndSupportGhost != null) _beltEndSupportGhost.SetActive(false);
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
        if (_currentTool == BuildTool.Belt) lineCount++;
        if (_zoopStartSet) lineCount++;
        if (_beltState == BeltPlacementState.Dragging) lineCount++;

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

        string filterLabel;
        if (_currentTool == BuildTool.Machine || _currentTool == BuildTool.Storage)
            filterLabel = _peerSnapMode ? "MACHINE/STORAGE" : "FOUNDATION";
        else
            filterLabel = _edgeSnapMode ? "EDGE" : "CENTER";

        if (_placementMode == PlacementMode.Snap && _activeSnapPoint != null)
            GUILayout.Label($"Snap: {_activeSnapPoint.name}  |  Filter: {filterLabel} (scroll to swap)");
        else
            GUILayout.Label($"Mode: Grid  |  Filter: {filterLabel} (scroll to swap)");

        GUILayout.Label($"1:Foundation 2:Wall 3:Ramp 4:Machine 5:Storage 6:Belt  |  Mode: {zoopLabel}");
        GUILayout.Label($"Rotation: {_placeRotation}  |  [R] Rotate  [X] Delete  [Z] Zoop  [G] Grid  [Tab] Variant  [PgUp/Dn] Nudge (+Shift: 0.5m)");
        GUILayout.Label("[B] Exit  |  [Esc] Cancel  |  LMB: Place  |  RMB: Remove");

        if (_zoopStartSet)
        {
            GUILayout.Label($"{_currentTool} zoop: start ({_zoopStartCell.x},{_zoopStartCell.y}) -- click end");
        }

        if (_currentTool == BuildTool.Belt)
            GUILayout.Label($"Belt mode: {_beltRoutingMode}  |  [Tab] Cycle Default/Straight/Curved");

        if (_beltState == BeltPlacementState.Dragging)
            GUILayout.Label($"Belt start: {_beltStartPos} -- click end point");

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
        if (_snapHighlight != null)
            Destroy(_snapHighlight);
    }
}
