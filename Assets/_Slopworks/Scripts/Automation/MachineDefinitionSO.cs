using UnityEngine;

/// <summary>
/// Read-only definition for a machine building type.
/// Never mutate at runtime -- SOs are shared across all instances.
/// </summary>
[CreateAssetMenu(menuName = "Slopworks/Buildings/Machine Definition")]
public class MachineDefinitionSO : ScriptableObject, IPlaceableDefinition
{
    /// <summary>
    /// Stable string identifier for this machine type.
    /// </summary>
    public string machineId;

    public string displayName;

    /// <summary>
    /// Grid footprint in cells (e.g. 2x2 for a smelter).
    /// </summary>
    public Vector2Int size = new Vector2Int(1, 1);

    /// <summary>
    /// Visual prefab instantiated when the machine is placed.
    /// </summary>
    public GameObject prefab;

    public Sprite icon;

    /// <summary>
    /// Matches RecipeSO.requiredMachineType for recipe filtering.
    /// </summary>
    public string machineType;

    /// <summary>
    /// Number of input item slots available on this machine.
    /// </summary>
    public int inputBufferSize = 1;

    /// <summary>
    /// Number of output item slots available on this machine.
    /// </summary>
    public int outputBufferSize = 1;

    /// <summary>
    /// Multiplier applied to recipe craft duration. Higher = faster.
    /// </summary>
    public float processingSpeed = 1f;

    /// <summary>
    /// Watts consumed while the machine is working.
    /// </summary>
    public float powerConsumption;

    /// <summary>
    /// I/O port definitions for belt connections.
    /// </summary>
    public MachinePort[] ports;

    public string PlaceableId => machineId;
    Vector2Int IPlaceableDefinition.Size => size;
}
