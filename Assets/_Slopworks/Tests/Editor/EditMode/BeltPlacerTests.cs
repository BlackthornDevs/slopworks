using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BeltPlacerTests
{
    private BeltPlacer _placer;

    [SetUp]
    public void SetUp()
    {
        _placer = new BeltPlacer();
    }

    // -- Initial state --

    [Test]
    public void NewPlacer_IsNotPlacing()
    {
        Assert.IsFalse(_placer.IsPlacing);
    }

    // -- Start placement --

    [Test]
    public void StartPlacement_SetsIsPlacing()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        Assert.IsTrue(_placer.IsPlacing);
    }

    [Test]
    public void StartPlacement_PreviewContainsStartCell()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        Assert.AreEqual(1, _placer.PreviewCells.Count);
        Assert.AreEqual(new Vector2Int(3, 5), _placer.PreviewCells[0]);
    }

    // -- Drag --

    [Test]
    public void UpdateDrag_Horizontal_ConstrainsToAxis()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        _placer.UpdateDrag(new Vector2Int(7, 6)); // more horizontal than vertical

        Assert.AreEqual(new Vector2Int(3, 5), _placer.StartCell);
        Assert.AreEqual(new Vector2Int(7, 5), _placer.EndCell); // Y snapped to start
    }

    [Test]
    public void UpdateDrag_Vertical_ConstrainsToAxis()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        _placer.UpdateDrag(new Vector2Int(4, 10)); // more vertical

        Assert.AreEqual(new Vector2Int(3, 10), _placer.EndCell); // X snapped to start
    }

    [Test]
    public void UpdateDrag_PreviewCellsCorrect()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        _placer.UpdateDrag(new Vector2Int(6, 5));

        Assert.AreEqual(4, _placer.PreviewCells.Count);
        Assert.AreEqual(new Vector2Int(3, 5), _placer.PreviewCells[0]);
        Assert.AreEqual(new Vector2Int(4, 5), _placer.PreviewCells[1]);
        Assert.AreEqual(new Vector2Int(5, 5), _placer.PreviewCells[2]);
        Assert.AreEqual(new Vector2Int(6, 5), _placer.PreviewCells[3]);
    }

    [Test]
    public void UpdateDrag_SetsPreviewDirection()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        _placer.UpdateDrag(new Vector2Int(6, 5));
        Assert.AreEqual(new Vector2Int(1, 0), _placer.PreviewDirection);
    }

    [Test]
    public void UpdateDrag_SetsPreviewLength()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        _placer.UpdateDrag(new Vector2Int(6, 5));
        Assert.AreEqual(3, _placer.PreviewLength);
    }

    [Test]
    public void UpdateDrag_NegativeDirection_Works()
    {
        _placer.StartPlacement(new Vector2Int(6, 5));
        _placer.UpdateDrag(new Vector2Int(3, 5));

        Assert.AreEqual(new Vector2Int(-1, 0), _placer.PreviewDirection);
        Assert.AreEqual(4, _placer.PreviewCells.Count);
    }

    [Test]
    public void UpdateDrag_WhenNotPlacing_DoesNothing()
    {
        _placer.UpdateDrag(new Vector2Int(6, 5));
        Assert.AreEqual(0, _placer.PreviewCells.Count);
    }

    // -- Finish placement --

    [Test]
    public void FinishPlacement_ValidLength_ReturnsStartEnd()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        _placer.UpdateDrag(new Vector2Int(6, 5));

        var result = _placer.FinishPlacement();

        Assert.IsNotNull(result);
        Assert.AreEqual(new Vector2Int(3, 5), result.Value.start);
        Assert.AreEqual(new Vector2Int(6, 5), result.Value.end);
        Assert.IsFalse(_placer.IsPlacing);
    }

    [Test]
    public void FinishPlacement_ZeroLength_ReturnsNull()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        // No drag, still at start cell

        var result = _placer.FinishPlacement();
        Assert.IsNull(result);
    }

    [Test]
    public void FinishPlacement_ClearsPreview()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        _placer.UpdateDrag(new Vector2Int(6, 5));
        _placer.FinishPlacement();

        Assert.AreEqual(0, _placer.PreviewCells.Count);
        Assert.AreEqual(Vector2Int.zero, _placer.PreviewDirection);
    }

    // -- Cancel --

    [Test]
    public void Cancel_StopsPlacing()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        _placer.UpdateDrag(new Vector2Int(6, 5));
        _placer.Cancel();

        Assert.IsFalse(_placer.IsPlacing);
        Assert.AreEqual(0, _placer.PreviewCells.Count);
    }

    // -- Equal deltas --

    [Test]
    public void UpdateDrag_EqualDeltas_SnapsToHorizontal()
    {
        _placer.StartPlacement(new Vector2Int(3, 5));
        _placer.UpdateDrag(new Vector2Int(6, 8)); // delta 3x, 3y -- equal

        // Should snap to horizontal (X axis) since absDx >= absDy
        Assert.AreEqual(new Vector2Int(6, 5), _placer.EndCell);
    }
}
