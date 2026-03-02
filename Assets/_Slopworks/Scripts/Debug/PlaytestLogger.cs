using UnityEngine;

/// <summary>
/// Verbose runtime input/action logger for playtest sessions.
/// Static singleton -- each system calls PlaytestLogger.Log() which is a no-op when disabled.
/// Add to the playtest scene alongside PlaytestValidator. Both independently toggleable.
/// </summary>
public class PlaytestLogger : MonoBehaviour
{
    public static PlaytestLogger Instance { get; private set; }

    [SerializeField] private bool _enabled;
    [SerializeField] private bool _logMovement;
    [SerializeField] private bool _logBreadcrumbs = true;
    [SerializeField] private float _breadcrumbInterval = 5f;

    private float _breadcrumbTimer;
    private Transform _playerTransform;
    private Rigidbody _playerRb;
    private PlaytestToolController _toolCtrl;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void Log(string msg)
    {
        if (Instance == null || !Instance._enabled) return;
        Debug.Log($"[LOG] {msg}");
    }

    public static void LogMovement(string msg)
    {
        if (Instance == null || !Instance._enabled || !Instance._logMovement) return;
        Debug.Log($"[LOG] {msg}");
    }

    private void Update()
    {
        if (!_enabled || !_logBreadcrumbs) return;

        _breadcrumbTimer += Time.deltaTime;
        if (_breadcrumbTimer < _breadcrumbInterval) return;
        _breadcrumbTimer = 0f;

        if (_playerTransform == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc == null) return;
            _playerTransform = pc.transform;
            _playerRb = pc.GetComponent<Rigidbody>();
        }

        if (_toolCtrl == null)
            _toolCtrl = FindAnyObjectByType<PlaytestToolController>();

        var pos = _playerTransform.position;
        var vel = _playerRb != null ? _playerRb.linearVelocity : Vector3.zero;
        string toolInfo = _toolCtrl != null
            ? $"tool={_toolCtrl.CurrentTool} level={_toolCtrl.CurrentLevel}"
            : "";
        Debug.Log($"[LOG] breadcrumb: pos=({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) vel=({vel.x:F1}, {vel.y:F1}, {vel.z:F1}) {toolInfo}");
    }
}
