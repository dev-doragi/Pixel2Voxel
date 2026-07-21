namespace PixelVoxel.App.ViewModels;

/// <summary>Identifies the active left-button viewport editing behavior.</summary>
public enum VoxelEditTool
{
    /// <summary>Adds a voxel next to the picked surface.</summary>
    Add,

    /// <summary>Removes picked voxels.</summary>
    Erase,

    /// <summary>Changes picked face colors.</summary>
    Paint,

    /// <summary>Defines an axis-aligned selection from two picked voxels.</summary>
    Select,
}
