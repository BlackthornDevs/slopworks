using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around BuildModeController (D-004).
/// Handles raycasting, ghost preview visuals, and input polling.
/// </summary>
public class BuildModeControllerBehaviour : MonoBehaviour
{
    [SerializeField] private FactoryGridBehaviour _factoryGrid;
    [SerializeField] private Transform _previewGhost;
    [SerializeField] private LayerMask _placementRaycastMask;

    // TODO: replace with SlopworksControls callbacks when InputActions asset exists
    [Header("Prototype Input (temporary)")]
    [SerializeField] private FoundationDefinitionSO _debugFoundation;

    private BuildModeController _controller;
    private Renderer[] _ghostRenderers;

    private static readonly Color _validColor = new Color(0f, 1f, 0f, 0.5f);
    private static readonly Color _invalidColor = new Color(1f, 0f, 0f, 0.5f);

    private void Awake()
    {
        _controller = new BuildModeController();

        if (_placementRaycastMask == 0)
            _placementRaycastMask = PhysicsLayers.PlacementMask;

        if (_previewGhost != null)
        {
            _ghostRenderers = _previewGhost.GetComponentsInChildren<Renderer>();
            _previewGhost.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        HandleInput();

        if (!_controller.IsInBuildMode)
            return;

        UpdatePreviewFromCursor();
        UpdateGhostVisuals();
    }

    // TODO: replace with SlopworksControls callbacks when InputActions asset exists
    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (_controller.IsInBuildMode)
            {
                ExitBuildMode();
            }
            else if (_debugFoundation != null)
            {
                EnterBuildMode(_debugFoundation);
            }
        }

        if (!_controller.IsInBuildMode)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitBuildMode();
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            _controller.RotatePreview();
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (_controller.TryPlace(_factoryGrid.Grid))
            {
                Debug.Log("Build mode: foundation placed");
            }
        }
    }

    /// <summary>
    /// Enter build mode with the given definition. Can be called from UI.
    /// Accepts any IPlaceableDefinition (foundation, machine, storage).
    /// </summary>
    public void EnterBuildMode(IPlaceableDefinition definition)
    {
        _controller.EnterBuildMode(definition);

        if (_previewGhost != null)
            _previewGhost.gameObject.SetActive(true);
    }

    /// <summary>
    /// Exit build mode. Can be called from UI.
    /// </summary>
    public void ExitBuildMode()
    {
        _controller.ExitBuildMode();

        if (_previewGhost != null)
            _previewGhost.gameObject.SetActive(false);
    }

    private void UpdatePreviewFromCursor()
    {
        var camera = Camera.main;
        if (camera == null)
            return;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, _placementRaycastMask))
        {
            _controller.UpdatePreview(hit.point, _factoryGrid.Grid);
        }
    }

    private void UpdateGhostVisuals()
    {
        if (_previewGhost == null)
            return;

        var worldPos = _factoryGrid.Grid.CellToWorld(_controller.SnappedCell);
        var effectiveSize = _controller.EffectiveSize;

        // Position the ghost at the center of the footprint
        float offsetX = (effectiveSize.x - 1) * FactoryGrid.CellSize * 0.5f;
        float offsetZ = (effectiveSize.y - 1) * FactoryGrid.CellSize * 0.5f;
        _previewGhost.position = worldPos + new Vector3(offsetX, 0f, offsetZ);
        _previewGhost.rotation = Quaternion.Euler(0f, _controller.Rotation, 0f);

        // Tint ghost green or red based on validity
        Color tint = _controller.IsValidPlacement ? _validColor : _invalidColor;

        if (_ghostRenderers != null)
        {
            foreach (var r in _ghostRenderers)
            {
                if (r.material.HasProperty("_Color"))
                    r.material.color = tint;
            }
        }
    }
}
