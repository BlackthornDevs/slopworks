public struct BuildingRepairedEvent
{
    public string BuildingId;
    public int NewRepairLevel;
}

public struct BuildingClaimedEvent
{
    public string BuildingId;
}

public struct BuildingUpgradedEvent
{
    public string BuildingId;
    public int NewTier;
}

public struct RoadBuiltEvent
{
    public string BuildingIdA;
    public string BuildingIdB;
}

public struct ProductionCollectedEvent
{
    public string BuildingId;
    public string ItemId;
    public int Amount;
}

public struct WorkerAssignedEvent
{
    public string BuildingId;
    public string WorkerId;
}

public struct WorkerUnassignedEvent
{
    public string BuildingId;
    public string WorkerId;
}
