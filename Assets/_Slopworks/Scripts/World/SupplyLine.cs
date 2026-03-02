using System;
using System.Collections.Generic;

/// <summary>
/// Pure C# supply connection with transport delay (D-004).
/// Subscribes to source building's OnItemProduced, holds items in-flight
/// for the configured delay, then delivers to destination via TryInsert.
/// </summary>
public class SupplyLine : IDisposable
{
    public struct InFlightItem
    {
        public string ItemId;
        public int Amount;
        public float RemainingTime;
    }

    private readonly BuildingState _source;
    private readonly IItemDestination _destination;
    private readonly float _transportDelay;
    private readonly List<InFlightItem> _inFlight = new();
    private bool _disposed;

    private int _totalDelivered;

    public BuildingState Source => _source;
    public IItemDestination Destination => _destination;
    public float TransportDelay => _transportDelay;
    public int InFlightCount => _inFlight.Count;
    public int TotalDelivered => _totalDelivered;
    public IReadOnlyList<InFlightItem> InFlightItems => _inFlight;

    public event Action<string, int> OnItemDelivered;
    public event Action<string, int> OnItemLost;

    public SupplyLine(BuildingState source, IItemDestination destination, float transportDelay)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _destination = destination ?? throw new ArgumentNullException(nameof(destination));
        _transportDelay = transportDelay;

        _source.OnItemProduced += HandleItemProduced;
    }

    private void HandleItemProduced(string itemId, int amount)
    {
        if (_disposed) return;

        _inFlight.Add(new InFlightItem
        {
            ItemId = itemId,
            Amount = amount,
            RemainingTime = _transportDelay
        });
    }

    /// <summary>
    /// Advances all in-flight timers. Delivers arrived items to destination.
    /// Items that cannot be inserted (destination full) are lost.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (_disposed) return;

        for (int i = _inFlight.Count - 1; i >= 0; i--)
        {
            var item = _inFlight[i];
            item.RemainingTime -= deltaTime;
            _inFlight[i] = item;

            if (item.RemainingTime <= 0f)
            {
                _inFlight.RemoveAt(i);

                int delivered = 0;
                for (int j = 0; j < item.Amount; j++)
                {
                    if (_destination.TryInsert(item.ItemId))
                        delivered++;
                }

                if (delivered > 0)
                {
                    _totalDelivered += delivered;
                    OnItemDelivered?.Invoke(item.ItemId, delivered);
                }

                int lost = item.Amount - delivered;
                if (lost > 0)
                {
                    OnItemLost?.Invoke(item.ItemId, lost);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source.OnItemProduced -= HandleItemProduced;
    }
}
