using System.Numerics;
using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

/// <summary>Stores one colored surface vertex in model space.</summary>
/// <param name="Position">The model-space vertex position.</param>
/// <param name="Color">The exact source face color.</param>
/// <param name="Normal">The outward model-space face normal.</param>
public readonly record struct VoxelMeshVertex(
    Vector3 Position,
    Rgba32Color Color,
    Vector3 Normal,
    float EditorMask = 0f);

/// <summary>Identifies the source voxel and direction represented by one mesh face.</summary>
public readonly record struct VoxelSurfaceIdentity(VoxelCoordinate Coordinate, VoxelFace Face);

/// <summary>
/// Stores a disposable render cache derived from a voxel document.
/// </summary>
public sealed class VoxelMeshData
{
    private readonly VoxelMeshVertex[] _vertices;
    private readonly uint[] _indices;
    private readonly Vector3[] _faceNormals;
    private readonly VoxelSurfaceIdentity[] _faces;

    internal VoxelMeshData(
        VoxelDimensions dimensions,
        VoxelMeshVertex[] vertices,
        uint[] indices,
        Vector3[] faceNormals,
        VoxelSurfaceIdentity[] faces)
    {
        Dimensions = dimensions;
        _vertices = vertices;
        _indices = indices;
        _faceNormals = faceNormals;
        _faces = faces;
    }

    /// <summary>Gets the source model dimensions used to build the cache.</summary>
    public VoxelDimensions Dimensions { get; }

    /// <summary>Gets four independent vertices per exposed face.</summary>
    public ReadOnlyMemory<VoxelMeshVertex> Vertices => _vertices;

    /// <summary>Gets six triangle indices per exposed face.</summary>
    public ReadOnlyMemory<uint> Indices => _indices;

    /// <summary>Gets one outward normal per exposed face.</summary>
    public ReadOnlyMemory<Vector3> FaceNormals => _faceNormals;

    /// <summary>Gets source voxel identities in the same order as exposed faces.</summary>
    public ReadOnlyMemory<VoxelSurfaceIdentity> Faces => _faces;

    /// <summary>Gets the number of exposed faces in the cache.</summary>
    public int ExposedFaceCount => FaceNormals.Length;

    /// <summary>Creates a viewport-only mesh carrying selection and hover masks.</summary>
    public VoxelMeshData WithEditorOverlay(
        VoxelSelectionBox? selection,
        VoxelPickResult? hover)
    {
        VoxelMeshVertex[] vertices = Vertices.ToArray();
        ReadOnlySpan<VoxelSurfaceIdentity> faces = Faces.Span;
        for (int faceIndex = 0; faceIndex < faces.Length; faceIndex++)
        {
            VoxelSurfaceIdentity identity = faces[faceIndex];
            float mask = hover is not null &&
                hover.Coordinate == identity.Coordinate &&
                hover.Face == identity.Face
                ? 1f
                : selection?.Contains(identity.Coordinate) == true ? 0.5f : 0f;
            for (int vertexIndex = 0; vertexIndex < 4; vertexIndex++)
            {
                int index = (faceIndex * 4) + vertexIndex;
                vertices[index] = vertices[index] with { EditorMask = mask };
            }
        }

        return new VoxelMeshData(
            Dimensions,
            vertices,
            _indices,
            _faceNormals,
            _faces);
    }
}

/// <summary>Reports whether a derived surface cache is within the rendering limit.</summary>
public sealed record VoxelMeshBuildResult(
    VoxelMeshData? Mesh,
    int ActualExposedFaceCount,
    string? Diagnostic)
{
    /// <summary>Gets whether a render cache was produced.</summary>
    public bool IsSuccess => Mesh is not null;
}
