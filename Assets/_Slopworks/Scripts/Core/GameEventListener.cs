using UnityEngine;
using UnityEngine.Events;

public class GameEventListener : MonoBehaviour
{
    [SerializeField] private GameEventSO _event;
    [SerializeField] private UnityEvent _response;

    private void OnEnable()
    {
        if (_event != null)
            _event.RegisterListener(this);
    }

    private void OnDisable()
    {
        if (_event != null)
            _event.UnregisterListener(this);
    }

    public void OnEventRaised() => _response.Invoke();

    /// <summary>
    /// Wire this listener at runtime instead of via inspector.
    /// </summary>
    public void Configure(GameEventSO evt, UnityAction callback)
    {
        if (_event != null && enabled)
            _event.UnregisterListener(this);

        _event = evt;
        _response = new UnityEvent();
        _response.AddListener(callback);

        if (_event != null && enabled)
            _event.RegisterListener(this);
    }
}
