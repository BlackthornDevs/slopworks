using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class NetworkFactorySimulation : NetworkBehaviour
{
    private FactorySimulation _simulation;

    private readonly List<NetworkMachine> _machines = new();
    private readonly List<NetworkBeltSegment> _belts = new();

    // Runtime recipe lookup -- create test recipes on server start
    private readonly Dictionary<string, RecipeSO> _recipes = new();

    public FactorySimulation Simulation => _simulation;

    public override void OnStartServer()
    {
        base.OnStartServer();
        CreateTestRecipes();
        _simulation = new FactorySimulation(LookupRecipe);
    }

    public void RegisterMachine(NetworkMachine netMachine)
    {
        if (netMachine.Machine == null) return;
        _machines.Add(netMachine);
        _simulation.RegisterMachine(netMachine.Machine);
    }

    public void RegisterBelt(NetworkBeltSegment netBelt)
    {
        if (netBelt.Segment == null) return;
        _belts.Add(netBelt);
        _simulation.RegisterBelt(netBelt.Segment);
    }

    public void RegisterInserter(Inserter inserter)
    {
        _simulation.RegisterInserter(inserter);
    }

    private void FixedUpdate()
    {
        if (!IsServerInitialized) return;
        if (_simulation == null) return;

        _simulation.Tick(Time.fixedDeltaTime);

        // Sync state from simulation to network
        for (int i = 0; i < _machines.Count; i++)
            _machines[i].ServerSyncState();
        for (int i = 0; i < _belts.Count; i++)
            _belts[i].ServerSyncState();
    }

    private RecipeSO LookupRecipe(string recipeId)
    {
        _recipes.TryGetValue(recipeId, out var recipe);
        return recipe;
    }

    private void CreateTestRecipes()
    {
        // Smelting: iron_scrap -> iron_ingot
        var smeltRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        smeltRecipe.recipeId = "smelt_iron";
        smeltRecipe.displayName = "Smelt Iron";
        smeltRecipe.requiredMachineType = "smelter";
        smeltRecipe.craftDuration = 2f;
        smeltRecipe.inputs = new[] { new RecipeIngredient { itemId = "iron_scrap", count = 1 } };
        smeltRecipe.outputs = new[] { new RecipeIngredient { itemId = "iron_ingot", count = 1 } };
        _recipes[smeltRecipe.recipeId] = smeltRecipe;

        // Smelting: copper_scrap -> copper_ingot
        var copperRecipe = ScriptableObject.CreateInstance<RecipeSO>();
        copperRecipe.recipeId = "smelt_copper";
        copperRecipe.displayName = "Smelt Copper";
        copperRecipe.requiredMachineType = "smelter";
        copperRecipe.craftDuration = 2f;
        copperRecipe.inputs = new[] { new RecipeIngredient { itemId = "copper_scrap", count = 1 } };
        copperRecipe.outputs = new[] { new RecipeIngredient { itemId = "copper_ingot", count = 1 } };
        _recipes[copperRecipe.recipeId] = copperRecipe;

        Debug.Log($"factory: {_recipes.Count} test recipes created");
    }
}
