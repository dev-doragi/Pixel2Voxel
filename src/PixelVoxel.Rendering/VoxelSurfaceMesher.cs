using System.Numerics;
using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

/// <summary>
/// Builds a non-persisted colored cache containing only faces exposed to empty space.
/// </summary>
public sealed class VoxelSurfaceMesher
{
    /// <summary>The largest exposed surface accepted by the rendering cache.</summary>
    public const int MaximumExposedFaceCount = 500_000;

    /// <summary>Builds a derived surface cache in two passes.</summary>
    public VoxelMeshBuildResult Build(
        VoxelDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        int faceCount = CountExposedFaces(document, cancellationToken);
        if (faceCount > MaximumExposedFaceCount)
        {
            return new VoxelMeshBuildResult(
                null,
                faceCount,
                $"The model has {faceCount:N0} exposed faces; " +
                $"the render-cache limit is {MaximumExposedFaceCount:N0}.");
        }

        VoxelMeshVertex[] vertices = new VoxelMeshVertex[checked(faceCount * 4)];
        uint[] indices = new uint[checked(faceCount * 6)];
        Vector3[] normals = new Vector3[faceCount];
        VoxelSurfaceIdentity[] identities = new VoxelSurfaceIdentity[faceCount];
        int faceIndex = 0;

        foreach (VoxelEntry entry in document.Storage.GetOccupiedCells())
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (FaceDefinition face in Faces)
            {
                if (HasNeighbor(document, entry.Coordinate, face.Neighbor))
                {
                    continue;
                }

                if (!entry.Cell.TryGetColor(face.Face, out Rgba32Color color))
                {
                    throw new InvalidOperationException(
                        $"The exposed {face.Face} face at {entry.Coordinate} has no matching source color.");
                }

                int vertexOffset = faceIndex * 4;
                for (int vertexIndex = 0; vertexIndex < 4; vertexIndex++)
                {
                    vertices[vertexOffset + vertexIndex] = new VoxelMeshVertex(
                        new Vector3(
                            entry.Coordinate.X + face.Vertices[vertexIndex].X,
                            entry.Coordinate.Y + face.Vertices[vertexIndex].Y,
                            entry.Coordinate.Z + face.Vertices[vertexIndex].Z),
                        color,
                        face.Normal);
                }

                int indexOffset = faceIndex * 6;
                uint first = checked((uint)vertexOffset);
                indices[indexOffset] = first;
                indices[indexOffset + 1] = first + 1;
                indices[indexOffset + 2] = first + 2;
                indices[indexOffset + 3] = first;
                indices[indexOffset + 4] = first + 2;
                indices[indexOffset + 5] = first + 3;
                normals[faceIndex] = face.Normal;
                identities[faceIndex] = new VoxelSurfaceIdentity(entry.Coordinate, face.Face);
                faceIndex++;
            }
        }

        return new VoxelMeshBuildResult(
            new VoxelMeshData(document.Storage.Dimensions, vertices, indices, normals, identities),
            faceCount,
            null);
    }

    private static int CountExposedFaces(
        VoxelDocument document,
        CancellationToken cancellationToken)
    {
        int count = 0;

        foreach (VoxelEntry entry in document.Storage.GetOccupiedCells())
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (FaceDefinition face in Faces)
            {
                if (!HasNeighbor(document, entry.Coordinate, face.Neighbor))
                {
                    count = checked(count + 1);
                }
            }
        }

        return count;
    }

    private static bool HasNeighbor(
        VoxelDocument document,
        VoxelCoordinate coordinate,
        VoxelCoordinate offset) =>
        document.Storage.TryGetCell(
            new VoxelCoordinate(
                coordinate.X + offset.X,
                coordinate.Y + offset.Y,
                coordinate.Z + offset.Z),
            out _);

    private static readonly FaceDefinition[] Faces =
    [
        new(VoxelFace.Front, new(0, 0, 1), Vector3.UnitZ,
            [new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)]),
        new(VoxelFace.Back, new(0, 0, -1), -Vector3.UnitZ,
            [new(1, 0, 0), new(0, 0, 0), new(0, 1, 0), new(1, 1, 0)]),
        new(VoxelFace.Right, new(1, 0, 0), Vector3.UnitX,
            [new(1, 0, 1), new(1, 0, 0), new(1, 1, 0), new(1, 1, 1)]),
        new(VoxelFace.Left, new(-1, 0, 0), -Vector3.UnitX,
            [new(0, 0, 0), new(0, 0, 1), new(0, 1, 1), new(0, 1, 0)]),
        new(VoxelFace.Top, new(0, 1, 0), Vector3.UnitY,
            [new(0, 1, 1), new(1, 1, 1), new(1, 1, 0), new(0, 1, 0)]),
        new(VoxelFace.Bottom, new(0, -1, 0), -Vector3.UnitY,
            [new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1)]),
    ];

    private sealed record FaceDefinition(
        VoxelFace Face,
        VoxelCoordinate Neighbor,
        Vector3 Normal,
        Vector3[] Vertices);
}
