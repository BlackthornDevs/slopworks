public struct DamageData
{
    public float amount;
    public string sourceId;
    public DamageType type;

    public DamageData(float amount, string sourceId, DamageType type)
    {
        this.amount = amount;
        this.sourceId = sourceId;
        this.type = type;
    }
}
