using UnityEngine;

/// <summary>
/// Thin MonoBehaviour wrapper around the Machine simulation class.
/// Owns a Machine instance and exposes it for other systems to drive.
/// All simulation logic lives in Machine (D-004).
/// </summary>
public class MachineBehaviour : MonoBehaviour
{
    [SerializeField] private MachineDefinitionSO _definition;

    private Machine _machine;

    public Machine Machine => _machine;

    private void Awake()
    {
        if (_definition == null)
        {
            Debug.LogError("MachineBehaviour: missing machine definition", this);
            return;
        }

        _machine = new Machine(_definition);
    }
}
