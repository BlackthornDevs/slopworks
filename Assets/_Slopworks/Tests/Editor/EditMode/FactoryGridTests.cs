using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class FactoryGridTests
{
    private FactoryGrid _grid;

    [SetUp]
    public void SetUp()
    {
        _grid = new FactoryGrid();
    }

    // -- WorldToCell / CellToWorld --

    [Test]
    public void WorldToCell_ReturnsCorrectCell()
    {
        var cell = _grid.WorldToCell(new Vector3(2.7f, 0f, 5.3f));
        Assert.AreEqual(new Vector2Int(2, 5), cell);
    }

    [Test]
    public void WorldToCell_NegativeCoords_ReturnsNegativeCell()
    {
        var cell = _grid.WorldToCell(new Vector3(-1.5f, 0f, -0.1f));
        Assert.AreEqual(new Vector2Int(-2, -1), cell);
    }

    [Test]
    public void CellToWorld_ReturnsCellCenter()
    {
        var world = _grid.CellToWorld(new Vector2Int(3, 7));
        Assert.AreEqual(3.5f, world.x, 0.001f);
        Assert.AreEqual(0f, world.y, 0.001f);
        Assert.AreEqual(7.5f, world.z, 0.001f);
    }

    [Test]
    public void WorldToCell_And_CellToWorld_RoundTrip()
    {
        var original = new Vector2Int(10, 20);
        var world = _grid.CellToWorld(original);
        var back = _grid.WorldToCell(world);
        Assert.AreEqual(original, back);
    }

    // -- Placement --

    [Test]
    public void CanPlace_EmptyGrid_ReturnsTrue()
    {
        Assert.IsTrue(_grid.CanPlace(new Vector2Int(0, 0), new Vector2Int(2, 2)));
    }

    [Test]
    public void Place_ThenGetAt_ReturnsBuildingData()
    {
        var data = new BuildingData("foundation_1x1", new Vector2Int(5, 5), new Vector2Int(1, 1));
        _grid.Place(new Vector2Int(5, 5), new Vector2Int(1, 1), data);

        Assert.AreSame(data, _grid.GetAt(new Vector2Int(5, 5)));
    }

    [Test]
    public void Place_MultiCell_AllCellsOccupied()
    {
        var data = new BuildingData("smelter", new Vector2Int(10, 10), new Vector2Int(3, 2));
        _grid.Place(new Vector2Int(10, 10), new Vector2Int(3, 2), data);

        for (int x = 10; x < 13; x++)
        {
            for (int z = 10; z < 12; z++)
            {
                Assert.AreSame(data, _grid.GetAt(new Vector2Int(x, z)),
                    $"Cell ({x},{z}) should be occupied");
            }
        }
    }

    // -- Overlap rejection --

    [Test]
    public void CanPlace_OverlapsExisting_ReturnsFalse()
    {
        var data = new BuildingData("foundation", new Vector2Int(5, 5), new Vector2Int(2, 2));
        _grid.Place(new Vector2Int(5, 5), new Vector2Int(2, 2), data);

        Assert.IsFalse(_grid.CanPlace(new Vector2Int(6, 6), new Vector2Int(2, 2)));
    }

    [Test]
    public void CanPlace_AdjacentNoOverlap_ReturnsTrue()
    {
        var data = new BuildingData("foundation", new Vector2Int(5, 5), new Vector2Int(2, 2));
        _grid.Place(new Vector2Int(5, 5), new Vector2Int(2, 2), data);

        // Place right next to it with no overlap
        Assert.IsTrue(_grid.CanPlace(new Vector2Int(7, 5), new Vector2Int(2, 2)));
    }

    // -- Out of bounds --

    [Test]
    public void CanPlace_OutOfBounds_Negative_ReturnsFalse()
    {
        Assert.IsFalse(_grid.CanPlace(new Vector2Int(-1, 0), new Vector2Int(1, 1)));
    }

    [Test]
    public void CanPlace_OutOfBounds_ExceedsWidth_ReturnsFalse()
    {
        Assert.IsFalse(_grid.CanPlace(new Vector2Int(199, 0), new Vector2Int(2, 1)));
    }

    [Test]
    public void CanPlace_OutOfBounds_ExceedsHeight_ReturnsFalse()
    {
        Assert.IsFalse(_grid.CanPlace(new Vector2Int(0, 199), new Vector2Int(1, 2)));
    }

    [Test]
    public void CanPlace_ExactlyAtBoundary_ReturnsTrue()
    {
        Assert.IsTrue(_grid.CanPlace(new Vector2Int(199, 199), new Vector2Int(1, 1)));
    }

    [Test]
    public void GetAt_OutOfBounds_ReturnsNull()
    {
        Assert.IsNull(_grid.GetAt(new Vector2Int(-1, -1)));
        Assert.IsNull(_grid.GetAt(new Vector2Int(200, 200)));
    }

    // -- Removal --

    [Test]
    public void Remove_ClearsOccupiedCells()
    {
        var data = new BuildingData("foundation", new Vector2Int(5, 5), new Vector2Int(2, 2));
        _grid.Place(new Vector2Int(5, 5), new Vector2Int(2, 2), data);
        _grid.Remove(new Vector2Int(5, 5), new Vector2Int(2, 2));

        Assert.IsNull(_grid.GetAt(new Vector2Int(5, 5)));
        Assert.IsNull(_grid.GetAt(new Vector2Int(6, 6)));
    }

    [Test]
    public void Remove_ThenCanPlace_ReturnsTrue()
    {
        var data = new BuildingData("foundation", new Vector2Int(5, 5), new Vector2Int(2, 2));
        _grid.Place(new Vector2Int(5, 5), new Vector2Int(2, 2), data);
        _grid.Remove(new Vector2Int(5, 5), new Vector2Int(2, 2));

        Assert.IsTrue(_grid.CanPlace(new Vector2Int(5, 5), new Vector2Int(2, 2)));
    }

    [Test]
    public void GetAt_EmptyCell_ReturnsNull()
    {
        Assert.IsNull(_grid.GetAt(new Vector2Int(50, 50)));
    }
}
