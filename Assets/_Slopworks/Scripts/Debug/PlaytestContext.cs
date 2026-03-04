using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data class holding all shared references produced by PlaytestBootstrap.Setup().
/// Each dev bootstrapper receives one of these and passes it to PlaytestToolController.
/// </summary>
public class PlaytestContext
{
    // Infrastructure
    public FactoryGrid Grid;
    public SnapPointRegistry SnapRegistry;
    public StructuralPlacementService PlacementService;
    public BuildingPlacementService AutomationService;
    public FactorySimulation Simulation;
    public PortNodeRegistry PortRegistry;
    public ConnectionResolver ConnectionResolver;

    // Structural definitions
    public FoundationDefinitionSO FoundationDef;
    public WallDefinitionSO WallDef;
    public RampDefinitionSO RampDef;

    // Automation definitions
    public MachineDefinitionSO SmelterDef;
    public StorageDefinitionSO StorageDef;
    public ItemDefinitionSO IronOreDef;
    public ItemDefinitionSO IronIngotDef;
    public ItemDefinitionSO IronScrapDef;
    public ItemDefinitionSO TurretAmmoDef;
    public RecipeSO SmeltRecipe;
    public RecipeSO TurretAmmoRecipe;

    // Player
    public GameObject PlayerObject;
    public PlayerHUD PlayerHUD;
    public PlayerInventory PlayerInventory;
    public WeaponBehaviour WeaponBehaviour;

    // Combat
    public WeaponDefinitionSO WeaponDef;
    public FaunaDefinitionSO FaunaDef;
    public GameEventSO EnemyDiedEvent;
    public GameObject EnemyTemplate;

    // Cleanup list -- all runtime SOs, caller destroys in OnDestroy
    public List<ScriptableObject> RuntimeSOs;

    // Shared constants
    public const string IronOre = "iron_ore";
    public const string IronIngot = "iron_ingot";
    public const string IronScrap = "iron_scrap";
    public const string TurretAmmo = "turret_ammo";
    public const string SmeltIronRecipeId = "smelt_iron";
    public const string TurretAmmoRecipeId = "craft_turret_ammo";
    public const string SmelterType = "smelter";
}
