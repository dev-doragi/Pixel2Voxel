namespace PixelVoxel.App.ViewModels;

public enum WorkspaceMode
{
    Import,
    Edit,
    Animate,
    Export,
}

public enum ImportWorkflowStep
{
    Source,
    MapAndAlign,
    Validate,
    Reconstruct,
}

public enum ExportWorkflowMode
{
    CurrentView,
    DirectionSheet,
    AnimatedGif,
    AnimationSheet,
    TrimmedAtlas,
    UnityObj,
}
