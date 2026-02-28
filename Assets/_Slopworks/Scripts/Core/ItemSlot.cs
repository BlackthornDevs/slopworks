using System;

[Serializable]
public struct ItemSlot
{
    public ItemInstance item;
    public int count;

    public bool IsEmpty => item.IsEmpty || count <= 0;

    public static readonly ItemSlot Empty = default;
}
