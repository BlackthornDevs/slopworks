using System.Collections.Generic;
using UnityEngine;
using NPBehave;

/// <summary>
/// Singleton-per-fauna-type coordinator that tracks pack membership and shared state.
/// Uses NPBehave shared blackboards so all pack members receive updates automatically.
/// </summary>
public class PackCoordinator
{
    private static readonly Dictionary<string, PackCoordinator> _packs =
        new Dictionary<string, PackCoordinator>();

    private readonly List<FaunaController> _members = new List<FaunaController>();
    private readonly List<float> _deathTimes = new List<float>();
    private readonly Blackboard _sharedBlackboard;
    private float _baseBravery = 0.5f;

    private static readonly float[] FlankAngles = { 0f, 90f, -90f, 180f };
    private const float DEATH_WINDOW = 10f;
    private const float ALERT_STALE_TIME = 15f;

    public Blackboard SharedBlackboard => _sharedBlackboard;
    public int AliveCount => _members.Count;

    private PackCoordinator(string faunaId)
    {
        _sharedBlackboard = UnityContext.GetSharedBlackboard("pack_" + faunaId);

        // initialize shared keys so child blackboard writes propagate to parent
        _sharedBlackboard["alert_valid"] = false;
        _sharedBlackboard["alert_position"] = Vector3.zero;
        _sharedBlackboard["alert_time"] = 0f;
        _sharedBlackboard["pack_confidence"] = 1f;
        _sharedBlackboard["ally_death_time"] = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnDomainReload()
    {
        _packs.Clear();
    }

    public static PackCoordinator GetOrCreate(string faunaId)
    {
        if (!_packs.TryGetValue(faunaId, out var pack))
        {
            pack = new PackCoordinator(faunaId);
            _packs[faunaId] = pack;
        }
        return pack;
    }

    public void Register(FaunaController member)
    {
        if (_members.Contains(member))
            return;

        if (_members.Count == 0)
            _baseBravery = member.Definition.baseBravery;

        _members.Add(member);
        UpdateConfidence();
    }

    public void Unregister(FaunaController member)
    {
        _members.Remove(member);
        UpdateConfidence();
    }

    public void ReportPlayerSighted(Vector3 playerPos)
    {
        _sharedBlackboard["alert_valid"] = true;
        _sharedBlackboard["alert_position"] = playerPos;
        _sharedBlackboard["alert_time"] = Time.time;
    }

    public void ReportAllyDeath(Vector3 deathPos)
    {
        _deathTimes.Add(Time.time);
        _sharedBlackboard["ally_death_time"] = Time.time;
        UpdateConfidence();
    }

    /// <summary>
    /// Distributes approach angles among pack members targeting the same player.
    /// Member 0 = direct, 1 = +90, 2 = -90, 3 = 180, with ±15 deg jitter.
    /// </summary>
    public float GetFlankAngle(FaunaController self)
    {
        int index = _members.IndexOf(self);
        if (index < 0)
            return 0f;

        float baseAngle = FlankAngles[index % FlankAngles.Length];
        float jitter = UnityEngine.Random.Range(-15f, 15f);
        return baseAngle + jitter;
    }

    public float Confidence
    {
        get
        {
            int recentDeaths = CountRecentDeaths(Time.time);
            return CalculateConfidence(AliveCount, recentDeaths, _baseBravery);
        }
    }

    private void UpdateConfidence()
    {
        _sharedBlackboard["pack_confidence"] = Confidence;
    }

    private int CountRecentDeaths(float currentTime)
    {
        float cutoff = currentTime - DEATH_WINDOW;
        int count = 0;
        for (int i = _deathTimes.Count - 1; i >= 0; i--)
        {
            if (_deathTimes[i] >= cutoff)
                count++;
            else
                break;
        }
        return count;
    }

    /// <summary>
    /// Pure math for confidence calculation — exposed for testing.
    /// Confidence = Clamp01(aliveCount * 0.25 - recentDeaths * 0.3 + baseBravery * 0.5)
    /// </summary>
    public static float CalculateConfidence(int aliveCount, int recentDeaths, float baseBravery)
    {
        return Mathf.Clamp01(aliveCount * 0.25f - recentDeaths * 0.3f + baseBravery * 0.5f);
    }

    public static void ClearAll()
    {
        _packs.Clear();
    }
}
