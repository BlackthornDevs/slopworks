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

        // Teleport player
        var cc = other.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            other.transform.position = _entranceSpawn.position;
            cc.enabled = true;
        }
        else
        {
            other.transform.position = _entranceSpawn.position;
        }

        Debug.Log("building: player entered warehouse");
        _onPlayerEnter?.Invoke();
    }
}
