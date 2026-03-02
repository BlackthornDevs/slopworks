using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around the Machine simulation class.
/// Implements IInteractable for player interaction (recipe selection).
/// All simulation logic lives in Machine (D-004).
/// </summary>
public class MachineBehaviour : MonoBehaviour, IInteractable
{
    [SerializeField] private MachineDefinitionSO _definition;

    private Machine _machine;

    public Machine Machine => _machine;
    public MachineDefinitionSO Definition => _definition;

    private void Awake()
    {
        // Skip if Initialize() already set the machine (bootstrapper path)
        if (_machine != null)
            return;

        if (_definition == null)
        {
            Debug.LogError("MachineBehaviour: missing machine definition", this);
            return;
        }

        _machine = new Machine(_definition);
        gameObject.layer = PhysicsLayers.Interactable;
    }

    /// <summary>
    /// Initializes with an existing machine (used by bootstrappers that create the
    /// machine before adding the component).
    /// </summary>
    public void Initialize(MachineDefinitionSO definition, Machine machine)
    {
        _definition = definition;
        _machine = machine;
    }

    public string GetInteractionPrompt()
    {
        return $"press E to configure {_definition.displayName}";
    }

    public void Interact(GameObject player)
    {
        var recipeUI = FindAnyObjectByType<RecipeSelectionUI>();
        if (recipeUI != null)
            recipeUI.Open(this, player.GetComponent<PlayerInventory>());
    }
}
