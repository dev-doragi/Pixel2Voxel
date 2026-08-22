namespace PixelVoxel.Core;

/// <summary>Describes a source-mask pixel whose occupancy disagrees with the reconstructed volume.</summary>
public sealed record VoxelReprojectionConflict(
    VoxelFace Face,
    int X,
    int Y,
    bool SourceOccupied,
    bool ReprojectedOccupied);

/// <summary>Contains deterministic silhouette conflicts for all supplied source views.</summary>
public sealed class VoxelReprojectionAnalysis
{
    internal VoxelReprojectionAnalysis(IReadOnlyList<VoxelReprojectionConflict> conflicts) =>
        Conflicts = conflicts;

    public IReadOnlyList<VoxelReprojectionConflict> Conflicts { get; }

    public int ConflictCount => Conflicts.Count;

    public int GetConflictCount(VoxelFace face) => Conflicts.Count(conflict => conflict.Face == face);
}

/// <summary>Reprojects a reconstructed document and compares its silhouette with each source mask.</summary>
public sealed class VoxelReprojectionAnalyzer
{
    public VoxelReprojectionAnalysis Analyze(
        OrthographicViewSet views,
        VoxelDocument document,
        VoxelReconstructionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(document);
        options ??= VoxelReconstructionOptions.UnitFallback;
        options.Validate();
        VisualHullVoxelReconstructor.ValidateCommonCanvas(views);
        VisualHullVoxelReconstructor.ReconstructionSpace space =
            VisualHullVoxelReconstructor.ResolveSpace(views, options);

        List<VoxelReprojectionConflict> conflicts = [];
        foreach ((VoxelFace face, OrthographicImage image) in views.Views.OrderBy(pair => pair.Key))
        {
            bool[] projected = new bool[checked(image.Width * image.Height)];
            foreach (VoxelEntry entry in document.Storage.GetOccupiedCells())
            {
                (int x, int y) = VisualHullVoxelReconstructor.ProjectToImage(face, image, entry.Coordinate, space);
                if ((uint)x < (uint)image.Width && (uint)y < (uint)image.Height)
                {
                    projected[(y * image.Width) + x] = true;
                }
            }

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    bool sourceOccupied = image.GetPixel(x, y).Alpha > 0;
                    bool reprojectedOccupied = projected[(y * image.Width) + x];
                    if (sourceOccupied != reprojectedOccupied)
                    {
                        conflicts.Add(new VoxelReprojectionConflict(
                            face, x, y, sourceOccupied, reprojectedOccupied));
                    }
                }
            }
        }

        return new VoxelReprojectionAnalysis(conflicts);
    }
}
