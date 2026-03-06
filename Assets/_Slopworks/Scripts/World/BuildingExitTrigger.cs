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

        // Teleport player -- use Rigidbody (player has no CharacterController)
        var rb = other.GetComponentInParent<Rigidbody>();
        Transform playerRoot = rb != null ? rb.transform : other.transform.root;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = _exitDestination.position;
        }
        playerRoot.position = _exitDestination.position;
        Physics.SyncTransforms();

        // Reset child local positions (teleport displaces compound collider children)
        foreach (Transform child in playerRoot)
            child.localPosition = Vector3.zero;

        PlaytestLogger.Log("event: exited building portal");
        Debug.Log("building: player exited warehouse");
        _onPlayerExit?.Invoke();
    }
}
