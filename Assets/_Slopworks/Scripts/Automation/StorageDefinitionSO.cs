using UnityEngine;

[CreateAssetMenu(menuName = "Buildings/Storage")]
public class StorageDefinitionSO : ScriptableObject
{
    public string storageId;
    public string displayName;
    public int slotCount = 20;
    public int maxStackSize = 50;
    public Vector2Int size = Vector2Int.one;
}
