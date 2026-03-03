using UnityEngine;

/// <summary>
/// Read-only turret configuration. Never mutate at runtime.
/// </summary>
[CreateAssetMenu(menuName = "Slopworks/Combat/Turret Definition")]
public class TurretDefinitionSO : ScriptableObject, IPlaceableDefinition
{
    public string turretId;
    public string displayName;
    public float range = 20f;
    public float fireInterval = 0.5f;
    public float damagePerShot = 10f;
    public DamageType damageType = DamageType.Kinetic;
    public string ammoItemId;
    public float powerConsumption = 50f;
    public Vector2Int size = new Vector2Int(1, 1);
    public MachinePort[] ports;

    /// <summary>
    /// Power satisfaction threshold below which the turret refuses to fire (0-1).
    /// </summary>
    public float powerThreshold = 0.5f;

    /// <summary>
    /// Number of ammo slots in internal storage.
    /// </summary>
    public int ammoSlotCount = 1;

    /// <summary>
    /// Max stack size per ammo slot.
    /// </summary>
    public int ammoMaxStackSize = 64;

    public string PlaceableId => turretId;
    Vector2Int IPlaceableDefinition.Size => size;
}
