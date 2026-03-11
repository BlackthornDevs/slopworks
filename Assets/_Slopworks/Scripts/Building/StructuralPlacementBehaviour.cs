using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around StructuralPlacementService (D-004).
/// Creates and owns the snap point registry and structural placement service.
/// Spawns placeholder cube visuals for foundations, walls, and ramps.
/// </summary>
public class StructuralPlacementBehaviour : MonoBehaviour
{
    [SerializeField] private FactoryGridBehaviour _factoryGrid;

    private SnapPointRegistry _snapRegistry;
    private StructuralPlacementService _placementService;

    public SnapPointRegistry SnapRegistry => _snapRegistry;
    public StructuralPlacementService PlacementService => _placementService;
    public FactoryGrid Grid => _factoryGrid.Grid;

    private readonly List<BuildingData> _foundations = new();
    private readonly List<WallData> _walls = new();
    private readonly List<RampData> _ramps = new();

    public IReadOnlyList<BuildingData> Foundations => _foundations;
    public IReadOnlyList<WallData> Walls => _walls;
    public IReadOnlyList<RampData> Ramps => _ramps;

    private void Awake()
    {
        _snapRegistry = new SnapPointRegistry();
        _placementService = new StructuralPlacementService(_factoryGrid.Grid, _snapRegistry);
    }

    /// <summary>
    /// Initialize with externally created grid and registry (for playtest setup).
    /// </summary>
    public void InitializeExternal(FactoryGrid grid, SnapPointRegistry snapRegistry,
        StructuralPlacementService placementService)
    {
        _snapRegistry = snapRegistry;
        _placementService = placementService;
    }

    public BuildingData PlaceFoundation(FoundationDefinitionSO def, Vector2Int cell, float surfaceY)
    {
        var data = _placementService.PlaceFoundation(def, cell, surfaceY);
        if (data == null)
            return null;

        _foundations.Add(data);
        SpawnFoundationVisual(data, cell, def.size, surfaceY);
        return data;
    }

    public WallData PlaceWall(WallDefinitionSO def, SnapPoint attachPoint)
    {
        var wallData = _placementService.PlaceWall(def, attachPoint);
        if (wallData == null)
            return null;

        _walls.Add(wallData);
        SpawnWallVisual(wallData);
        return wallData;
    }

    public RampData PlaceRamp(RampDefinitionSO def, SnapPoint attachPoint)
    {
        var rampData = _placementService.PlaceRamp(def, attachPoint);
        if (rampData == null)
            return null;

        _ramps.Add(rampData);
        SpawnRampVisual(rampData);
        return rampData;
    }

    public bool RemoveFoundation(BuildingData data)
    {
        if (!_placementService.RemoveFoundation(data))
            return false;

        _foundations.Remove(data);
        if (data.Instance != null)
            Destroy(data.Instance);
        return true;
    }

    public void RemoveWall(WallData wallData)
    {
        _placementService.RemoveWall(wallData);
        _walls.Remove(wallData);
        if (wallData.Instance != null)
            Destroy(wallData.Instance);
    }

    public void RemoveRamp(RampData rampData)
    {
        _placementService.RemoveRamp(rampData);
        _ramps.Remove(rampData);
        if (rampData.Instance != null)
            Destroy(rampData.Instance);
    }

    private void SpawnFoundationVisual(BuildingData data, Vector2Int origin, Vector2Int size, float surfaceY)
    {
        var grid = _factoryGrid != null ? _factoryGrid.Grid : Grid;
        var parent = new GameObject($"Foundation_{origin.x}_{origin.y}_Y{surfaceY:F1}");

        for (int x = origin.x; x < origin.x + size.x; x++)
        {
            for (int z = origin.y; z < origin.y + size.y; z++)
            {
                var worldPos = grid.CellToWorld(new Vector2Int(x, z), surfaceY);
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "FoundationTile";
                tile.transform.SetParent(parent.transform);
                tile.transform.position = worldPos + Vector3.up * 0.05f;
                tile.transform.localScale = new Vector3(0.95f, 0.1f, 0.95f);
                SetColor(tile, Color.white);
            }
        }

        data.Instance = parent;
    }

    private void SpawnWallVisual(WallData wallData)
    {
        var grid = _factoryGrid != null ? _factoryGrid.Grid : Grid;
        var cellCenter = grid.CellToWorld(wallData.Cell, wallData.SurfaceY);
        var edgeDir = wallData.EdgeDirection;
        var edgeOffset = new Vector3(edgeDir.x * 0.5f * FactoryGrid.CellSize, 0f, edgeDir.y * 0.5f * FactoryGrid.CellSize);
        var wallPos = cellCenter + edgeOffset + Vector3.up * FactoryGrid.WallHeight * 0.5f;

        float yRotation = Mathf.Atan2(edgeDir.x, edgeDir.y) * Mathf.Rad2Deg;

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = $"Wall_{wallData.Cell.x}_{wallData.Cell.y}_Y{wallData.SurfaceY:F1}";
        wall.transform.position = wallPos;
        wall.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        wall.transform.localScale = new Vector3(0.95f, FactoryGrid.WallHeight, 0.1f);
        SetColor(wall, new Color(0.6f, 0.6f, 0.6f));

        wallData.Instance = wall;
    }

    private void SpawnRampVisual(RampData rampData)
    {
        var grid = _factoryGrid != null ? _factoryGrid.Grid : Grid;
        var dir2D = rampData.Direction;

        // Edge-to-edge: start at foundation/ramp boundary, end at far edge of last ramp cell
        var snapCell = rampData.BaseCell - dir2D;
        var cellCenter = grid.CellToWorld(snapCell, rampData.BaseSurfaceY);
        var edgeOffset = new Vector3(dir2D.x * 0.5f * FactoryGrid.CellSize, 0f, dir2D.y * 0.5f * FactoryGrid.CellSize);
        var startPos = cellCenter + edgeOffset;
        startPos.y = rampData.BaseSurfaceY;

        var endPos = startPos
            + new Vector3(dir2D.x, 0f, dir2D.y) * rampData.FootprintLength * FactoryGrid.CellSize;
        endPos.y = rampData.BaseSurfaceY + FactoryGrid.WallHeight;

        var midpoint = (startPos + endPos) * 0.5f;
        var dir3D = (endPos - startPos).normalized;
        var length = Vector3.Distance(startPos, endPos);

        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = $"Ramp_{rampData.BaseCell.x}_{rampData.BaseCell.y}_Y{rampData.BaseSurfaceY:F1}";
        ramp.transform.position = midpoint;
        ramp.transform.rotation = Quaternion.LookRotation(dir3D);
        ramp.transform.localScale = new Vector3(0.95f, 0.1f, length);
        SetColor(ramp, new Color(0.76f, 0.6f, 0.42f));

        rampData.Instance = ramp;
    }

    private static void SetColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (Application.isPlaying)
                renderer.material.color = color;
            else
                renderer.sharedMaterial.color = color;
        }
    }
}
