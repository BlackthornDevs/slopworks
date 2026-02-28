using System;

/// <summary>
/// Core machine simulation logic. Plain C# class (D-004) -- no MonoBehaviour,
/// fully testable in EditMode. Owns input/output item buffers and drives the
/// craft cycle: Idle -> Working -> (output check) -> Idle or Blocked.
/// </summary>
public class Machine
{
    private readonly MachineDefinitionSO _definition;
    private readonly ItemSlot[] _inputBuffer;
    private readonly ItemSlot[] _outputBuffer;

    private string _activeRecipeId;
    private MachineStatus _status;
    private float _craftProgress;

    // Cached recipe reference to avoid repeated lookups while crafting
    private RecipeSO _cachedRecipe;

    public MachineDefinitionSO Definition => _definition;
    public MachineStatus Status => _status;
    public string ActiveRecipeId => _activeRecipeId;
    public float CraftProgress => _craftProgress;

    public Machine(MachineDefinitionSO definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _inputBuffer = new ItemSlot[definition.inputBufferSize];
        _outputBuffer = new ItemSlot[definition.outputBufferSize];
        _status = MachineStatus.Idle;
    }

    /// <summary>
    /// Sets the active recipe by ID. Resets craft progress and returns to Idle.
    /// </summary>
    public void SetRecipe(string recipeId)
    {
        _activeRecipeId = recipeId;
        _craftProgress = 0f;
        _status = MachineStatus.Idle;
        _cachedRecipe = null;
    }

    /// <summary>
    /// Clears the active recipe. Machine goes Idle with zero progress.
    /// </summary>
    public void ClearRecipe()
    {
        _activeRecipeId = null;
        _craftProgress = 0f;
        _status = MachineStatus.Idle;
        _cachedRecipe = null;
    }

    /// <summary>
    /// Attempts to insert items into the specified input buffer slot.
    /// Returns false if the slot already contains a different item type
    /// or if there is no room.
    /// </summary>
    public bool TryInsertInput(int slotIndex, ItemInstance item, int count)
    {
        if (slotIndex < 0 || slotIndex >= _inputBuffer.Length)
            return false;
        if (item.IsEmpty || count <= 0)
            return false;

        var existing = _inputBuffer[slotIndex];
        if (existing.IsEmpty)
        {
            _inputBuffer[slotIndex] = new ItemSlot { item = item, count = count };
            return true;
        }

        // Slot has items -- must be same type
        if (existing.item.definitionId != item.definitionId)
            return false;

        _inputBuffer[slotIndex] = new ItemSlot { item = existing.item, count = existing.count + count };
        return true;
    }

