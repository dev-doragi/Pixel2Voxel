using System.Numerics;
using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

/// <summary>
/// Rasterizes the derived voxel surface into the deterministic reference framebuffer.
/// </summary>
public sealed class CpuVoxelRasterizer
{
    private const int SubpixelBits = 8;
    private const long SubpixelScale = 1L << SubpixelBits;
    private const long SubpixelHalf = SubpixelScale / 2;

    /// <summary>Renders a surface cache with per-pixel depth testing and exact face colors.</summary>
    public PixelFramebuffer Render(
        VoxelMeshData mesh,
        VoxelRenderTransform transform,
        PixelRenderSettings settings) =>
        RenderCore(mesh, transform, settings, useStablePixelEdges: false);

    internal PixelFramebuffer RenderPixelStable(
        VoxelMeshData mesh,
        VoxelRenderTransform transform,
        PixelRenderSettings settings) =>
        RenderCore(mesh, transform, settings, useStablePixelEdges: true);

    internal PixelRasterSurface RenderPixelSurface(
        VoxelMeshData mesh,
        VoxelRenderTransform transform,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(transform);
        PixelRasterSurface surface = new(width, height);
        ReadOnlySpan<VoxelMeshVertex> vertices = mesh.Vertices.Span;
        ReadOnlySpan<Vector3> normals = mesh.FaceNormals.Span;

        for (int faceIndex = 0; faceIndex < mesh.ExposedFaceCount; faceIndex++)
        {
            Vector3 normal = normals[faceIndex];
            if (!transform.IsFrontFacing(normal))
            {
                continue;
            }

            int offset = faceIndex * 4;
            ScreenVertex first = Project(vertices[offset], transform);
            ScreenVertex second = Project(vertices[offset + 1], transform);
            ScreenVertex third = Project(vertices[offset + 2], transform);
            ScreenVertex fourth = Project(vertices[offset + 3], transform);
            Rgba32Color color = vertices[offset].Color;
            Vector3 worldNormal = transform.TransformNormalToWorld(normal);
            RasterizeTriangleFixed(
                first, second, third, color, surface.Colors, surface.Depth,
                width, height, worldNormal, surface.Normals, surface.Coverage,
                vertices[offset].EditorMask, surface.EditorMask);
            RasterizeTriangleFixed(
                first, third, fourth, color, surface.Colors, surface.Depth,
                width, height, worldNormal, surface.Normals, surface.Coverage,
                vertices[offset].EditorMask, surface.EditorMask);
        }

        return surface;
    }

    private static PixelFramebuffer RenderCore(
        VoxelMeshData mesh,
        VoxelRenderTransform transform,
        PixelRenderSettings settings,
        bool useStablePixelEdges)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(settings);

        Rgba32Color[] pixels = Enumerable.Repeat(
            settings.Background,
            checked(settings.Width * settings.Height)).ToArray();
        float[] depthBuffer = Enumerable.Repeat(float.NegativeInfinity, pixels.Length).ToArray();
        ReadOnlySpan<VoxelMeshVertex> vertices = mesh.Vertices.Span;
        ReadOnlySpan<Vector3> normals = mesh.FaceNormals.Span;

        for (int faceIndex = 0; faceIndex < mesh.ExposedFaceCount; faceIndex++)
        {
            if (!transform.IsFrontFacing(normals[faceIndex]))
            {
                continue;
            }

            int offset = faceIndex * 4;
            ScreenVertex first = Project(vertices[offset], transform);
            ScreenVertex second = Project(vertices[offset + 1], transform);
            ScreenVertex third = Project(vertices[offset + 2], transform);
            ScreenVertex fourth = Project(vertices[offset + 3], transform);
            Rgba32Color color = vertices[offset].Color;

            if (useStablePixelEdges)
            {
                RasterizeTriangleFixed(
                    first,
                    second,
                    third,
                    color,
                    pixels,
                    depthBuffer,
                    settings.Width,
                    settings.Height,
                    normals[faceIndex],
                    null,
                    null);
                RasterizeTriangleFixed(
                    first,
                    third,
                    fourth,
                    color,
                    pixels,
                    depthBuffer,
                    settings.Width,
                    settings.Height,
                    normals[faceIndex],
                    null,
                    null);
            }
            else
            {
                RasterizeTriangle(first, second, third, color, pixels, depthBuffer, settings.Width, settings.Height);
                RasterizeTriangle(first, third, fourth, color, pixels, depthBuffer, settings.Width, settings.Height);
            }
        }

