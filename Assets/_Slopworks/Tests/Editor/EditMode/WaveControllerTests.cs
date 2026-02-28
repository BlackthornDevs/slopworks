using System.Collections.Generic;
using NUnit.Framework;

[TestFixture]
public class WaveControllerTests
{
    private WaveController _controller;
    private ThreatMeter _threat;
    private List<WaveDefinition> _waves;

    [SetUp]
    public void SetUp()
    {
        _threat = new ThreatMeter();
        _waves = new List<WaveDefinition>
        {
            new WaveDefinition
            {
                enemyCount = 5,
                spawnDelay = 0.5f,
                timeBetweenWaves = 3f,
                faunaIds = new[] { "test_grunt" }
            },
            new WaveDefinition
            {
                enemyCount = 8,
                spawnDelay = 0.3f,
                timeBetweenWaves = 5f,
                faunaIds = new[] { "test_grunt", "test_brute" }
            }
        };
        _controller = new WaveController(_waves, _threat);
    }

    // -- WaveController tests --

    [Test]
    public void StartNextWave_SetsWaveActive()
    {
        _controller.StartNextWave();

        Assert.IsTrue(_controller.IsWaveActive);
    }

    [Test]
    public void StartNextWave_TracksEnemyCount()
    {
        _controller.StartNextWave();

        Assert.AreEqual(5, _controller.EnemiesRemaining);
    }

    [Test]
    public void StartNextWave_IncrementsCurrentWave()
    {
        _controller.StartNextWave();
        Assert.AreEqual(0, _controller.CurrentWave);

        // kill all to end wave
        for (int i = 0; i < 5; i++)
            _controller.OnEnemyKilled();

        _controller.StartNextWave();
        Assert.AreEqual(1, _controller.CurrentWave);
    }

    [Test]
    public void StartNextWave_ReturnsNullWhenAllWavesComplete()
    {
        _controller.StartNextWave();
        for (int i = 0; i < 5; i++) _controller.OnEnemyKilled();

        _controller.StartNextWave();
        for (int i = 0; i < 8; i++) _controller.OnEnemyKilled();

        var result = _controller.StartNextWave();
        Assert.IsNull(result);
    }

    [Test]
    public void OnEnemyKilled_DecrementsRemaining()
    {
        _controller.StartNextWave();
        _controller.OnEnemyKilled();

        Assert.AreEqual(4, _controller.EnemiesRemaining);
    }

    [Test]
    public void OnEnemyKilled_DoesNothingWhenNoWaveActive()
    {
        _controller.OnEnemyKilled();

        Assert.AreEqual(0, _controller.EnemiesRemaining);
        Assert.IsFalse(_controller.IsWaveActive);
    }

    [Test]
    public void WaveEnds_WhenAllEnemiesDead()
    {
        _controller.StartNextWave();
        for (int i = 0; i < 5; i++)
            _controller.OnEnemyKilled();

        Assert.IsFalse(_controller.IsWaveActive);
        Assert.AreEqual(0, _controller.EnemiesRemaining);
    }

    [Test]
    public void OnWaveStarted_Fires()
    {
        bool fired = false;
        _controller.OnWaveStarted += () => fired = true;

        _controller.StartNextWave();

        Assert.IsTrue(fired);
    }

    [Test]
    public void OnWaveEnded_Fires_WhenAllEnemiesDead()
    {
        bool fired = false;
        _controller.OnWaveEnded += () => fired = true;

        _controller.StartNextWave();
        for (int i = 0; i < 5; i++)
            _controller.OnEnemyKilled();

        Assert.IsTrue(fired);
    }

    [Test]
    public void OnWaveEnded_DoesNotFire_WhenEnemiesRemain()
    {
        bool fired = false;
        _controller.OnWaveEnded += () => fired = true;

        _controller.StartNextWave();
        _controller.OnEnemyKilled();

        Assert.IsFalse(fired);
    }

    // -- ThreatMeter tests --

    [Test]
    public void Threat_StartsAtZero()
    {
        Assert.AreEqual(0f, _threat.ThreatLevel);
    }

    [Test]
    public void Threat_Increases()
    {
        _threat.AddThreat(0.3f);

        Assert.AreEqual(0.3f, _threat.ThreatLevel, 0.001f);
    }

    [Test]
    public void Threat_ClampsToOne()
    {
        _threat.AddThreat(0.8f);
        _threat.AddThreat(0.5f);

        Assert.AreEqual(1f, _threat.ThreatLevel, 0.001f);
    }

    [Test]
    public void Threat_DoesNotGoNegative()
    {
        _threat.AddThreat(-0.5f);

        Assert.AreEqual(0f, _threat.ThreatLevel, 0.001f);
    }

    [Test]
    public void Threat_ScalesEnemyCount()
    {
        // at 0 threat: 1x multiplier
        Assert.AreEqual(5, _threat.ScaleEnemyCount(5));

        // at 0.5 threat: 1.5x multiplier → ceil(7.5) = 8
        _threat.AddThreat(0.5f);
        Assert.AreEqual(8, _threat.ScaleEnemyCount(5));

        // at 1.0 threat: 2x multiplier → 10
        _threat.AddThreat(0.5f);
        Assert.AreEqual(10, _threat.ScaleEnemyCount(5));
    }

    [Test]
    public void Threat_AffectsWaveEnemyCount()
    {
        _threat.AddThreat(0.5f);
        _controller.StartNextWave();

        // base 5 enemies * 1.5 multiplier = ceil(7.5) = 8
        Assert.AreEqual(8, _controller.EnemiesRemaining);
    }

    // -- additional edge-case tests --

    [Test]
    public void EmptyWaveList_ReturnsNull()
    {
        var empty = new WaveController(new List<WaveDefinition>(), _threat);

        Assert.IsNull(empty.StartNextWave());
    }

    [Test]
    public void OnEnemyKilled_ExtraKillsDoNotGoNegative()
    {
        _controller.StartNextWave(); // 5 enemies
        for (int i = 0; i < 10; i++)
            _controller.OnEnemyKilled();

        Assert.AreEqual(0, _controller.EnemiesRemaining);
    }

    [Test]
    public void StartNextWave_ReturnsCorrectDefinition()
    {
        var def = _controller.StartNextWave();

        Assert.AreEqual(5, def.enemyCount);
        Assert.AreEqual(0.5f, def.spawnDelay);
        Assert.AreEqual("test_grunt", def.faunaIds[0]);
    }

    [Test]
    public void TotalWaves_MatchesListCount()
    {
        Assert.AreEqual(2, _controller.TotalWaves);
    }

    [Test]
    public void CurrentWave_StartsAtNegativeOne()
    {
        Assert.AreEqual(-1, _controller.CurrentWave);
    }

    [Test]
    public void Threat_ScaleEnemyCount_ZeroBase_ReturnsZero()
    {
        _threat.AddThreat(0.5f);

        Assert.AreEqual(0, _threat.ScaleEnemyCount(0));
    }

    [Test]
    public void StartNextWave_PastEnd_KeepsReturningNull()
    {
        // exhaust all waves
        _controller.StartNextWave();
        for (int i = 0; i < 5; i++) _controller.OnEnemyKilled();
        _controller.StartNextWave();
        for (int i = 0; i < 8; i++) _controller.OnEnemyKilled();

        Assert.IsNull(_controller.StartNextWave());
        Assert.IsNull(_controller.StartNextWave()); // second call past end
    }
}
