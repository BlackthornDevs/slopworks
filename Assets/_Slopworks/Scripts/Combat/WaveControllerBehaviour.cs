using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class WaveControllerBehaviour : NetworkBehaviour
{
    [SerializeField] private EnemySpawner _spawner;
    [SerializeField] private List<WaveDefinition> _waves;
    [SerializeField] private GameEventSO _waveStartedEvent;
    [SerializeField] private GameEventSO _waveEndedEvent;
    [SerializeField] private GameEventSO _enemyDiedEvent;
    [SerializeField] private float _autoStartDelay = -1f;
    [SerializeField] private List<TowerSpawnEntry> _spawnEntries;

    private WaveController _controller;
    private ThreatMeter _threat;
    private bool _spawnInProgress;

    public WaveController Controller => _controller;
    public ThreatMeter Threat => _threat;

    private void Awake()
    {
        _threat = new ThreatMeter();
        _controller = new WaveController(_waves, _threat);

        _controller.OnWaveStarted += HandleWaveStarted;
        _controller.OnWaveEnded += HandleWaveEnded;
    }

    private IEnumerator Start()
    {
        if (NetworkObject != null && !IsServerInitialized) yield break;

        if (_autoStartDelay >= 0f)
        {
            yield return new WaitForSeconds(_autoStartDelay);
            BeginNextWave();
        }
    }

    private void OnEnable()
    {
        if (_enemyDiedEvent != null)
        {
            var listener = GetComponent<GameEventListener>();
            if (listener == null)
                listener = gameObject.AddComponent<GameEventListener>();

            listener.Configure(_enemyDiedEvent, ReportEnemyKilled);
        }
    }

    private void OnDestroy()
    {
        _controller.OnWaveStarted -= HandleWaveStarted;
        _controller.OnWaveEnded -= HandleWaveEnded;
    }

    public void BeginNextWave()
    {
        if (NetworkObject != null && !IsServerInitialized) return;

        if (_spawnInProgress)
        {
            Debug.LogWarning("wave spawn already in progress");
            return;
        }

        var def = _controller.StartNextWave();
        if (def == null)
        {
            Debug.Log("all waves complete");
            return;
        }

        StartCoroutine(SpawnWaveCoroutine(def));
    }

    public void ReportEnemyKilled()
    {
        if (NetworkObject != null && !IsServerInitialized) return;

        _controller.OnEnemyKilled();
    }

    private IEnumerator SpawnWaveCoroutine(WaveDefinition def)
    {
        _spawnInProgress = true;
        int spawnCount = _controller.EnemiesRemaining;

        if (_spawnEntries != null && _spawnEntries.Count > 0)
        {
            int spawned = 0;
            foreach (var entry in _spawnEntries)
            {
                for (int i = 0; i < entry.count; i++)
                {
                    if (_spawner != null)
                        _spawner.SpawnOne(entry.templateIndex);

                    spawned++;
                    if (def.spawnDelay > 0f && spawned < spawnCount)
                        yield return new WaitForSeconds(def.spawnDelay);
                }
            }
        }
        else
        {
            for (int i = 0; i < spawnCount; i++)
            {
                if (_spawner != null)
                    _spawner.SpawnWave(1);

                if (def.spawnDelay > 0f && i < spawnCount - 1)
                    yield return new WaitForSeconds(def.spawnDelay);
            }
        }

        _spawnInProgress = false;
    }

    private void HandleWaveStarted()
    {
        if (_waveStartedEvent != null)
            _waveStartedEvent.Raise();

        Debug.Log("wave " + (_controller.CurrentWave + 1) + " started — " +
                  _controller.EnemiesRemaining + " enemies");
    }

    private void HandleWaveEnded()
    {
        if (_waveEndedEvent != null)
            _waveEndedEvent.Raise();

        Debug.Log("wave " + (_controller.CurrentWave + 1) + " cleared");

        if (_controller.CurrentWave + 1 < _controller.TotalWaves)
        {
            float rest = _waves[_controller.CurrentWave].timeBetweenWaves;
            StartCoroutine(RestThenNextWave(rest));
        }
    }

    private IEnumerator RestThenNextWave(float delay)
    {
        yield return new WaitForSeconds(delay);
        BeginNextWave();
    }
}
