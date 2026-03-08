using UnityEngine;

public class SettlementManagerBehaviour : MonoBehaviour
{
    public static SettlementManagerBehaviour Instance { get; private set; }

    [SerializeField] private string _factoryHubId = "factory_yard";
    [SerializeField] private float _tickInterval = 1f;

    private SettlementGraph _graph;
    private float _tickTimer;

    public SettlementGraph Graph => _graph;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _graph = new SettlementGraph(_factoryHubId);
    }

    public SettlementBuilding RegisterBuilding(
        SettlementBuildingDefinitionSO definition, Vector3 position)
    {
        if (!_graph.Register(definition, position))
        {
            Debug.LogWarning($"settlement: failed to register {definition.buildingId}");
            return null;
        }
        var building = _graph.Get(definition.buildingId);
        Debug.Log($"settlement: registered {definition.displayName} at {position}");
        return building;
    }

    public bool BuildRoad(string idA, string idB)
    {
        bool result = _graph.BuildRoad(idA, idB);
        if (result)
            Debug.Log($"settlement: road built between {idA} and {idB}");
        else
            Debug.LogWarning($"settlement: failed to build road {idA} <-> {idB}");
        return result;
    }

    private void FixedUpdate()
    {
        if (_graph == null) return;
        _tickTimer += Time.fixedDeltaTime;
        if (_tickTimer >= _tickInterval)
        {
            _graph.Tick(_tickTimer);
            _tickTimer = 0f;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
