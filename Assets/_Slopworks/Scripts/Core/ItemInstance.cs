using System;

[Serializable]
public struct ItemInstance
{
    public string definitionId;
    public int durability;

    public bool IsEmpty => string.IsNullOrEmpty(definitionId);

    public static ItemInstance Create(string definitionId)
    {
        return new ItemInstance
        {
            definitionId = definitionId,
            durability = -1
        };
    }

    public static readonly ItemInstance Empty = default;
}
