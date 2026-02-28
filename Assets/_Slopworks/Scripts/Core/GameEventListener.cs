using UnityEngine;
using UnityEngine.Events;

// Attach to any GameObject that needs to respond to a GameEventSO.
// Wire the event SO and UnityEvent response in the Inspector.
public class GameEventListener : MonoBehaviour
{
    [SerializeField] private GameEventSO _event;
    [SerializeField] private UnityEvent _response;

    private void OnEnable() => _event.RegisterListener(this);
    private void OnDisable() => _event.UnregisterListener(this);

    public void OnEventRaised() => _response.Invoke();
}
