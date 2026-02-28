using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject _enemyPrefab;
    [SerializeField] private Transform[] _spawnPoints;

    public void SpawnWave(int count)
    {
        if (_enemyPrefab == null || _spawnPoints == null || _spawnPoints.Length == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            Transform point = _spawnPoints[i % _spawnPoints.Length];
            GameObject enemy = Instantiate(_enemyPrefab, point.position, point.rotation);
            enemy.layer = PhysicsLayers.Fauna;
        }
    }
}
