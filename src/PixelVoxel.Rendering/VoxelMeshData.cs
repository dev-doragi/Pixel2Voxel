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
    Vector3 Normal);

/// <summary>
/// Stores a disposable render cache derived from a voxel document.
/// </summary>
public sealed class VoxelMeshData
{
    internal VoxelMeshData(
        VoxelDimensions dimensions,
        VoxelMeshVertex[] vertices,
        uint[] indices,
        Vector3[] faceNormals)
    {
        Dimensions = dimensions;
        Vertices = vertices;
        Indices = indices;
        FaceNormals = faceNormals;
    }

    /// <summary>Gets the source model dimensions used to build the cache.</summary>
    public VoxelDimensions Dimensions { get; }

    /// <summary>Gets four independent vertices per exposed face.</summary>
    public ReadOnlyMemory<VoxelMeshVertex> Vertices { get; }

    /// <summary>Gets six triangle indices per exposed face.</summary>
    public ReadOnlyMemory<uint> Indices { get; }

    /// <summary>Gets one outward normal per exposed face.</summary>
    public ReadOnlyMemory<Vector3> FaceNormals { get; }

    /// <summary>Gets the number of exposed faces in the cache.</summary>
    public int ExposedFaceCount => FaceNormals.Length;
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
