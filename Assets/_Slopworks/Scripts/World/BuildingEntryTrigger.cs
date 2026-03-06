using UnityEngine;

/// <summary>
/// Trigger near the factory that teleports the player to the building entrance.
/// Uses OnTriggerEnter with kinematic Rigidbody + isTrigger BoxCollider on VolumeTrigger layer.
/// In the real game this would call SceneLoaderBehaviour.TransitionTo().
/// Playtest simulates with teleport.
/// </summary>
public class BuildingEntryTrigger : MonoBehaviour
{
    private Transform _entranceSpawn;
    private System.Action _onPlayerEnter;
    private bool _triggered;

    public void Initialize(Transform entranceSpawn, System.Action onPlayerEnter)
    {
        _entranceSpawn = entranceSpawn;
        _onPlayerEnter = onPlayerEnter;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != PhysicsLayers.Player)
            return;

        if (_entranceSpawn == null)
            return;

        // Prevent double-fire from multiple colliders on the player hierarchy
        if (_triggered) return;
        _triggered = true;

        // Find the root player via Rigidbody (may be on parent)
        var rb = other.GetComponentInParent<Rigidbody>();
        Transform playerRoot = rb != null ? rb.transform : other.transform.root;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = _entranceSpawn.position;
        }
        playerRoot.position = _entranceSpawn.position;
        Physics.SyncTransforms();

        // Reset child local positions (teleport displaces compound collider children)
        foreach (Transform child in playerRoot)
            child.localPosition = Vector3.zero;

        PlaytestLogger.Log("event: entered building portal");
        Debug.Log("building: player entered portal");
        _onPlayerEnter?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == PhysicsLayers.Player)
            _triggered = false;
    }
}
