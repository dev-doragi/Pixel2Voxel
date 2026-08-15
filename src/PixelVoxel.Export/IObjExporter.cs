using PixelVoxel.Rendering;

namespace PixelVoxel.Export;

public sealed record ObjExportRequest(string DestinationDirectory, string BaseName, VoxelMeshData Mesh);

public sealed record ObjExportResult(
    string ObjPath,
    string MaterialPath,
    string PalettePath,
    string MetadataPath,
    string UnityImporterPath,
    int FaceCount,
    int PaletteColorCount);

/// <summary>Exports a Unity-oriented, pixel-textured surface package.</summary>
public interface IObjExporter
{
    Task<ObjExportResult> ExportAsync(ObjExportRequest request, CancellationToken cancellationToken = default);
}
