using System;
using System.Collections.Generic;

/// <summary>
/// Pure C# tower run state manager. Tracks the current run: building, cleared chunks,
/// carried loot, banked loot/fragments, difficulty tier. No MonoBehaviour dependency.
/// </summary>
public class TowerController
{
    private TowerBuildingDefinitionSO _building;
    private readonly HashSet<int> _clearedChunks = new HashSet<int>();
    private readonly List<ItemInstance> _carriedLoot = new List<ItemInstance>();
    private readonly List<ItemInstance> _bankedLoot = new List<ItemInstance>();
    private int _carriedFragments;
    private int _bankedFragments;
    private int _currentTier = 1;
    private bool _isRunActive;

    public TowerBuildingDefinitionSO CurrentBuilding => _building;
    public IReadOnlyList<ItemInstance> CarriedLoot => _carriedLoot;
    public IReadOnlyList<ItemInstance> BankedLoot => _bankedLoot;
    public int CarriedFragments => _carriedFragments;
    public int BankedFragments => _bankedFragments;
    public int CurrentTier => _currentTier;
    public bool IsRunActive => _isRunActive;

    /// <summary>
    /// Initialize a new run. Resets carried state and randomizes fragment placement.
    /// </summary>
    public void StartRun(TowerBuildingDefinitionSO building)
    {
        _building = building;
        _clearedChunks.Clear();
        _carriedLoot.Clear();
        _carriedFragments = 0;
        _isRunActive = true;

        RandomizeFragments();
    }

    /// <summary>
    /// Mark a chunk as cleared. Out-of-range indices are ignored.
    /// </summary>
    public void ClearChunk(int chunkIndex)
    {
        if (chunkIndex < 0 || chunkIndex >= _building.chunks.Count)
            return;

        _clearedChunks.Add(chunkIndex);
    }

    /// <summary>
    /// Check if a chunk has been cleared this run.
    /// </summary>
    public bool IsChunkCleared(int chunkIndex)
    {
        return _clearedChunks.Contains(chunkIndex);
    }

    /// <summary>
    /// Add an item to carried loot.
    /// </summary>
    public void CollectLoot(ItemInstance item)
    {
        _carriedLoot.Add(item);
    }

    /// <summary>
    /// Collect a key fragment (carried, not yet banked).
    /// </summary>
    public void CollectFragment()
    {
        _carriedFragments++;
    }

    /// <summary>
    /// Bank all carried loot and fragments. Returns total banked fragment count.
    /// </summary>
    public int Extract()
    {
        _bankedFragments += _carriedFragments;
        _carriedFragments = 0;

        _bankedLoot.AddRange(_carriedLoot);
        _carriedLoot.Clear();

        _isRunActive = false;

        return _bankedFragments;
    }

    /// <summary>
    /// Player died. Clear all carried loot and fragments. Banked items are safe.
    /// </summary>
    public void Die()
    {
        _carriedLoot.Clear();
        _carriedFragments = 0;
        _isRunActive = false;
    }

    /// <summary>
    /// Check if the boss floor can be unlocked. Requires banked fragments >= building requirement.
    /// </summary>
    public bool UnlockBoss()
    {
        if (_building == null)
            return false;

        return _bankedFragments >= _building.requiredFragments;
    }

    /// <summary>
    /// Boss defeated. Increment tier, reset banked fragments, start new cycle.
    /// </summary>
    public void CompleteBoss()
    {
        _currentTier++;
        _bankedFragments = 0;
        _isRunActive = false;
    }

    private void RandomizeFragments()
    {
        // Clear previous fragment flags
        for (int i = 0; i < _building.chunks.Count; i++)
            _building.chunks[i].hasFragment = false;

        // Collect non-boss chunk indices
        var candidates = new List<int>();
        for (int i = 0; i < _building.chunks.Count; i++)
        {
            if (i != _building.bossChunkIndex)
                candidates.Add(i);
        }

        // Clamp fragment count to available non-boss chunks
        int fragmentsToPlace = Math.Min(_building.requiredFragments, candidates.Count);

        // Fisher-Yates shuffle, then take first N
        var rng = new System.Random();
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }

        for (int i = 0; i < fragmentsToPlace; i++)
            _building.chunks[candidates[i]].hasFragment = true;
    }
}
