using UnityEngine;

[CreateAssetMenu(menuName = "Slopworks/Items/Item Definition")]
public class ItemDefinitionSO : ScriptableObject
{
    public string itemId;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public bool isStackable = true;
    public int maxStackSize = 64;
    public GameObject worldPrefab;
}
