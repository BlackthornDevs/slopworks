using UnityEngine;

public struct DamageData
{
    public float amount;
    public string sourceId;
    public DamageType type;
    public Vector3 sourcePosition;

    public DamageData(float amount, string sourceId, DamageType type)
    {
        this.amount = amount;
        this.sourceId = sourceId;
        this.type = type;
        this.sourcePosition = Vector3.zero;
    }

    public DamageData(float amount, string sourceId, DamageType type, Vector3 sourcePosition)
    {
        this.amount = amount;
        this.sourceId = sourceId;
        this.type = type;
        this.sourcePosition = sourcePosition;
    }
}
