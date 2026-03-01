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
        if (_definition == null)
        {
            Debug.LogError("MachineBehaviour: missing machine definition", this);
            return;
        }

        _machine = new Machine(_definition);
        gameObject.layer = PhysicsLayers.Interactable;
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
