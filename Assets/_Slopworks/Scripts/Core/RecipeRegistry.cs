using System.Collections.Generic;
using UnityEngine;

public class RecipeRegistry : MonoBehaviour
{
    [SerializeField] private RecipeSO[] _recipes;

    private readonly Dictionary<string, RecipeSO> _lookup = new();
    private readonly Dictionary<string, List<RecipeSO>> _byMachine = new();

    private void Awake()
    {
        foreach (var recipe in _recipes)
        {
            if (!_lookup.TryAdd(recipe.recipeId, recipe))
            {
                Debug.LogWarning($"duplicate recipe id: {recipe.recipeId}");
                continue;
            }

            if (!string.IsNullOrEmpty(recipe.requiredMachineType))
            {
                if (!_byMachine.TryGetValue(recipe.requiredMachineType, out var list))
                {
                    list = new List<RecipeSO>();
                    _byMachine[recipe.requiredMachineType] = list;
                }
                list.Add(recipe);
            }
        }
    }

    public RecipeSO Get(string recipeId)
    {
        _lookup.TryGetValue(recipeId, out var recipe);
        return recipe;
    }

    public IReadOnlyList<RecipeSO> GetForMachine(string machineType)
    {
        if (_byMachine.TryGetValue(machineType, out var list))
            return list;
        return System.Array.Empty<RecipeSO>();
    }
}
