using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around BuildingPlacementService (D-004).
/// Creates and owns the port registry, connection resolver, and placement service.
/// Bridges between BuildModeControllerBehaviour and the simulation layer.
/// </summary>
public class BuildingPlacementBehaviour : MonoBehaviour
{
    [SerializeField] private FactoryGridBehaviour _factoryGrid;
    [SerializeField] private FactorySimulationBehaviour _factorySimulation;

    private PortNodeRegistry _portRegistry;
    private ConnectionResolver _connectionResolver;
    private BuildingPlacementService _placementService;

    public PortNodeRegistry PortRegistry => _portRegistry;
    public BuildingPlacementService PlacementService => _placementService;

    private void Awake()
    {
        _portRegistry = new PortNodeRegistry();
        _connectionResolver = new ConnectionResolver(_portRegistry, _factorySimulation.Simulation);
        _placementService = new BuildingPlacementService(
            _factoryGrid.Grid,
            _portRegistry,
            _connectionResolver,
            _factorySimulation.Simulation);
    }

    /// <summary>
    /// Place a machine at the given cell with rotation. Returns the placed GameObject or null.
    /// </summary>
    public GameObject PlaceMachine(MachineDefinitionSO def, Vector2Int cell, int rotation)
    {
        var result = _placementService.PlaceMachine(def, cell, rotation);
        if (result == null)
            return null;

        return SpawnVisual(def.prefab, cell, result.BuildingData);
    }

    /// <summary>
    /// Place a storage container at the given cell with rotation.
    /// </summary>
    public GameObject PlaceStorage(StorageDefinitionSO def, Vector2Int cell, int rotation)
    {
        var result = _placementService.PlaceStorage(def, cell, rotation);
        if (result == null)
            return null;

        return SpawnVisual(def.prefab, cell, result.BuildingData);
    }

    /// <summary>
    /// Place a belt from startCell to endCell.
    /// </summary>
    public GameObject PlaceBelt(Vector2Int startCell, Vector2Int endCell, GameObject beltPrefab)
    {
        var result = _placementService.PlaceBelt(startCell, endCell);
        if (result == null)
            return null;

        if (beltPrefab == null)
            return null;

        var startWorld = _factoryGrid.Grid.CellToWorld(startCell);
        var endWorld = _factoryGrid.Grid.CellToWorld(endCell);
        var midpoint = (startWorld + endWorld) * 0.5f;
        var direction = (endWorld - startWorld).normalized;
        var rotation = Quaternion.LookRotation(direction, Vector3.up);

        var instance = Instantiate(beltPrefab, midpoint, rotation);
        var length = Vector3.Distance(startWorld, endWorld);
        instance.transform.localScale = new Vector3(1f, 1f, length);

        result.BuildingData.Instance = instance;

        // Set up belt item visualization
        var beltBehaviour = instance.GetComponent<BeltSegmentBehaviour>();
        if (beltBehaviour != null)
        {
            beltBehaviour.Initialize(
                (BeltSegment)result.SimulationObject,
                startWorld,
                endWorld);
        }

        return instance;
    }

    /// <summary>
    /// Remove a building and destroy its visual.
    /// </summary>
    public void Remove(BuildingData data)
    {
        if (data == null)
            return;

        if (data.Instance != null)
            Destroy(data.Instance);

        _placementService.Remove(data);
    }

    private GameObject SpawnVisual(GameObject prefab, Vector2Int cell, BuildingData data)
    {
        if (prefab == null)
            return null;

        var worldPos = _factoryGrid.Grid.CellToWorld(cell);
        var effectiveSize = data.Size;

        // Position at footprint center
        float offsetX = (effectiveSize.x - 1) * FactoryGrid.CellSize * 0.5f;
        float offsetZ = (effectiveSize.y - 1) * FactoryGrid.CellSize * 0.5f;
        var centerPos = worldPos + new Vector3(offsetX, 0f, offsetZ);

        var instance = Instantiate(prefab, centerPos, Quaternion.Euler(0f, data.Rotation, 0f));
        data.Instance = instance;
        return instance;
    }
}
