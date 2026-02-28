using System;

/// <summary>
/// Core inserter simulation logic. Plain C# class (D-004) -- no MonoBehaviour,
/// fully testable in EditMode. Transfers items one at a time from an IItemSource
/// to an IItemDestination with a configurable swing duration.
///
/// The inserter cycle:
/// 1. Idle (no item held): try to extract from source.
/// 2. If extraction succeeds, begin swinging (timer starts at swingDuration).
/// 3. When swing completes (timer reaches 0): try to deposit into destination.
/// 4. If destination accepts: item delivered, return to step 1.
/// 5. If destination rejects: item is kept, swing resets for retry next tick.
/// </summary>
public class Inserter
{
    private readonly IItemSource _source;
    private readonly IItemDestination _destination;
    private readonly float _swingDuration;

    private string _heldItemId;
    private float _swingTimer;
    private bool _isSwinging;

    /// <summary>
    /// The item the inserter is currently holding, or null if empty.
    /// </summary>
    public string HeldItemId => _heldItemId;

    /// <summary>
    /// Whether the inserter arm is currently in motion.
    /// </summary>
    public bool IsSwinging => _isSwinging;

    /// <summary>
    /// Progress of the current swing from 0 (just picked up) to 1 (arrived at destination).
    /// Returns 0 when not swinging.
    /// </summary>
    public float SwingProgress
    {
        get
        {
            if (!_isSwinging || _swingDuration <= 0f)
                return 0f;
            return 1f - (_swingTimer / _swingDuration);
        }
    }

    /// <summary>
    /// The source this inserter pulls items from.
    /// </summary>
    public IItemSource Source => _source;

    /// <summary>
    /// The destination this inserter pushes items to.
    /// </summary>
    public IItemDestination Destination => _destination;

    /// <param name="source">Where to pull items from.</param>
    /// <param name="destination">Where to push items to.</param>
    /// <param name="swingDuration">Time in seconds for one transfer swing.</param>
    public Inserter(IItemSource source, IItemDestination destination, float swingDuration)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _destination = destination ?? throw new ArgumentNullException(nameof(destination));

        if (swingDuration <= 0f)
            throw new ArgumentOutOfRangeException(nameof(swingDuration), "Swing duration must be positive.");

        _swingDuration = swingDuration;
    }

    /// <summary>
    /// Advances the inserter simulation by deltaTime seconds.
    /// </summary>
    public void Tick(float deltaTime)
    {
        // If not holding anything and not swinging, try to pick up from source
        if (_heldItemId == null && !_isSwinging)
        {
            if (_source.TryExtract(out string itemId))
            {
                _heldItemId = itemId;
                _isSwinging = true;
                _swingTimer = _swingDuration;
            }

            // Nothing available, nothing to do
            return;
        }

        // If holding an item but not swinging, a previous deposit was rejected.
        // Try again each tick without requiring another swing.
        if (_heldItemId != null && !_isSwinging)
        {
            if (_destination.TryInsert(_heldItemId))
            {
                _heldItemId = null;
            }
            return;
        }

        // If swinging, advance the timer
        if (_isSwinging)
        {
            _swingTimer -= deltaTime;

            if (_swingTimer <= 0f)
            {
                // Swing complete -- try to deposit
                if (_destination.TryInsert(_heldItemId))
                {
                    // Successfully delivered
                    _heldItemId = null;
                    _isSwinging = false;
                    _swingTimer = 0f;
                }
                else
                {
                    // Destination rejected the item -- keep holding it.
                    // Stop swinging; retry deposit on subsequent ticks.
                    _isSwinging = false;
                    _swingTimer = 0f;
                }
            }
        }
    }
}