    /// <summary>
    /// Returns the contents of the specified input buffer slot.
    /// </summary>
    public ItemSlot GetInput(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _inputBuffer.Length)
            return ItemSlot.Empty;
        return _inputBuffer[slotIndex];
    }

    /// <summary>
    /// Returns the contents of the specified output buffer slot.
    /// </summary>
    public ItemSlot GetOutput(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _outputBuffer.Length)
            return ItemSlot.Empty;
        return _outputBuffer[slotIndex];
    }

    /// <summary>
    /// Removes up to the specified count from an output buffer slot.
    /// Returns the items actually extracted.
    /// </summary>
    public ItemSlot ExtractOutput(int slotIndex, int count)
    {
        if (slotIndex < 0 || slotIndex >= _outputBuffer.Length)
            return ItemSlot.Empty;
        if (count <= 0)
            return ItemSlot.Empty;

        var existing = _outputBuffer[slotIndex];
        if (existing.IsEmpty)
            return ItemSlot.Empty;

        int extracted = Math.Min(count, existing.count);
        int remaining = existing.count - extracted;

        if (remaining <= 0)
        {
            _outputBuffer[slotIndex] = ItemSlot.Empty;
        }
        else
        {
            _outputBuffer[slotIndex] = new ItemSlot { item = existing.item, count = remaining };
        }

        return new ItemSlot { item = existing.item, count = extracted };
    }

    /// <summary>
    /// Core simulation tick. Drives the machine through its craft cycle:
    /// Idle (check inputs) -> Working (increment progress) -> produce output or Blocked.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last tick in seconds.</param>
    /// <param name="recipeLookup">Delegate to resolve a recipe ID to a RecipeSO.</param>
    public void Tick(float deltaTime, Func<string, RecipeSO> recipeLookup)
    {
        if (string.IsNullOrEmpty(_activeRecipeId))
            return;

        var recipe = ResolveRecipe(recipeLookup);
        if (recipe == null)
            return;

        if (_status == MachineStatus.Idle)
        {
            TryStartCrafting(recipe);
            // If we just transitioned to Working, apply deltaTime in the same tick
        }

        if (_status == MachineStatus.Working)
        {
            AdvanceCrafting(deltaTime, recipe);
        }
        else if (_status == MachineStatus.Blocked)
        {
            TryPushOutputs(recipe);
        }
    }

    private RecipeSO ResolveRecipe(Func<string, RecipeSO> recipeLookup)
    {
        if (_cachedRecipe != null && _cachedRecipe.recipeId == _activeRecipeId)
            return _cachedRecipe;

        _cachedRecipe = recipeLookup?.Invoke(_activeRecipeId);
        return _cachedRecipe;
    }

    private void TryStartCrafting(RecipeSO recipe)
    {
        if (!HasSufficientInputs(recipe))
            return;

        ConsumeInputs(recipe);
        _status = MachineStatus.Working;
        _craftProgress = 0f;
    }

    private void AdvanceCrafting(float deltaTime, RecipeSO recipe)
    {
        _craftProgress += deltaTime * _definition.processingSpeed;

        if (_craftProgress >= recipe.craftDuration)
        {
            if (CanPushOutputs(recipe))
            {
                PushOutputs(recipe);
                _craftProgress = 0f;
                _status = MachineStatus.Idle;
            }
            else
            {
                _status = MachineStatus.Blocked;
            }
        }
    }

    private void TryPushOutputs(RecipeSO recipe)
    {
        if (CanPushOutputs(recipe))
        {
            PushOutputs(recipe);
            _craftProgress = 0f;
            _status = MachineStatus.Idle;
        }
    }

    private bool HasSufficientInputs(RecipeSO recipe)
    {
        if (recipe.inputs == null)
            return true;

        foreach (var ingredient in recipe.inputs)
        {
            int available = CountItemInInputBuffer(ingredient.itemId);
            if (available < ingredient.count)
                return false;
        }

        return true;
    }

    private int CountItemInInputBuffer(string itemId)
    {
        int total = 0;
        for (int i = 0; i < _inputBuffer.Length; i++)
        {
            var slot = _inputBuffer[i];
            if (!slot.IsEmpty && slot.item.definitionId == itemId)
                total += slot.count;
        }
        return total;
    }

    private void ConsumeInputs(RecipeSO recipe)
    {
        if (recipe.inputs == null)
            return;

        foreach (var ingredient in recipe.inputs)
        {
            int remaining = ingredient.count;
            for (int i = 0; i < _inputBuffer.Length && remaining > 0; i++)
            {
                var slot = _inputBuffer[i];
                if (slot.IsEmpty || slot.item.definitionId != ingredient.itemId)
                    continue;

                int consumed = Math.Min(remaining, slot.count);
                remaining -= consumed;
                int left = slot.count - consumed;

                _inputBuffer[i] = left <= 0
                    ? ItemSlot.Empty
                    : new ItemSlot { item = slot.item, count = left };
            }
        }
    }

    private bool CanPushOutputs(RecipeSO recipe)
    {
        if (recipe.outputs == null)
            return true;

        // Check that each output ingredient can fit somewhere in the output buffer.
        // We track hypothetical additions per slot to handle multiple outputs correctly.
        int[] hypotheticalCounts = new int[_outputBuffer.Length];
        string[] hypotheticalItems = new string[_outputBuffer.Length];

        for (int i = 0; i < _outputBuffer.Length; i++)
        {
            hypotheticalCounts[i] = _outputBuffer[i].count;
            hypotheticalItems[i] = _outputBuffer[i].IsEmpty ? null : _outputBuffer[i].item.definitionId;
        }

        foreach (var output in recipe.outputs)
        {
            int toPlace = output.count;

            for (int i = 0; i < _outputBuffer.Length && toPlace > 0; i++)
            {
                if (hypotheticalItems[i] == null)
                {
                    // Empty slot, can use it
                    hypotheticalItems[i] = output.itemId;
                    hypotheticalCounts[i] = toPlace;
                    toPlace = 0;
                }
                else if (hypotheticalItems[i] == output.itemId)
                {
                    // Same item type, can stack
                    hypotheticalCounts[i] += toPlace;
                    toPlace = 0;
                }
            }

            if (toPlace > 0)
                return false;
        }

        return true;
    }

    private void PushOutputs(RecipeSO recipe)
    {
        if (recipe.outputs == null)
            return;

        foreach (var output in recipe.outputs)
        {
            int toPlace = output.count;

            for (int i = 0; i < _outputBuffer.Length && toPlace > 0; i++)
            {
                var slot = _outputBuffer[i];

                if (slot.IsEmpty)
                {
                    _outputBuffer[i] = new ItemSlot
                    {
                        item = ItemInstance.Create(output.itemId),
                        count = toPlace
                    };
                    toPlace = 0;
                }
                else if (slot.item.definitionId == output.itemId)
                {
                    _outputBuffer[i] = new ItemSlot
                    {
                        item = slot.item,
                        count = slot.count + toPlace
                    };
                    toPlace = 0;
                }
            }
        }
    }
}
