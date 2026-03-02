using System.Collections.Generic;

/// <summary>
/// Pure C# manager tracking all buildings (D-004).
/// Register buildings, tick production, query state.
/// </summary>
public class BuildingManager
{
    private readonly List<BuildingState> _buildings = new();

    public int BuildingCount => _buildings.Count;

    public int ClaimedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].IsClaimed)
                    count++;
            }
            return count;
        }
    }

    public void Register(BuildingState building)
    {
        _buildings.Add(building);
    }

    public BuildingState Get(string buildingId)
    {
        for (int i = 0; i < _buildings.Count; i++)
        {
            if (_buildings[i].BuildingId == buildingId)
                return _buildings[i];
        }
        return null;
    }

    public void TickAll(float deltaTime)
    {
        for (int i = 0; i < _buildings.Count; i++)
            _buildings[i].Tick(deltaTime);
    }
}
