using UnityEngine;

[CreateAssetMenu(menuName = "Slopworks/Items/Item Definition")]
public class ItemDefinitionSO : ScriptableObject
{
    public string itemId;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public ItemCategory category;
    public bool isStackable = true;
    public int maxStackSize = 64;
    public bool hasDurability;
    public float maxDurability = 100f;
    public GameObject worldPrefab;
}
