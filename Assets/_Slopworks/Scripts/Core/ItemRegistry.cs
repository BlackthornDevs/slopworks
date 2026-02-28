using System.Collections.Generic;
using UnityEngine;

public class ItemRegistry : MonoBehaviour
{
    [SerializeField] private ItemDefinitionSO[] _items;

    private readonly Dictionary<string, ItemDefinitionSO> _lookup = new();

    private void Awake()
    {
        foreach (var item in _items)
        {
            if (!_lookup.TryAdd(item.itemId, item))
                Debug.LogWarning($"duplicate item id: {item.itemId}");
        }
    }

    public ItemDefinitionSO Get(string itemId)
    {
        _lookup.TryGetValue(itemId, out var def);
        return def;
    }

    public bool Exists(string itemId) => _lookup.ContainsKey(itemId);
}
