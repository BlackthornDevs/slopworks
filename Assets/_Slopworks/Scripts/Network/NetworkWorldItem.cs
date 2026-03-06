using FishNet.Object;
using UnityEngine;

public class NetworkWorldItem : NetworkBehaviour
{
    [SerializeField] private string _itemId;
    [SerializeField] private int _count = 1;

    public string ItemId => _itemId;
    public int Count => _count;

    public void Setup(string itemId, int count)
    {
        _itemId = itemId;
        _count = count;
    }
}
