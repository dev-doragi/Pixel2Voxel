using System.Globalization;
using System.Text;
using System.Text.Json;
using PixelVoxel.Core;
using PixelVoxel.Imaging;
using PixelVoxel.Rendering;

namespace PixelVoxel.Export;

/// <summary>Writes OBJ/MTL/palette plus a narrowly-scoped Unity AssetPostprocessor.</summary>
public sealed class UnityObjExporter : IObjExporter
{
    private readonly PngPixelWriter _pngWriter;
    public UnityObjExporter(PngPixelWriter pngWriter) => _pngWriter = pngWriter;

    public async Task<ObjExportResult> ExportAsync(ObjExportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BaseName);
        ArgumentNullException.ThrowIfNull(request.Mesh);
        string baseName = SanitizeBaseName(request.BaseName);
        string destination = Path.GetFullPath(request.DestinationDirectory);
        Directory.CreateDirectory(destination);
        string staging = Path.Combine(destination, $".pixelvoxel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            string objName = baseName + ".obj";
            string mtlName = baseName + ".mtl";
            string paletteName = baseName + "_palette.png";
            string metadataName = baseName + ".pixelvoxel.json";
            Rgba32Color[] palette = request.Mesh.Vertices.ToArray()
                .Where((_, index) => index % 4 == 0)
                .Select(vertex => vertex.Color)
                .Distinct()
                .OrderBy(color => color.Red).ThenBy(color => color.Green).ThenBy(color => color.Blue)
                .ToArray();
            if (palette.Length == 0) throw new InvalidDataException("The mesh has no exposed colors.");
            int paletteWidth = Math.Min(1024, palette.Length);
            int paletteHeight = checked((palette.Length + paletteWidth - 1) / paletteWidth);
            Rgba32Color[] atlasPixels = new Rgba32Color[checked(paletteWidth * paletteHeight)];
            palette.CopyTo(atlasPixels, 0);

            await _pngWriter.WriteAsync(Path.Combine(staging, paletteName), paletteWidth, paletteHeight, atlasPixels, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(staging, mtlName), BuildMtl(paletteName), cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(staging, objName), BuildObj(request.Mesh, mtlName, palette, paletteWidth, paletteHeight), cancellationToken);
            object metadata = new { format = "PixelVoxelUnity", version = 1, coordinateSystem = "XYZ-RightUpFront-v1", voxelScale = 1, palette = paletteName };
            await File.WriteAllTextAsync(Path.Combine(staging, metadataName), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            string editor = Path.Combine(staging, "Editor");
            Directory.CreateDirectory(editor);
            await File.WriteAllTextAsync(Path.Combine(editor, "PixelVoxelAssetPostprocessor.cs"), UnityImporterSource, cancellationToken);

            CommitStagedFiles(staging, destination, cancellationToken);

            return new ObjExportResult(
                Path.Combine(destination, objName), Path.Combine(destination, mtlName),
                Path.Combine(destination, paletteName), Path.Combine(destination, metadataName),
                Path.Combine(destination, "Editor", "PixelVoxelAssetPostprocessor.cs"),
                request.Mesh.ExposedFaceCount, palette.Length);
        }
        finally
        {
            try { Directory.Delete(staging, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static string BuildObj(VoxelMeshData mesh, string mtlName, IReadOnlyList<Rgba32Color> palette, int paletteWidth, int paletteHeight)
    {
        StringBuilder output = new();
        output.AppendLine("# Pixel Voxel Unity surface export");
        output.AppendLine($"mtllib {mtlName}");
        output.AppendLine("o PixelVoxel");
        float centerX = mesh.Dimensions.Width / 2f;
        float centerZ = mesh.Dimensions.Depth / 2f;
        foreach (VoxelMeshVertex vertex in mesh.Vertices.Span)
            output.AppendLine(FormattableString.Invariant($"v {vertex.Position.X - centerX:0.######} {vertex.Position.Y:0.######} {vertex.Position.Z - centerZ:0.######}"));
        for (int index = 0; index < palette.Count; index++)
        {
            float u = ((index % paletteWidth) + 0.5f) / paletteWidth;
            float v = 1f - (((index / paletteWidth) + 0.5f) / paletteHeight);
            output.AppendLine(FormattableString.Invariant($"vt {u:0.########} {v:0.########}"));
        }
        foreach (System.Numerics.Vector3 normal in mesh.FaceNormals.Span)
            output.AppendLine(FormattableString.Invariant($"vn {normal.X:0.######} {normal.Y:0.######} {normal.Z:0.######}"));
        output.AppendLine("usemtl PixelVoxelMaterial");
        ReadOnlySpan<VoxelMeshVertex> vertices = mesh.Vertices.Span;
        for (int face = 0; face < mesh.ExposedFaceCount; face++)
        {
            int vertex = face * 4 + 1;
            int uv = IndexOf(palette, vertices[face * 4].Color) + 1;
            int normal = face + 1;
            output.AppendLine($"f {vertex}/{uv}/{normal} {vertex + 1}/{uv}/{normal} {vertex + 2}/{uv}/{normal} {vertex + 3}/{uv}/{normal}");
        }
        return output.ToString();
    }

    private static void CommitStagedFiles(string staging, string destination, CancellationToken cancellationToken)
    {
        string[] files = Directory.GetFiles(staging, "*", SearchOption.AllDirectories);
        string backup = Path.Combine(staging, ".backup");
        List<(string Target, string Backup)> backups = [];
        List<string> committed = [];
        try
        {
            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relative = Path.GetRelativePath(staging, file);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target))
                {
                    string saved = Path.Combine(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                    File.Move(target, saved);
                    backups.Add((target, saved));
                }
                File.Move(file, target);
                committed.Add(target);
            }
        }
        catch
        {
            foreach (string target in committed)
                try { if (File.Exists(target)) File.Delete(target); } catch (IOException) { }
            foreach ((string target, string saved) in backups)
                if (File.Exists(saved)) File.Move(saved, target, overwrite: true);
            throw;
        }

        foreach ((_, string saved) in backups)
            try { if (File.Exists(saved)) File.Delete(saved); } catch (IOException) { }
    }

    private static int IndexOf(IReadOnlyList<Rgba32Color> colors, Rgba32Color value)
    {
        for (int i = 0; i < colors.Count; i++) if (colors[i] == value) return i;
        throw new InvalidOperationException("Face color is missing from the palette.");
    }

    private static string BuildMtl(string paletteName) => $"""
# Pixel Voxel material
newmtl PixelVoxelMaterial
Ka 1.000 1.000 1.000
Kd 1.000 1.000 1.000
Ks 0.000 0.000 0.000
illum 1
map_Kd {paletteName}
""";

    private static string SanitizeBaseName(string name)
    {
        string result = string.Concat(Path.GetFileNameWithoutExtension(name).Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return string.IsNullOrWhiteSpace(result) ? "pixel-voxel-model" : result;
    }

    internal const string UnityImporterSource = """
// Generated by Pixel Voxel. Applies only to textures with a sibling .pixelvoxel.json marker.
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class PixelVoxelAssetPostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.EndsWith("_palette.png", System.StringComparison.OrdinalIgnoreCase)) return;
        string marker = assetPath.Substring(0, assetPath.Length - "_palette.png".Length) + ".pixelvoxel.json";
        if (!File.Exists(marker)) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
    }
}
""";
}
