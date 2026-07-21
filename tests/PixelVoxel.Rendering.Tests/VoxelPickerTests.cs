using System.Numerics;
using PixelVoxel.Core;

namespace PixelVoxel.Rendering.Tests;

public sealed class VoxelPickerTests
{
    [Fact]
    public void ViewportMappingAccountsForIntegerScaleAndLetterbox()
    {
        PixelViewportMapping mapping = PixelViewportMapping.Create(240, 100, 20, 10, null);

        Assert.Equal(10, mapping.Scale);
        Assert.Equal(20, mapping.DestinationX);
        Assert.True(mapping.TryMapToLogical(25f, 5f, out Vector2 logical));
        Assert.Equal(new Vector2(0.5f, 0.5f), logical);
        Assert.False(mapping.TryMapToLogical(19f, 5f, out _));
    }

    [Fact]
    public void ManualScaleRemainsCentered()
    {
        PixelViewportMapping mapping = PixelViewportMapping.Create(200, 100, 20, 10, 2);

        Assert.Equal(2, mapping.Scale);
        Assert.Equal(80, mapping.DestinationX);
        Assert.Equal(40, mapping.DestinationY);
        Assert.True(mapping.TryMapToLogical(81f, 41f, out Vector2 logical));
        Assert.Equal(new Vector2(0.5f, 0.5f), logical);
    }

    [Fact]
    public void FrontCameraPicksFrontFaceAndAdjacentCoordinate()
    {
        VoxelDocument document = SingleVoxelDocument();
        VoxelRenderTransform transform = Resolve(
            new VoxelCameraState(VoxelViewMode.PixelPreview, VoxelCameraPreset.Front, 0f, 0f, 0f, 0f, 1f),
            VoxelModelRotationState.Identity);

        VoxelPickResult? result = new VoxelPicker().Pick(document, transform, new Vector2(16f, 16f));

        Assert.NotNull(result);
        Assert.Equal(new VoxelCoordinate(0, 0, 0), result.Coordinate);
        Assert.Equal(VoxelFace.Front, result.Face);
        Assert.Equal(new VoxelCoordinate(0, 0, 1), result.AdjacentCoordinate);
    }

    [Fact]
    public void PickingUsesTheInverseModelRotation()
    {
        VoxelDocument document = SingleVoxelDocument();
        VoxelRenderTransform transform = Resolve(
            new VoxelCameraState(VoxelViewMode.PixelPreview, VoxelCameraPreset.Front, 0f, 0f, 0f, 0f, 1f),
            new VoxelModelRotationState(90f, 0f));

        VoxelPickResult? result = new VoxelPicker().Pick(document, transform, new Vector2(16f, 16f));

        Assert.NotNull(result);
        Assert.Equal(new VoxelCoordinate(0, 0, 0), result.Coordinate);
        Assert.Contains(result.Face, new[] { VoxelFace.Left, VoxelFace.Right });
    }

    private static VoxelRenderTransform Resolve(
        VoxelCameraState camera,
        VoxelModelRotationState rotation) =>
        new VoxelRenderTransformResolver().Resolve(
            camera,
            rotation,
            new VoxelDimensions(1, 1, 1),
            new PixelRenderSettings(32, 32, 8, new Rgba32Color(0, 0, 0, 0)));

    private static VoxelDocument SingleVoxelDocument() =>
        new(new SingleVoxelStorage());

    private sealed class SingleVoxelStorage : IVoxelStorage
    {
        private static readonly VoxelCell Cell = VoxelCell.CreateUniform(new Rgba32Color(255, 255, 255, 255));

        public VoxelDimensions Dimensions => new(1, 1, 1);

        public int OccupiedCount => 1;

        public bool TryGetCell(VoxelCoordinate coordinate, out VoxelCell? cell)
        {
            if (coordinate == new VoxelCoordinate(0, 0, 0))
            {
                cell = Cell;
                return true;
            }

            cell = null;
            return false;
        }

        public IEnumerable<VoxelEntry> GetOccupiedCells() =>
            [new VoxelEntry(new VoxelCoordinate(0, 0, 0), Cell)];
    }
}
