using UnityEngine;

/// <summary>
/// Read-only definition for a foundation building type.
/// Never mutate at runtime -- SOs are shared across all instances.
/// </summary>
[CreateAssetMenu(menuName = "Slopworks/Buildings/Foundation Definition")]
public class FoundationDefinitionSO : ScriptableObject, IPlaceableDefinition
{
    public string foundationId;
    public string displayName;
    public Vector2Int size = new Vector2Int(1, 1);
    public GameObject prefab;
    public Sprite icon;

    public string PlaceableId => foundationId;
    Vector2Int IPlaceableDefinition.Size => size;
}
