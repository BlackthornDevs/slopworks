using System;
using System.Collections.Generic;

public class WaveController
{
    private readonly List<WaveDefinition> _waves;
    private readonly ThreatMeter _threat;

    private int _currentWave = -1;
    private int _enemiesRemaining;
    private bool _waveActive;

    public bool IsWaveActive => _waveActive;
    public int CurrentWave => _currentWave;
    public int EnemiesRemaining => _enemiesRemaining;
    public int TotalWaves => _waves.Count;

    public event Action OnWaveStarted;
    public event Action OnWaveEnded;

    public WaveController(List<WaveDefinition> waves, ThreatMeter threat)
    {
        _waves = waves;
        _threat = threat;
    }

    public WaveDefinition StartNextWave()
    {
        _currentWave++;
        if (_currentWave >= _waves.Count)
            return null;

        var def = _waves[_currentWave];
        _enemiesRemaining = _threat.ScaleEnemyCount(def.enemyCount);
        _waveActive = true;

        OnWaveStarted?.Invoke();
        return def;
    }

    public void OnEnemyKilled()
    {
        if (!_waveActive)
            return;

        _enemiesRemaining--;
        if (_enemiesRemaining <= 0)
        {
            _enemiesRemaining = 0;
            _waveActive = false;
            OnWaveEnded?.Invoke();
        }
    }
}
