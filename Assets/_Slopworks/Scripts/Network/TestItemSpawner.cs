using FishNet.Object;
using UnityEngine;

public class TestItemSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject _worldItemPrefab;
    [SerializeField] private int _itemCount = 5;
    [SerializeField] private Vector3 _spawnCenter = new(50f, 0.5f, 50f);
    [SerializeField] private float _spawnRadius = 5f;

    public override void OnStartServer()
    {
        base.OnStartServer();
        SpawnTestItems();
    }

    private void SpawnTestItems()
    {
        string[] itemIds = { "iron_scrap", "iron_ingot", "copper_scrap" };

        for (int i = 0; i < _itemCount; i++)
        {
            float angle = i * (360f / _itemCount);
            Vector3 pos = _spawnCenter + new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * _spawnRadius,
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * _spawnRadius
            );

            var go = Instantiate(_worldItemPrefab, pos, Quaternion.identity);
            var worldItem = go.GetComponent<NetworkWorldItem>();
            worldItem.Setup(itemIds[i % itemIds.Length], Random.Range(1, 5));
            ServerManager.Spawn(go);

            Debug.Log($"spawner: {worldItem.ItemId} x{worldItem.Count} at {pos}");
        }
    }
}
