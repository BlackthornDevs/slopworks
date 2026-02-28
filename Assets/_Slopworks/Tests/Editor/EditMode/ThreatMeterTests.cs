using NUnit.Framework;

[TestFixture]
public class ThreatMeterTests
{
    private ThreatMeter _meter;

    [SetUp]
    public void SetUp()
    {
        _meter = new ThreatMeter();
    }

    // ── initial state ───────────────────────────────

    [Test]
    public void InitialThreatIsZero()
    {
        Assert.AreEqual(0f, _meter.ThreatLevel);
    }

    // ── AddThreat ───────────────────────────────────

    [Test]
    public void AddThreat_IncreasesThreatLevel()
    {
        _meter.AddThreat(0.3f);

        Assert.AreEqual(0.3f, _meter.ThreatLevel, 0.001f);
    }

    [Test]
    public void AddThreat_Cumulative()
    {
        _meter.AddThreat(0.2f);
        _meter.AddThreat(0.3f);

        Assert.AreEqual(0.5f, _meter.ThreatLevel, 0.001f);
    }

    [Test]
    public void AddThreat_ClampsToOne()
    {
        _meter.AddThreat(0.8f);
        _meter.AddThreat(0.5f);

        Assert.AreEqual(1f, _meter.ThreatLevel);
    }

    [Test]
    public void AddThreat_ClampsToZero()
    {
        _meter.AddThreat(-0.5f);

        Assert.AreEqual(0f, _meter.ThreatLevel);
    }

    [Test]
    public void AddThreat_NegativeReducesThreat()
    {
        _meter.AddThreat(0.6f);
        _meter.AddThreat(-0.2f);

        Assert.AreEqual(0.4f, _meter.ThreatLevel, 0.001f);
    }

    // ── ScaleEnemyCount ─────────────────────────────

    [Test]
    public void ScaleEnemyCount_ZeroThreat_ReturnsBaseCount()
    {
        int scaled = _meter.ScaleEnemyCount(5);

        Assert.AreEqual(5, scaled);
    }

    [Test]
    public void ScaleEnemyCount_MaxThreat_DoublesEnemies()
    {
        _meter.AddThreat(1f);

        int scaled = _meter.ScaleEnemyCount(5);

        Assert.AreEqual(10, scaled);
    }

    [Test]
    public void ScaleEnemyCount_HalfThreat_ScalesCorrectly()
    {
        _meter.AddThreat(0.5f);

        // 3 * 1.5 = 4.5 → CeilToInt = 5
        int scaled = _meter.ScaleEnemyCount(3);

        Assert.AreEqual(5, scaled);
    }

    [Test]
    public void ScaleEnemyCount_CeilRoundsUp()
    {
        _meter.AddThreat(0.1f);

        // 3 * 1.1 = 3.3 → CeilToInt = 4
        int scaled = _meter.ScaleEnemyCount(3);

        Assert.AreEqual(4, scaled);
    }

    [Test]
    public void ScaleEnemyCount_ZeroBase_ReturnsZero()
    {
        _meter.AddThreat(1f);

        int scaled = _meter.ScaleEnemyCount(0);

        Assert.AreEqual(0, scaled);
    }
}
