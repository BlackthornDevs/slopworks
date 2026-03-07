using System;

[Serializable]
public class ProductionDefinition
{
    public string producedItemId;
    public int producedAmount = 1;
    public float productionInterval = 30f;
    public bool requiresSupplyLine;
}
