using PixelVoxel.Core;

namespace PixelVoxel.Core.Tests;

public sealed class VoxelEditingTests
{
    private static readonly Rgba32Color White = new(255, 255, 255, 255);
    private static readonly Rgba32Color Red = new(255, 0, 0, 255);

    [Fact]
    public void AddUndoAndRedoRestoreExactCellState()
    {
        VoxelDocument document = EmptyDocument(new VoxelDimensions(3, 3, 3));
        VoxelEditHistory history = new();
        VoxelCoordinate coordinate = new(1, 1, 1);

        Assert.True(history.Execute(document, new AddVoxelsCommand([coordinate], White)));
        Assert.True(document.Storage.TryGetCell(coordinate, out VoxelCell? added));
        Assert.Equal(White, added!.GetColor(VoxelFace.Front));

        Assert.True(history.Undo(document));
        Assert.False(document.Storage.TryGetCell(coordinate, out _));
        Assert.True(history.Redo(document));
        Assert.True(document.Storage.TryGetCell(coordinate, out VoxelCell? restored));
        Assert.Equal(added, restored);
    }

    [Fact]
    public void PaintCanChangeOneFaceOrAllFaces()
    {
        VoxelCoordinate coordinate = new(0, 0, 0);
        VoxelDocument document = Document(
            new VoxelDimensions(1, 1, 1),
            new VoxelEntry(coordinate, VoxelCell.CreateUniform(White)));
        VoxelEditHistory history = new();

        history.Execute(document, new PaintVoxelsCommand([coordinate], VoxelFace.Top, Red));

        Assert.True(document.Storage.TryGetCell(coordinate, out VoxelCell? facePainted));
        Assert.Equal(Red, facePainted!.GetColor(VoxelFace.Top));
        Assert.Equal(White, facePainted.GetColor(VoxelFace.Front));

        history.Execute(document, new PaintVoxelsCommand([coordinate], VoxelFace.Front, Red, true));

        Assert.All(Enum.GetValues<VoxelFace>(), face => Assert.Equal(Red, document.Storage.TryGetCell(coordinate, out VoxelCell? cell) ? cell!.GetColor(face) : default));
    }