        return new PixelFramebuffer(settings.Width, settings.Height, pixels);
    }

    private static ScreenVertex Project(
        VoxelMeshVertex vertex,
        VoxelRenderTransform transform)
    {
        Vector3 projected = transform.ProjectToScreen(vertex.Position);
        return new ScreenVertex(projected.X, projected.Y, projected.Z);
    }

    private static void RasterizeTriangle(
        ScreenVertex first,
        ScreenVertex second,
        ScreenVertex third,
        Rgba32Color color,
        Rgba32Color[] pixels,
        float[] depthBuffer,
        int width,
        int height,
        Vector3 normal = default,
        Vector3[]? normalBuffer = null,
        bool[]? coverageBuffer = null,
        float editorMask = 0f,
        float[]? editorMaskBuffer = null)
    {
        float area = Edge(first, second, third.X, third.Y);
        if (MathF.Abs(area) < 0.0001f)
        {
            return;
        }

        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(first.X, MathF.Min(second.X, third.X))));
        int maxX = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(first.X, MathF.Max(second.X, third.X))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(first.Y, MathF.Min(second.Y, third.Y))));
        int maxY = Math.Min(height - 1, (int)MathF.Ceiling(MathF.Max(first.Y, MathF.Max(second.Y, third.Y))));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float sampleX = x + 0.5f;
                float sampleY = y + 0.5f;
                float firstWeight = Edge(second, third, sampleX, sampleY) / area;
                float secondWeight = Edge(third, first, sampleX, sampleY) / area;
                float thirdWeight = Edge(first, second, sampleX, sampleY) / area;

                if (firstWeight < -0.0001f || secondWeight < -0.0001f || thirdWeight < -0.0001f)
                {
                    continue;
                }

                float depth =
                    (first.Z * firstWeight) +
                    (second.Z * secondWeight) +
                    (third.Z * thirdWeight);
                int pixelIndex = (y * width) + x;

                if (depth > depthBuffer[pixelIndex])
                {
                    depthBuffer[pixelIndex] = depth;
                    pixels[pixelIndex] = color;
                    if (normalBuffer is not null)
                    {
                        normalBuffer[pixelIndex] = normal;
                    }

                    if (coverageBuffer is not null)
                    {
                        coverageBuffer[pixelIndex] = true;
                    }

                    if (editorMaskBuffer is not null)
                    {
                        editorMaskBuffer[pixelIndex] = editorMask;
                    }
                }
            }
        }
    }

    private static void RasterizeTriangleFixed(
        ScreenVertex first,
        ScreenVertex second,
        ScreenVertex third,
        Rgba32Color color,
        Rgba32Color[] pixels,
        float[] depthBuffer,
        int width,
        int height,
        Vector3 normal = default,
        Vector3[]? normalBuffer = null,
        bool[]? coverageBuffer = null,
        float editorMask = 0f,
        float[]? editorMaskBuffer = null)
    {
        FixedScreenVertex fixedFirst = ToFixed(first);
        FixedScreenVertex fixedSecond = ToFixed(second);
        FixedScreenVertex fixedThird = ToFixed(third);
        long area = FixedEdge(fixedFirst, fixedSecond, fixedThird.X, fixedThird.Y);

        if (area == 0)
        {
            return;
        }

        if (area < 0)
        {
            (fixedSecond, fixedThird) = (fixedThird, fixedSecond);
            area = -area;
        }

        int minX = Math.Max(0, FloorPixel(Math.Min(fixedFirst.X, Math.Min(fixedSecond.X, fixedThird.X))));
        int maxX = Math.Min(width - 1, CeilingPixel(Math.Max(fixedFirst.X, Math.Max(fixedSecond.X, fixedThird.X))));
        int minY = Math.Max(0, FloorPixel(Math.Min(fixedFirst.Y, Math.Min(fixedSecond.Y, fixedThird.Y))));
        int maxY = Math.Min(height - 1, CeilingPixel(Math.Max(fixedFirst.Y, Math.Max(fixedSecond.Y, fixedThird.Y))));
        bool firstEdgeTopLeft = IsTopLeft(fixedSecond, fixedThird);
        bool secondEdgeTopLeft = IsTopLeft(fixedThird, fixedFirst);
        bool thirdEdgeTopLeft = IsTopLeft(fixedFirst, fixedSecond);
        double inverseArea = 1d / area;

        for (int y = minY; y <= maxY; y++)
        {
            long sampleY = checked(((long)y * SubpixelScale) + SubpixelHalf);

            for (int x = minX; x <= maxX; x++)
            {
                long sampleX = checked(((long)x * SubpixelScale) + SubpixelHalf);
                long firstWeight = FixedEdge(fixedSecond, fixedThird, sampleX, sampleY);
                long secondWeight = FixedEdge(fixedThird, fixedFirst, sampleX, sampleY);
                long thirdWeight = FixedEdge(fixedFirst, fixedSecond, sampleX, sampleY);

                if (!CoversSample(firstWeight, firstEdgeTopLeft) ||
                    !CoversSample(secondWeight, secondEdgeTopLeft) ||
                    !CoversSample(thirdWeight, thirdEdgeTopLeft))
                {
                    continue;
                }

                float depth = (float)(
                    (fixedFirst.Z * firstWeight * inverseArea) +
                    (fixedSecond.Z * secondWeight * inverseArea) +
                    (fixedThird.Z * thirdWeight * inverseArea));
                int pixelIndex = (y * width) + x;

                if (depth > depthBuffer[pixelIndex])
                {
                    depthBuffer[pixelIndex] = depth;
                    pixels[pixelIndex] = color;
                    if (normalBuffer is not null)
                    {
                        normalBuffer[pixelIndex] = normal;
                    }

                    if (coverageBuffer is not null)
                    {
                        coverageBuffer[pixelIndex] = true;
                    }


                    if (editorMaskBuffer is not null)
                    {
                        editorMaskBuffer[pixelIndex] = editorMask;
                    }
                }
            }
        }
    }

    private static FixedScreenVertex ToFixed(ScreenVertex vertex) =>
        new(
            checked((long)Math.Round((double)vertex.X * SubpixelScale, MidpointRounding.AwayFromZero)),
            checked((long)Math.Round((double)vertex.Y * SubpixelScale, MidpointRounding.AwayFromZero)),
            vertex.Z);

    private static int FloorPixel(long coordinate) =>
        checked((int)Math.Floor(coordinate / (double)SubpixelScale));

    private static int CeilingPixel(long coordinate) =>
        checked((int)Math.Ceiling(coordinate / (double)SubpixelScale));

    private static bool CoversSample(long edgeValue, bool isTopLeft) =>
        edgeValue > 0 || (edgeValue == 0 && isTopLeft);

    private static bool IsTopLeft(FixedScreenVertex first, FixedScreenVertex second)
    {
        long deltaX = second.X - first.X;
        long deltaY = second.Y - first.Y;
        return deltaY < 0 || (deltaY == 0 && deltaX > 0);
    }

    private static long FixedEdge(
        FixedScreenVertex first,
        FixedScreenVertex second,
        long x,
        long y) =>
        checked(
            ((second.X - first.X) * (y - first.Y)) -
            ((second.Y - first.Y) * (x - first.X)));

    private static float Edge(ScreenVertex first, ScreenVertex second, float x, float y) =>
        ((x - first.X) * (second.Y - first.Y)) -
        ((y - first.Y) * (second.X - first.X));

    private readonly record struct ScreenVertex(float X, float Y, float Z);

    private readonly record struct FixedScreenVertex(long X, long Y, float Z);
}
