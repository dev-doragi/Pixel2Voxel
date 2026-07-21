using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using PixelVoxel.Core;
using PixelVoxel.Imaging;

namespace PixelVoxel.App.ViewModels;

/// <summary>Represents one fixed target-face card in the import alignment workspace.</summary>
public sealed class ImportFaceSlotViewModel : INotifyPropertyChanged, IDisposable
{
    private int _sourceSlotIndex = -1;
    private string _sourceName = "Unassigned";
    private WriteableBitmap? _previewBitmap;
    private string _diagnosticText = "No source assigned";
    private ImportDiagnosticSeverity? _diagnosticSeverity;
    private bool _flipHorizontal;
    private bool _flipVertical;
    private int _offsetX;
    private int _offsetY;
    private bool _suppressChanges;

    /// <summary>Initializes one target face card.</summary>
    public ImportFaceSlotViewModel(VoxelFace targetFace)
    {
        TargetFace = targetFace;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when a Flip or Offset value changes.</summary>
    public event EventHandler? AlignmentChanged;

    /// <summary>Gets the fixed model face represented by this card.</summary>
    public VoxelFace TargetFace { get; }

    /// <summary>Gets the assigned source slot index, or -1.</summary>
    public int SourceSlotIndex
    {
        get => _sourceSlotIndex;
        private set
        {
            if (SetField(ref _sourceSlotIndex, value)) OnPropertyChanged(nameof(HasSource));
        }
    }

    /// <summary>Gets whether this target has a source image.</summary>
    public bool HasSource => SourceSlotIndex >= 0;

    /// <summary>Gets the assigned source display name.</summary>
    public string SourceName
    {
        get => _sourceName;
        private set => SetField(ref _sourceName, value);
    }

    /// <summary>Gets the transformed nearest-neighbor preview.</summary>
    public WriteableBitmap? PreviewBitmap
    {
        get => _previewBitmap;
        private set
        {
            if (ReferenceEquals(_previewBitmap, value)) return;
            WriteableBitmap? previous = _previewBitmap;
            _previewBitmap = value;
            OnPropertyChanged();
            previous?.Dispose();
        }
    }

    /// <summary>Gets the most relevant error or warning for this target.</summary>
    public string DiagnosticText
    {
        get => _diagnosticText;
        private set => SetField(ref _diagnosticText, value);
    }

    public ImportDiagnosticSeverity? DiagnosticSeverity
    {
        get => _diagnosticSeverity;
        private set
        {
            if (!SetField(ref _diagnosticSeverity, value)) return;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasWarning));
        }
    }

    public bool HasError => DiagnosticSeverity == ImportDiagnosticSeverity.Error;

    public bool HasWarning => DiagnosticSeverity == ImportDiagnosticSeverity.Warning;

    public bool FlipHorizontal
    {
        get => _flipHorizontal;
        set { if (SetField(ref _flipHorizontal, value)) RaiseAlignmentChanged(); }
    }

    public bool FlipVertical
    {
        get => _flipVertical;
        set { if (SetField(ref _flipVertical, value)) RaiseAlignmentChanged(); }
    }

    public int OffsetX
    {
        get => _offsetX;
        set { if (SetField(ref _offsetX, value)) RaiseAlignmentChanged(); }
    }

    public int OffsetY
    {
        get => _offsetY;
        set { if (SetField(ref _offsetY, value)) RaiseAlignmentChanged(); }
    }

    /// <summary>Assigns a source without firing an alignment refresh.</summary>
    public void AssignSource(int sourceSlotIndex, string sourceName)
    {
        SourceSlotIndex = sourceSlotIndex;
        SourceName = sourceName;
    }

    /// <summary>Clears the source without changing this face's adjustment controls.</summary>
    public void ClearSource()
    {
        SourceSlotIndex = -1;
        SourceName = "Unassigned";
        PreviewBitmap = null;
        DiagnosticText = "No source assigned";
        DiagnosticSeverity = ImportDiagnosticSeverity.Error;
    }

    /// <summary>Updates the generated preview and diagnostic label.</summary>
    public void SetPreview(
        WriteableBitmap? bitmap,
        string diagnosticText,
        ImportDiagnosticSeverity? diagnosticSeverity = null)
    {
        PreviewBitmap = bitmap;
        DiagnosticText = diagnosticText;
        DiagnosticSeverity = diagnosticSeverity;
    }

    /// <summary>Restores one face adjustment without intermediate refreshes.</summary>
    public void SetAdjustment(bool flipHorizontal, bool flipVertical, int offsetX, int offsetY)
    {
        _suppressChanges = true;
        try
        {
            FlipHorizontal = flipHorizontal;
            FlipVertical = flipVertical;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }
        finally
        {
            _suppressChanges = false;
        }

        AlignmentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Resets Flip and Offset for this target.</summary>
    public void ResetAdjustment() => SetAdjustment(false, false, 0, 0);

    public void Dispose()
    {
        PreviewBitmap = null;
    }

    private void RaiseAlignmentChanged()
    {
        if (!_suppressChanges) AlignmentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
