namespace PixelVoxel.Core;

/// <summary>
/// Stores an RGBA color independently of any imaging framework.
/// </summary>
/// <param name="Red">The red channel.</param>
/// <param name="Green">The green channel.</param>
/// <param name="Blue">The blue channel.</param>
/// <param name="Alpha">The alpha channel.</param>
public readonly record struct Rgba32Color(byte Red, byte Green, byte Blue, byte Alpha);

