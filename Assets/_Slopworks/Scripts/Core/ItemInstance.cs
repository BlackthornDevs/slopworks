using System;

[Serializable]
public struct ItemInstance
{
    public string definitionId;
    public string instanceId;
    public float durability;
    public int quality;

    public bool IsEmpty => string.IsNullOrEmpty(definitionId);

    public static ItemInstance Create(string definitionId)
    {
        return new ItemInstance
        {
            definitionId = definitionId,
            instanceId = null,
            durability = -1f,
            quality = 0
        };
    }

    public static readonly ItemInstance Empty = default;
}
