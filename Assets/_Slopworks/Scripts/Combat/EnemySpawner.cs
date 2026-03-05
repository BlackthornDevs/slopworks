using FishNet.Object;
using UnityEngine;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private GameObject[] _enemyTemplates;
    [SerializeField] private Transform[] _spawnPoints;

    private int _spawnIndex;

    public void SpawnWave(int count)
    {
        if (NetworkObject != null && !IsServerInitialized) return;

        if (_enemyTemplates == null || _enemyTemplates.Length == 0 || _spawnPoints == null || _spawnPoints.Length == 0)
            return;

        for (int i = 0; i < count; i++)
            SpawnOne(0);
    }

    public void SpawnOne(int templateIndex)
    {
        if (NetworkObject != null && !IsServerInitialized) return;

        if (_enemyTemplates == null || _enemyTemplates.Length == 0 || _spawnPoints == null || _spawnPoints.Length == 0)
            return;

        if (templateIndex < 0 || templateIndex >= _enemyTemplates.Length)
            templateIndex = 0;

        var template = _enemyTemplates[templateIndex];
        if (template == null) return;

        Transform point = _spawnPoints[_spawnIndex % _spawnPoints.Length];
        _spawnIndex++;
        GameObject enemy = Instantiate(template, point.position, point.rotation);
        enemy.SetActive(true);
        enemy.layer = PhysicsLayers.Fauna;
    }
}
