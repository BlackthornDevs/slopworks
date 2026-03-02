using UnityEngine;

/// <summary>
/// Trigger inside the building that teleports the player back to the home base.
/// Uses OnTriggerEnter with kinematic Rigidbody + isTrigger BoxCollider on VolumeTrigger layer.
/// </summary>
public class BuildingExitTrigger : MonoBehaviour
{
    private Transform _exitDestination;
    private System.Action _onPlayerExit;

    public void Initialize(Transform exitDestination, System.Action onPlayerExit)
    {
        _exitDestination = exitDestination;
        _onPlayerExit = onPlayerExit;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != PhysicsLayers.Player)
            return;

        if (_exitDestination == null)
            return;

        // Teleport player
        var cc = other.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            other.transform.position = _exitDestination.position;
            cc.enabled = true;
        }
        else
        {
            other.transform.position = _exitDestination.position;
        }

        PlaytestLogger.Log("event: exited building portal");
        Debug.Log("building: player exited warehouse");
        _onPlayerExit?.Invoke();
    }
}
