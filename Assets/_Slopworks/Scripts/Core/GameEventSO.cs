using System.Collections.Generic;
using UnityEngine;

// ScriptableObject event asset. Lives in ScriptableObjects/Events/.
// Any scene can raise or listen to an event through the SO asset reference —
// no direct scene-to-scene references needed.
[CreateAssetMenu(menuName = "Events/Game Event")]
public class GameEventSO : ScriptableObject
{
    private readonly List<GameEventListener> _listeners = new();

    public void Raise()
    {
        // iterate in reverse so listeners can safely deregister during callback
        for (int i = _listeners.Count - 1; i >= 0; i--)
            _listeners[i].OnEventRaised();
    }

    public void RegisterListener(GameEventListener listener)
    {
        if (!_listeners.Contains(listener))
            _listeners.Add(listener);
    }

    public void UnregisterListener(GameEventListener listener)
    {
        _listeners.Remove(listener);
    }
}
