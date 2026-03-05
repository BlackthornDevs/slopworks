using System;
using System.Collections.Generic;

/// <summary>
/// Pure C# tower run state manager. Tracks the current run: building, cleared chunks,
/// banked fragments, difficulty tier. Loot and carried fragments live in PlayerInventory.
/// </summary>
public class TowerController
{
    private TowerBuildingDefinitionSO _building;
    private readonly HashSet<int> _clearedChunks = new HashSet<int>();
    private readonly HashSet<int> _fragmentChunks = new HashSet<int>();
    private int _bankedFragments;
    private int _currentTier = 1;
    private bool _isRunActive;

    public TowerBuildingDefinitionSO CurrentBuilding => _building;
    public int BankedFragments => _bankedFragments;
    public int CurrentTier => _currentTier;
    public bool IsRunActive => _isRunActive;

    /// <summary>
    /// Check if the given chunk index has a fragment this run.
    /// </summary>
    public bool HasFragment(int chunkIndex)
    {
        return _fragmentChunks.Contains(chunkIndex);
    }

    /// <summary>
    /// Initialize a new run. Resets cleared chunks and randomizes fragment placement.
    /// </summary>
    public void StartRun(TowerBuildingDefinitionSO building)
    {
        _building = building;
        _clearedChunks.Clear();
        _fragmentChunks.Clear();
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
    /// Bank carried fragments (from inventory). Returns total banked fragment count.
    /// </summary>
    public int Extract(int carriedFragments)
    {
        _bankedFragments += carriedFragments;
        _isRunActive = false;
        return _bankedFragments;
    }

    /// <summary>
    /// Player died. Ends the run. Banked fragments are safe.
    /// Caller is responsible for clearing tower items from inventory.
    /// </summary>
    public void Die()
    {
        _isRunActive = false;
    }

    /// <summary>
    /// Check if the boss floor can be unlocked. Carried + banked fragments must meet requirement.
    /// </summary>
    public bool UnlockBoss(int carriedFragments)
    {
        if (_building == null)
            return false;

        return (_bankedFragments + carriedFragments) >= _building.requiredFragments;
    }

    /// <summary>
    /// Consume all banked fragments. Called when entering the boss floor.
    /// Returns the number of fragments consumed.
    /// </summary>
    public int ConsumeFragments()
    {
        int consumed = _bankedFragments;
        _bankedFragments = 0;
        return consumed;
    }

    /// <summary>
    /// Boss defeated. Increment tier, end run.
    /// Fragment reset happens via ConsumeFragments on boss floor entry, not here.
    /// </summary>
    public void CompleteBoss()
    {
        _currentTier++;
        _isRunActive = false;
    }

    private void RandomizeFragments()
    {
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
            _fragmentChunks.Add(candidates[i]);
    }
}