    [Fact]
    public void MoveSelectionRejectsOccupiedDestinationWithoutPartialChanges()
    {
        VoxelCoordinate source = new(0, 0, 0);
        VoxelCoordinate blocker = new(1, 0, 0);
        VoxelDocument document = Document(
            new VoxelDimensions(3, 1, 1),
            new VoxelEntry(source, VoxelCell.CreateUniform(White)),
            new VoxelEntry(blocker, VoxelCell.CreateUniform(Red)));
        VoxelEditHistory history = new();

        Assert.Throws<InvalidOperationException>(() => history.Execute(
            document,
            new MoveVoxelSelectionCommand(
                new VoxelSelectionBox(source, source),
                new VoxelCoordinate(1, 0, 0))));

        Assert.Equal(2, document.Storage.OccupiedCount);
        Assert.True(document.Storage.TryGetCell(source, out _));
        Assert.True(document.Storage.TryGetCell(blocker, out _));
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void MoveSelectionAndUndoPreserveColors()
    {
        VoxelCoordinate source = new(0, 0, 0);
        VoxelCoordinate destination = new(1, 0, 0);
        VoxelDocument document = Document(
            new VoxelDimensions(3, 1, 1),
            new VoxelEntry(source, VoxelCell.CreateUniform(Red)));
        VoxelEditHistory history = new();

        history.Execute(
            document,
            new MoveVoxelSelectionCommand(
                new VoxelSelectionBox(source, source),
                new VoxelCoordinate(1, 0, 0)));

        Assert.False(document.Storage.TryGetCell(source, out _));
        Assert.True(document.Storage.TryGetCell(destination, out VoxelCell? moved));
        Assert.Equal(Red, moved!.GetColor(VoxelFace.Front));
        history.Undo(document);
        Assert.True(document.Storage.TryGetCell(source, out _));
        Assert.False(document.Storage.TryGetCell(destination, out _));
    }

    [Fact]
    public void ResizeRequiresExplicitClippingAndCanBeUndone()
    {
        VoxelCoordinate clipped = new(2, 0, 0);
        VoxelDocument document = Document(
            new VoxelDimensions(3, 1, 1),
            new VoxelEntry(clipped, VoxelCell.CreateUniform(White)));
        VoxelEditHistory history = new();

        Assert.Throws<InvalidOperationException>(() => history.Execute(
            document,
            new ResizeVoxelVolumeCommand(new VoxelDimensions(2, 1, 1), false)));

        history.Execute(document, new ResizeVoxelVolumeCommand(new VoxelDimensions(2, 1, 1), true));
        Assert.Equal(new VoxelDimensions(2, 1, 1), document.Storage.Dimensions);
        Assert.False(document.Storage.TryGetCell(clipped, out _));
        history.Undo(document);
        Assert.Equal(new VoxelDimensions(3, 1, 1), document.Storage.Dimensions);
        Assert.True(document.Storage.TryGetCell(clipped, out _));
    }

    [Fact]
    public void CleanCheckpointTracksUndoAndRedoState()
    {
        VoxelDocument document = EmptyDocument(new VoxelDimensions(2, 1, 1));
        VoxelEditHistory history = new();
        history.Execute(document, new AddVoxelsCommand([new VoxelCoordinate(0, 0, 0)], White));
        history.MarkClean();
        Assert.False(history.IsDirty);

        history.Execute(document, new AddVoxelsCommand([new VoxelCoordinate(1, 0, 0)], Red));
        Assert.True(history.IsDirty);
        history.Undo(document);
        Assert.False(history.IsDirty);
        history.Redo(document);
        Assert.True(history.IsDirty);
    }

    [Fact]
    public void ProjectHistoryUndoesAcrossFrameContextsInEditOrder()
    {
        VoxelDocument first = EmptyDocument(new VoxelDimensions(1, 1, 1));
        VoxelDocument second = EmptyDocument(new VoxelDimensions(1, 1, 1));
        VoxelDocument[] frames = [first, second];
        VoxelEditHistory history = new();
        VoxelCoordinate coordinate = new(0, 0, 0);
        history.Execute(first, new AddVoxelsCommand([coordinate], White), contextId: 0);
        history.Execute(second, new AddVoxelsCommand([coordinate], Red), contextId: 1);

        Assert.True(history.Undo(index => frames[index], out int firstContext));
        Assert.Equal(1, firstContext);
        Assert.Equal(1, first.Storage.OccupiedCount);
        Assert.Equal(0, second.Storage.OccupiedCount);
        Assert.True(history.Undo(index => frames[index], out int secondContext));
        Assert.Equal(0, secondContext);
        Assert.Equal(0, first.Storage.OccupiedCount);
        Assert.True(history.Redo(index => frames[index], out int redoContext));
        Assert.Equal(0, redoContext);
        Assert.Equal(1, first.Storage.OccupiedCount);
    }

    private static VoxelDocument EmptyDocument(VoxelDimensions dimensions) =>
        Document(dimensions);

    private static VoxelDocument Document(VoxelDimensions dimensions, params VoxelEntry[] entries) =>
        new(new TestStorage(dimensions, entries));

    private sealed class TestStorage : IVoxelStorage
    {
        private readonly IReadOnlyDictionary<VoxelCoordinate, VoxelCell> _cells;

        public TestStorage(VoxelDimensions dimensions, IEnumerable<VoxelEntry> entries)
        {
            Dimensions = dimensions;
            _cells = entries.ToDictionary(entry => entry.Coordinate, entry => entry.Cell);
        }

        public VoxelDimensions Dimensions { get; }

        public int OccupiedCount => _cells.Count;

        public bool TryGetCell(VoxelCoordinate coordinate, out VoxelCell? cell) =>
            _cells.TryGetValue(coordinate, out cell);

        public IEnumerable<VoxelEntry> GetOccupiedCells() =>
            _cells.Select(pair => new VoxelEntry(pair.Key, pair.Value));
    }
}
