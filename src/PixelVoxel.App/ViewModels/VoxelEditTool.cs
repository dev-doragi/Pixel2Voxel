namespace PixelVoxel.App.ViewModels;

/// <summary>Identifies the active left-button viewport editing behavior.</summary>
public enum VoxelEditTool
{
    /// <summary>Disables editing so the viewport can be used only for navigation.</summary>
    View,

    /// <summary>Adds a voxel next to the picked surface.</summary>
    Add,

    /// <summary>Removes picked voxels.</summary>
    Erase,

    /// <summary>Changes picked face colors.</summary>
    Paint,

    /// <summary>Samples the exact source color of a picked exposed face.</summary>
    Eyedropper,

    /// <summary>Defines an axis-aligned selection from two picked voxels.</summary>
    Select,
}
