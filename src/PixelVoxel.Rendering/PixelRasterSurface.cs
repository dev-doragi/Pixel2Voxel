using System.Numerics;
using PixelVoxel.Core;

namespace PixelVoxel.Rendering;

internal sealed class PixelRasterSurface
{
    public PixelRasterSurface(int width, int height)
    {
        Width = width;
        Height = height;
        int count = checked(width * height);
        Colors = new Rgba32Color[count];
        Depth = Enumerable.Repeat(float.NegativeInfinity, count).ToArray();
        Normals = new Vector3[count];
        Coverage = new bool[count];
    }

    public int Width { get; }

    public int Height { get; }

    public Rgba32Color[] Colors { get; }

    public float[] Depth { get; }

    public Vector3[] Normals { get; }

    public bool[] Coverage { get; }
}
