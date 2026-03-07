using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class NetworkBeltSegment : NetworkBehaviour
{
    [SerializeField] private int _lengthInTiles = 1;

    private BeltSegment _segment;

    private readonly SyncList<BeltItem> _syncItems = new();
    private readonly SyncVar<ushort> _syncTerminalGap = new();

    public BeltSegment Segment => _segment;
    public int ItemCount => _syncItems.Count;

    private Vector3 _startPos;
    private Vector3 _endPos;

    public Vector3 StartPos => _startPos;
    public Vector3 EndPos => _endPos;

    public void ServerInit(BeltSegment segment, Vector3 startPos, Vector3 endPos)
    {
        _segment = segment;
        _startPos = startPos;
        _endPos = endPos;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (_segment == null)
            _segment = new BeltSegment(_lengthInTiles);
    }

    public void ServerSyncState()
    {
        if (_segment == null) return;

        var items = _segment.GetItems();

        // Sync item list
        while (_syncItems.Count > items.Count)
            _syncItems.RemoveAt(_syncItems.Count - 1);

        for (int i = 0; i < items.Count; i++)
        {
            if (i < _syncItems.Count)
            {
                if (_syncItems[i].itemId != items[i].itemId ||
                    _syncItems[i].distanceToNext != items[i].distanceToNext)
                    _syncItems[i] = items[i];
            }
            else
            {
                _syncItems.Add(items[i]);
            }
        }

        _syncTerminalGap.Value = _segment.TerminalGap;
    }

    public void GetItemWorldPositions(List<Vector3> positions)
    {
        positions.Clear();
        if (_syncItems.Count == 0) return;

        int totalLength = _lengthInTiles * BeltItem.SubdivisionsPerTile;
        if (totalLength == 0) return;

        float cumulative = 0f;
        for (int i = 0; i < _syncItems.Count; i++)
        {
            cumulative += _syncItems[i].distanceToNext;
            float t = cumulative / totalLength;
            positions.Add(Vector3.Lerp(_startPos, _endPos, t));
        }
    }
}
