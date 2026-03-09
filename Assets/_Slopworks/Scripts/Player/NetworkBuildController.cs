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

        // Variant cycle: Tab key
        if (kb.tabKey.wasPressedThisFrame)
            CycleVariant();

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

            // Foundation/grid mode: only structural _Top snaps
            bool isStructural = targetInfo != null
                && targetInfo.Category != BuildingCategory.Machine
                && targetInfo.Category != BuildingCategory.Storage;
            return isStructural && name.Contains("Top");
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
        if (_snapHighlight != null)
            Destroy(_snapHighlight);
    }
}
