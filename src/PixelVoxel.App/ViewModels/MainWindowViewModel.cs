using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelVoxel.App.Services;
using PixelVoxel.Core;
using PixelVoxel.Imaging;
using PixelVoxel.Rendering;
using PixelVoxel.Export;
using AvaloniaColor = Avalonia.Media.Color;

namespace PixelVoxel.App.ViewModels;

/// <summary>Coordinates import, camera motion, visual styling, and GPU/CPU render state.</summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private const float FreeRotationSensitivity = 0.7f;
    private static readonly VoxelFace[] TargetFaceOrder =
    [
        VoxelFace.Front,
        VoxelFace.Right,
        VoxelFace.Back,
        VoxelFace.Left,
        VoxelFace.Top,
        VoxelFace.Bottom,
    ];
    private readonly AsepriteSpriteSheetImporter _importer;
    private readonly IVoxelReconstructor _reconstructor;
    private readonly VoxelSurfaceMesher _mesher;
    private readonly PixelArtVoxelRasterizer _pixelArtRasterizer;
    private readonly VoxelRenderTransformResolver _transformResolver;
    private readonly PixelRenderLayoutResolver _layoutResolver;
    private readonly ViewportSettingsStore _settingsStore;
    private readonly SpriteExportCoordinator _spriteExportCoordinator;
    private readonly IProjectSerializer _projectSerializer;
    private readonly VoxelPicker _voxelPicker;
    private readonly VoxelEditHistory _editHistory = new();
    private readonly Dictionary<VoxelFace, string> _separatePaths = [];
    private CancellationTokenSource? _importCancellation;
    private CancellationTokenSource? _exportCancellation;
    private CancellationTokenSource? _meshCancellation;
    private SixViewImportDraft? _importDraft;
    private SixViewAlignmentPreview? _alignmentPreview;
    private OrthographicViewSet? _sourceViews;
    private VoxelDocument? _document;
    private VoxelMeshData? _mesh;
    private VoxelMeshData? _displayMesh;
    private PixelRenderLayout? _renderLayout;
    private VoxelRenderTransform? _renderTransform;
    private VoxelCameraState _camera;
    private VoxelModelRotationState _modelRotation = VoxelModelRotationState.Identity;
    private VoxelRenderStyle _renderStyle;
    private WriteableBitmap? _viewportBitmap;
    private string _importSummary = "No six-view input loaded.";
    private string _statusText = "Ready";
    private string _viewportMessage = "Import a 6×1 sheet or assign six PNG files.";
    private string _cameraSummary = "Pixel Preview · Pixel 2:1";
    private string _objectRotationSummary = "Object · Yaw 0° · Pitch 0° · Tilt 0°";
    private string _zoomSummary = "Fit";
    private int _sourcePixelWidth;
    private int _sourcePixelHeight;
    private float _rawYawDegrees;
    private float _rawPitchDegrees;
    private float _rawModelYawDegrees;
    private float _rawModelPitchDegrees;
    private float _rawModelRollDegrees;
    private float _savedDefaultYaw;
    private float _savedDefaultPitch;
    private bool _cameraFaceSnapEnabled;
    private bool _isCameraFaceSnapped;
    private bool _horizontalAnimationEnabled;
    private bool _verticalAnimationEnabled;
    private float _animationSpeed;
    private int? _manualZoomScale;
    private bool _lightingEnabled;
    private float _lightAzimuth;
    private float _lightElevation;
    private float _ambient;
    private float _intensity;
    private bool _outlineEnabled;
    private VoxelOutlineMode _outlineMode;
    private AvaloniaColor _outlineColor;
    private AvaloniaColor _backgroundColor;
    private bool _cpuFallbackActive;
    private bool _canApplyImport;
    private bool _isImportDraftLoaded;
    private int _exportDirectionCount;
    private bool _exportTrueIsometric;
    private bool _exportTransparentBackground;
    private string _exportSummary = "8 directions · Pixel 2:1 · Transparent";
    private IReadOnlyList<RecentImportSettings> _recentImports = [];
    private RecentImportSettings? _selectedRecentImport;
    private bool _suppressImportPreviewRefresh;
    private VoxelEditTool _selectedEditTool = VoxelEditTool.View;
    private AvaloniaColor _editColor = new(255, 255, 255, 255);
    private readonly List<VoxelPickResult> _editStroke = [];
    private bool _strokePaintAllFaces;
    private VoxelCoordinate? _selectionAnchor;
    private VoxelSelectionBox? _selection;
    private VoxelPickResult? _hoverPick;
    private string _selectionSummary = "No selection";
    private string? _projectPath;
    private int _resizeWidth = 1;
    private int _resizeHeight = 1;
    private int _resizeDepth = 1;
    private long _meshRevision;
    private long _importRevision;

    /// <summary>Initializes the application workflow and loads user viewport settings.</summary>
    public MainWindowViewModel(
        AsepriteSpriteSheetImporter importer,
        IVoxelReconstructor reconstructor,
        VoxelSurfaceMesher mesher,
        PixelArtVoxelRasterizer pixelArtRasterizer,
        VoxelRenderTransformResolver transformResolver,
        PixelRenderLayoutResolver layoutResolver,
        ViewportSettingsStore settingsStore,
        SpriteExportCoordinator spriteExportCoordinator,
        IProjectSerializer? projectSerializer = null,
        VoxelPicker? voxelPicker = null)
    {
        _importer = importer;
        _reconstructor = reconstructor;
        _mesher = mesher;
        _pixelArtRasterizer = pixelArtRasterizer;
        _transformResolver = transformResolver;
        _layoutResolver = layoutResolver;
        _settingsStore = settingsStore;
        _spriteExportCoordinator = spriteExportCoordinator;
        _projectSerializer = projectSerializer ?? new PxvProjectSerializer(new PngPixelWriter(), new PngPixelReader());
        _voxelPicker = voxelPicker ?? new VoxelPicker();
        _editHistory.Changed += OnEditHistoryChanged;

        ImportFaceSlots = new ObservableCollection<ImportFaceSlotViewModel>(
            TargetFaceOrder.Select(face => new ImportFaceSlotViewModel(face)));
        foreach (ImportFaceSlotViewModel slot in ImportFaceSlots)
        {
            slot.AlignmentChanged += OnImportFaceAlignmentChanged;
        }

        (ViewportUserSettings settings, string? diagnostic) = _settingsStore.Load();
        diagnostic ??= GetInvalidSettingsDiagnostic(settings);
        _savedDefaultYaw = VoxelCameraMotion.WrapAngle(FiniteOrDefault(settings.DefaultYaw, -45f));
        _savedDefaultPitch = VoxelCameraMotion.WrapAngle(FiniteOrDefault(settings.DefaultPitch, -30f));
        _rawYawDegrees = _savedDefaultYaw;
        _rawPitchDegrees = _savedDefaultPitch;
        _cameraFaceSnapEnabled = settings.CameraFaceSnapEnabled;
        _camera = new VoxelCameraState(
            VoxelViewMode.PixelPreview,
            VoxelCameraPreset.Free,
            _savedDefaultYaw,
            _savedDefaultPitch,
            0f,
            0f,
            1f);
        _manualZoomScale = settings.ZoomIsFit ? null : Math.Clamp(settings.ManualZoomScale, 1, 16);
        _animationSpeed = Math.Clamp(FiniteOrDefault(settings.AnimationSpeed, 30f), 5f, 180f);
        _lightingEnabled = settings.LightingEnabled;
        _lightAzimuth = Math.Clamp(FiniteOrDefault(settings.LightAzimuth, -45f), -180f, 180f);
        _lightElevation = Math.Clamp(FiniteOrDefault(settings.LightElevation, 45f), -90f, 90f);
        _ambient = Math.Clamp(FiniteOrDefault(settings.Ambient, 0.35f), 0f, 1f);
        _intensity = Math.Clamp(FiniteOrDefault(settings.Intensity, 0.65f), 0f, 1f);
        _outlineEnabled = settings.OutlineEnabled;
        _outlineMode = Enum.IsDefined(settings.OutlineMode)
            ? settings.OutlineMode
            : VoxelOutlineMode.Silhouette;
        _outlineColor = ParseColor(settings.OutlineColor, new AvaloniaColor(255, 0, 0, 0));
        _backgroundColor = ParseColor(settings.BackgroundColor, new AvaloniaColor(255, 20, 24, 32));
        _exportDirectionCount = settings.ExportDirectionCount is 4 or 8 or 16
            ? settings.ExportDirectionCount
            : 8;
        _exportTrueIsometric = settings.ExportTrueIsometric;
        _exportTransparentBackground = settings.ExportTransparentBackground;
        _recentImports = settings.RecentImports
            .Where(item => item.Paths.Count > 0)
            .Take(10)
            .ToArray();
        _selectedRecentImport = _recentImports.FirstOrDefault();
        _renderStyle = BuildRenderStyle();
        ZoomSummary = _manualZoomScale.HasValue ? $"{_manualZoomScale.Value}×" : "Fit";
        CameraSummary = $"Saved Default · Yaw {_camera.YawDegrees:0}° · Pitch {_camera.PitchDegrees:0}°";
        UpdateExportSummary();

        if (diagnostic is not null)
        {
            StatusText = diagnostic;
            SaveSettings();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? RenderStateChanged;

    /// <summary>Gets the six fixed target-face cards used by the alignment workspace.</summary>
    public ObservableCollection<ImportFaceSlotViewModel> ImportFaceSlots { get; }

    public VoxelMeshData? CurrentMesh => _displayMesh ?? _mesh;

    public VoxelRenderTransform? CurrentRenderTransform => _renderTransform;

    public VoxelCameraState CurrentCameraState => _camera;

    public VoxelModelRotationState CurrentModelRotation => _modelRotation;

    public PixelRenderLayout? CurrentRenderLayout => _renderLayout;

    public VoxelRenderStyle CurrentRenderStyle => _renderStyle;

    public int? ManualZoomScale => _manualZoomScale;

    public long ImportRevision => Interlocked.Read(ref _importRevision);

    /// <summary>Gets the active editable document.</summary>
    public VoxelDocument? CurrentDocument => _document;

    /// <summary>Gets whether a document is available for editing.</summary>
    public bool HasDocument => _document is not null;

    /// <summary>Gets whether the current project differs from its saved checkpoint.</summary>
    public bool IsProjectDirty => _document is not null && (_projectPath is null || _editHistory.IsDirty);

    /// <summary>Gets whether the most recent edit can be undone.</summary>
    public bool CanUndo => _editHistory.CanUndo;

    /// <summary>Gets whether an undone edit can be reapplied.</summary>
    public bool CanRedo => _editHistory.CanRedo;

    /// <summary>Gets the current project path when it has been saved.</summary>
    public string? ProjectPath => _projectPath;

    /// <summary>Gets the window title including the unsaved marker.</summary>
    public string WindowTitle => $"Pixel Voxel{(_projectPath is null ? string.Empty : $" — {Path.GetFileName(_projectPath)}")}{(IsProjectDirty ? " *" : string.Empty)}";

    public VoxelEditTool SelectedEditTool
    {
        get => _selectedEditTool;
        set
        {
            if (!SetField(ref _selectedEditTool, value)) return;
            _editStroke.Clear();
            _hoverPick = null;
            OnPropertyChanged(nameof(IsViewMode));
            RefreshEditorOverlay();
        }
    }

    /// <summary>Gets whether left-button viewport input navigates instead of editing.</summary>
    public bool IsViewMode => SelectedEditTool == VoxelEditTool.View;

    public AvaloniaColor EditColor
    {
        get => _editColor;
        set
        {
            if (!SetField(ref _editColor, new AvaloniaColor(255, value.R, value.G, value.B))) return;
            if (SelectedEditTool == VoxelEditTool.Paint && (_hoverPick is not null || _editStroke.Count > 0))
            {
                RefreshEditorOverlay();
            }
        }
    }

    public string SelectionSummary
    {
        get => _selectionSummary;
        private set => SetField(ref _selectionSummary, value);
    }

    public int ResizeWidth
    {
        get => _resizeWidth;
        set => SetField(ref _resizeWidth, Math.Max(1, value));
    }

    public int ResizeHeight
    {
        get => _resizeHeight;
        set => SetField(ref _resizeHeight, Math.Max(1, value));
    }

    public int ResizeDepth
    {
        get => _resizeDepth;
        set => SetField(ref _resizeDepth, Math.Max(1, value));
    }

    public bool IsImportDraftLoaded
    {
        get => _isImportDraftLoaded;
        private set => SetField(ref _isImportDraftLoaded, value);
    }

    public bool CanApplyImport
    {
        get => _canApplyImport;
        private set => SetField(ref _canApplyImport, value);
    }

    public bool CanExport => _mesh is not null && _renderLayout is not null;

    public IReadOnlyList<RecentImportSettings> RecentImports
    {
        get => _recentImports;
        private set => SetField(ref _recentImports, value);
    }

    public RecentImportSettings? SelectedRecentImport
    {
        get => _selectedRecentImport;
        set => SetField(ref _selectedRecentImport, value);
    }

    public int ExportDirectionCount
    {
        get => _exportDirectionCount;
        set
        {
            if (value is not (4 or 8 or 16)) return;
            if (!SetField(ref _exportDirectionCount, value)) return;
            UpdateExportSummary();
            SaveSettings();
        }
    }

    public bool ExportTrueIsometric
    {
        get => _exportTrueIsometric;
        set
        {
            if (!SetField(ref _exportTrueIsometric, value)) return;
            UpdateExportSummary();
            SaveSettings();
        }
    }

    public bool ExportTransparentBackground
    {
        get => _exportTransparentBackground;
        set
        {
            if (!SetField(ref _exportTransparentBackground, value)) return;
            UpdateExportSummary();
            SaveSettings();
        }
    }

    public string ExportSummary
    {
        get => _exportSummary;
        private set => SetField(ref _exportSummary, value);
    }

    public bool IsCpuFallbackActive
    {
        get => _cpuFallbackActive;
        private set => SetField(ref _cpuFallbackActive, value);
    }

    public WriteableBitmap? ViewportBitmap
    {
        get => _viewportBitmap;
        private set
        {
            if (ReferenceEquals(_viewportBitmap, value)) return;
            WriteableBitmap? previous = _viewportBitmap;
            _viewportBitmap = value;
            OnPropertyChanged();
            previous?.Dispose();
        }
    }

    public string ImportSummary
    {
        get => _importSummary;
        private set => SetField(ref _importSummary, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string ViewportMessage
    {
        get => _viewportMessage;
        private set
        {
            if (!SetField(ref _viewportMessage, value)) return;
            OnPropertyChanged(nameof(HasViewportMessage));
        }
    }

    public bool HasViewportMessage => !string.IsNullOrWhiteSpace(_viewportMessage);

    public string CameraSummary
    {
        get => _cameraSummary;
        private set => SetField(ref _cameraSummary, value);
    }

    public string ObjectRotationSummary
    {
        get => _objectRotationSummary;
        private set => SetField(ref _objectRotationSummary, value);
    }

    /// <summary>Gets or sets object-local Z-axis tilt in degrees.</summary>
    public float ModelRollDegrees
    {
        get => _rawModelRollDegrees;
        set
        {
            float roll = VoxelCameraMotion.WrapAngle(FiniteOrDefault(value, 0f));
            if (MathF.Abs(_rawModelRollDegrees - roll) < 0.001f) return;
            _rawModelRollDegrees = roll;
            ApplyModelRotation();
        }
    }

    public string ZoomSummary
    {
        get => _zoomSummary;
        private set => SetField(ref _zoomSummary, value);
    }

    public bool HorizontalAnimationEnabled
    {
        get => _horizontalAnimationEnabled;
        set
        {
            if (!SetField(ref _horizontalAnimationEnabled, value)) return;
            if (!value)
            {
                ResetModelRotationAxis(resetYaw: true, resetPitch: false);
            }
            OnPropertyChanged(nameof(IsAnimationActive));
        }
    }

    public bool VerticalAnimationEnabled
    {
        get => _verticalAnimationEnabled;
        set
        {
            if (!SetField(ref _verticalAnimationEnabled, value)) return;
            if (!value)
            {
                ResetModelRotationAxis(resetYaw: false, resetPitch: true);
            }
            OnPropertyChanged(nameof(IsAnimationActive));
        }
    }

    public bool IsAnimationActive => HorizontalAnimationEnabled || VerticalAnimationEnabled;

    public bool CameraFaceSnapEnabled
    {
        get => _cameraFaceSnapEnabled;
        set
        {
            if (!SetField(ref _cameraFaceSnapEnabled, value)) return;
            if (_camera.Mode == VoxelViewMode.FreeView)
            {
                ApplyFreeCamera();
            }

            SaveSettings();
        }
    }

    public float AnimationSpeed
    {
        get => _animationSpeed;
        set
        {
            float clamped = Math.Clamp(FiniteOrDefault(value, 30f), 5f, 180f);
            if (!SetField(ref _animationSpeed, clamped)) return;
            SaveSettings();
        }
    }

    public bool LightingEnabled
    {
        get => _lightingEnabled;
        set { if (SetField(ref _lightingEnabled, value)) VisualStyleChanged(); }
    }

    public float LightAzimuth
    {
        get => _lightAzimuth;
        set { if (SetField(ref _lightAzimuth, Math.Clamp(FiniteOrDefault(value, -45f), -180f, 180f))) VisualStyleChanged(); }
    }

    public float LightElevation
    {
        get => _lightElevation;
        set { if (SetField(ref _lightElevation, Math.Clamp(FiniteOrDefault(value, 45f), -90f, 90f))) VisualStyleChanged(); }
    }

    public float Ambient
    {
        get => _ambient;
        set { if (SetField(ref _ambient, Math.Clamp(FiniteOrDefault(value, 0.35f), 0f, 1f))) VisualStyleChanged(); }
    }

    public float Intensity
    {
        get => _intensity;
        set { if (SetField(ref _intensity, Math.Clamp(FiniteOrDefault(value, 0.65f), 0f, 1f))) VisualStyleChanged(); }
    }

    public bool OutlineEnabled
    {
        get => _outlineEnabled;
        set { if (SetField(ref _outlineEnabled, value)) VisualStyleChanged(); }
    }

    public bool IncludeDepthOutline
    {
        get => _outlineMode == VoxelOutlineMode.SilhouetteAndDepth;
        set
        {
            VoxelOutlineMode mode = value
                ? VoxelOutlineMode.SilhouetteAndDepth
                : VoxelOutlineMode.Silhouette;
            if (!SetField(ref _outlineMode, mode, nameof(OutlineMode))) return;
            OnPropertyChanged();
            VisualStyleChanged();
        }
    }

    public VoxelOutlineMode OutlineMode => _outlineMode;

    public AvaloniaColor OutlineColor
    {
        get => _outlineColor;
        set
        {
            AvaloniaColor opaque = new(255, value.R, value.G, value.B);
            if (SetField(ref _outlineColor, opaque)) VisualStyleChanged();
        }
    }

    public AvaloniaColor BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            AvaloniaColor opaque = new(255, value.R, value.G, value.B);
            if (SetField(ref _backgroundColor, opaque)) VisualStyleChanged();
        }
    }

    public string FrontPath => GetSeparatePath(VoxelFace.Front);
    public string RightPath => GetSeparatePath(VoxelFace.Right);
    public string BackPath => GetSeparatePath(VoxelFace.Back);
    public string LeftPath => GetSeparatePath(VoxelFace.Left);
    public string TopPath => GetSeparatePath(VoxelFace.Top);
    public string BottomPath => GetSeparatePath(VoxelFace.Bottom);

    public Task LoadHorizontalSheetAsync(string path) =>
        RunInspectionAsync(() => _importer.InspectHorizontalSheet(path), null, default);

    public Task LoadSeparateFilesAsync(IEnumerable<string> paths) =>
        RunInspectionAsync(() => _importer.InspectSeparate(paths), null, default);

    public void SetSeparatePath(VoxelFace face, string path)
    {
        _separatePaths[face] = path;
        OnPropertyChanged(GetPathPropertyName(face));
        StatusText = $"Assigned {face}: {Path.GetFileName(path)}";
    }

    public Task ReconstructSeparateAsync()
    {
        Dictionary<VoxelFace, string> snapshot = new(_separatePaths);
        return RunInspectionAsync(() => _importer.InspectSeparate(snapshot), null, default);
    }

    public Task ApplyImportDraftAsync()
    {
        if (_importDraft is null || !CanApplyImport)
        {
            StatusText = "Fix the blocking import diagnostics before reconstruction.";
            return Task.CompletedTask;
        }

        SixViewImportDraft draft = _importDraft;
        SixViewAlignment alignment = BuildCurrentAlignment();
        return RunImportAsync(
            _ => _importer.ApplyAlignment(draft, alignment),
            default);
    }

    public Task ReimportAsync()
    {
        if (_importDraft is null)
        {
            StatusText = "No import draft is available to reload.";
            return Task.CompletedTask;
        }

        RecentFaceAlignmentSettings[] alignment = CaptureAlignmentSettings();
        string[] paths = _importDraft.Slots
            .Select(slot => slot.SourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Func<SixViewImportDraft> inspect = _importDraft.SourceKind == SixViewImportSourceKind.HorizontalSheet
            ? () => _importer.InspectHorizontalSheet(paths[0])
            : () => _importer.InspectSeparate(paths);
        return RunInspectionAsync(inspect, alignment, default);
    }

    public Task OpenSelectedRecentImportAsync()
    {
        RecentImportSettings? recent = SelectedRecentImport;
        if (recent is null || recent.Paths.Count == 0)
        {
            StatusText = "Select a recent import first.";
            return Task.CompletedTask;
        }

        Func<SixViewImportDraft> inspect = recent.SourceKind == SixViewImportSourceKind.HorizontalSheet
            ? () => _importer.InspectHorizontalSheet(recent.Paths[0])
            : () => _importer.InspectSeparate(recent.Paths);
        return RunInspectionAsync(inspect, recent.Alignments, default);
    }

    public void SwapImportSources(VoxelFace first, VoxelFace second)
    {
        if (first == second) return;
        ImportFaceSlotViewModel firstSlot = ImportFaceSlots.Single(slot => slot.TargetFace == first);
        ImportFaceSlotViewModel secondSlot = ImportFaceSlots.Single(slot => slot.TargetFace == second);
        int firstSource = firstSlot.SourceSlotIndex;
        string firstName = firstSlot.SourceName;
        if (secondSlot.HasSource)
        {
            firstSlot.AssignSource(secondSlot.SourceSlotIndex, secondSlot.SourceName);
        }
        else
        {
            firstSlot.ClearSource();
        }

        if (firstSource >= 0)
        {
            secondSlot.AssignSource(firstSource, firstName);
        }
        else
        {
            secondSlot.ClearSource();
        }

        RefreshAlignmentPreview();
    }

    public void ResetImportFace(VoxelFace face)
    {
        ImportFaceSlots.Single(slot => slot.TargetFace == face).ResetAdjustment();
    }

    public void ResetAllImportAdjustments()
    {
        foreach (ImportFaceSlotViewModel slot in ImportFaceSlots)
        {
            slot.SetAdjustment(false, false, 0, 0);
        }

        RefreshAlignmentPreview();
    }

    public async Task ExportCurrentViewAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_mesh is null || _renderLayout is null)
        {
            StatusText = "Load and reconstruct a model before exporting.";
            return;
        }

        await RunExportAsync(
            token => _spriteExportCoordinator.ExportCurrentViewAsync(
                path,
                _mesh,
                _camera,
                _modelRotation,
                _renderLayout,
                _renderStyle,
                _exportTransparentBackground,
                token),
            cancellationToken);
    }

    public async Task ExportDirectionSheetAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_mesh is null || _renderLayout is null)
        {
            StatusText = "Load and reconstruct a model before exporting.";
            return;
        }

        VoxelCameraPreset preset = _exportTrueIsometric
            ? VoxelCameraPreset.TrueIsometric
            : VoxelCameraPreset.Pixel2To1;
        await RunExportAsync(
            token => _spriteExportCoordinator.ExportDirectionSheetAsync(
                path,
                _exportDirectionCount,
                preset,
                _mesh,
                _renderLayout,
                _renderStyle,
                _exportTransparentBackground,
                token),
            cancellationToken);
    }

    public void SelectPreset(VoxelCameraPreset preset)
    {
        (float yaw, float pitch) = preset switch
        {
            VoxelCameraPreset.Pixel2To1 => (-45f, -30f),
            VoxelCameraPreset.TrueIsometric => (-45f, -35.2643897f),
            VoxelCameraPreset.Front => (0f, 0f),
            VoxelCameraPreset.Right => (-90f, 0f),
            VoxelCameraPreset.Top => (0f, -90f),
            _ => (_rawYawDegrees, _rawPitchDegrees),
        };
        SetCamera(yaw, pitch, VoxelViewMode.PixelPreview, preset);
        CameraSummary = $"Pixel Preview · {GetPresetName(preset)}";
    }

    public void LoadSavedDefault()
    {
        SetCamera(_savedDefaultYaw, _savedDefaultPitch, VoxelViewMode.PixelPreview, VoxelCameraPreset.Free);
        CameraSummary = $"Saved Default · Yaw {_camera.YawDegrees:0}° · Pitch {_camera.PitchDegrees:0}°";
    }

    public void SaveCurrentAsDefault()
    {
        _savedDefaultYaw = VoxelCameraMotion.WrapAngle(_camera.YawDegrees);
        _savedDefaultPitch = VoxelCameraMotion.WrapAngle(_camera.PitchDegrees);
        SaveSettings();
        StatusText = $"Saved default camera: {_savedDefaultYaw:0}°, {_savedDefaultPitch:0}°";
    }

    public void Rotate(float deltaX, float deltaY)
    {
        _rawYawDegrees = VoxelCameraMotion.RotateYaw(
            _rawYawDegrees,
            deltaX,
            FreeRotationSensitivity);
        _rawPitchDegrees = VoxelCameraMotion.RotatePitch(
            _rawPitchDegrees,
            deltaY,
            FreeRotationSensitivity);
        ApplyFreeCamera();
    }

    /// <summary>Enters unrestricted orbit mode at the current camera angle.</summary>
    public void EnterFreeView()
    {
        CameraFaceSnapEnabled = false;
        _rawYawDegrees = _camera.YawDegrees;
        _rawPitchDegrees = _camera.PitchDegrees;
        ApplyFreeCamera();
        CameraSummary = $"Free View · Yaw {_camera.YawDegrees:0}° · Pitch {_camera.PitchDegrees:0}°";
        StatusText = "Free rotation enabled";
    }

    /// <summary>Tilts the object around its local Z axis.</summary>
    public void Tilt(float deltaX) =>
        ModelRollDegrees = _rawModelRollDegrees + (deltaX * FreeRotationSensitivity);

    /// <summary>Restores only the object-local tilt.</summary>
    public void ResetTilt()
    {
        ModelRollDegrees = 0f;
        StatusText = "Object tilt reset";
    }

    /// <summary>Restores the standard camera and clears transient object rotation.</summary>
    public void ResetView()
    {
        _horizontalAnimationEnabled = false;
        _verticalAnimationEnabled = false;
        OnPropertyChanged(nameof(HorizontalAnimationEnabled));
        OnPropertyChanged(nameof(VerticalAnimationEnabled));
        OnPropertyChanged(nameof(IsAnimationActive));
        _rawModelYawDegrees = 0f;
        _rawModelPitchDegrees = 0f;
        _rawModelRollDegrees = 0f;
        ApplyModelRotation();
        SelectPreset(VoxelCameraPreset.Pixel2To1);
        StatusText = "View reset to Pixel 2:1";
    }

    /// <summary>Commits a visible face snap as the starting point for the next orbit drag.</summary>
    public void CommitCameraSnap()
    {
        if (!_isCameraFaceSnapped) return;
        _rawYawDegrees = _camera.YawDegrees;
        _rawPitchDegrees = _camera.PitchDegrees;
        _isCameraFaceSnapped = false;
        CameraSummary = $"Free View · Yaw {_camera.YawDegrees:0}° · Pitch {_camera.PitchDegrees:0}° · Face Aligned";
    }

    public void AdvanceAnimations(double elapsedSeconds)
    {
        if (!IsAnimationActive || elapsedSeconds <= 0d) return;
        float delta = _animationSpeed * (float)Math.Min(elapsedSeconds, 0.1d);

        if (HorizontalAnimationEnabled)
        {
            _rawModelYawDegrees = VoxelCameraMotion.WrapAngle(_rawModelYawDegrees + delta);
        }

        if (VerticalAnimationEnabled)
        {
            _rawModelPitchDegrees = VoxelCameraMotion.WrapAngle(_rawModelPitchDegrees + delta);
        }

        ApplyModelRotation();
    }

    public void ZoomBy(int direction, int currentFitScale)
    {
        if (direction == 0) return;
        int baseline = _manualZoomScale ?? Math.Clamp(currentFitScale, 1, 16);
        _manualZoomScale = Math.Clamp(baseline + Math.Sign(direction), 1, 16);
        ZoomSummary = $"{_manualZoomScale.Value}×";
        OnPropertyChanged(nameof(ManualZoomScale));
        SaveSettings();
        RenderStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void FitZoom()
    {
        _manualZoomScale = null;
        ZoomSummary = "Fit";
        OnPropertyChanged(nameof(ManualZoomScale));
        SaveSettings();
        RenderStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ActivateCpuFallback(string message)
    {
        IsCpuFallbackActive = true;
        StatusText = $"OpenGL unavailable; CPU fallback active. {message}";
        RenderCurrentScene();
    }

    public void ReportError(string message)
    {
        StatusText = message;
        ViewportMessage = message;
    }

    /// <summary>Creates a new editable 32-cube project with one center voxel.</summary>
    public async Task NewProjectAsync(CancellationToken cancellationToken = default)
    {
        VoxelDimensions dimensions = new(32, 32, 32);
        VoxelCoordinate center = new(15, 15, 15);
        VoxelDocument document = new(
            dimensions,
            [new VoxelEntry(center, VoxelCell.CreateUniform(ToRgba(_editColor)))]);
        VoxelMeshBuildResult meshResult = await Task.Run(
            () => _mesher.Build(document, cancellationToken),
            cancellationToken);
        ApplyLoadedDocument(document, null, meshResult, null, "New project");
    }

    /// <summary>Saves the current editable project to a portable .pxv archive.</summary>
    public async Task<bool> SaveProjectAsync(
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        if (_document is null)
        {
            StatusText = "There is no project to save.";
            return false;
        }

        string? destination = path ?? _projectPath;
        if (string.IsNullOrWhiteSpace(destination))
        {
            StatusText = "Choose a .pxv destination first.";
            return false;
        }

        try
        {
            StatusText = "Saving Pixel Voxel project...";
            PixelVoxelProject project = new(
                new VoxelDocument(_document.Storage),
                _sourceViews,
                CaptureProjectSettings());
            await _projectSerializer.SaveAsync(destination, project, cancellationToken);
            _projectPath = Path.GetFullPath(destination);
            _editHistory.MarkClean();
            OnPropertyChanged(nameof(ProjectPath));
            OnPropertyChanged(nameof(WindowTitle));
            StatusText = $"Saved {Path.GetFileName(_projectPath)}";
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Project save cancelled.";
            return false;
        }
        catch (Exception exception)
        {
            StatusText = $"Project save failed: {exception.Message}";
            return false;
        }
    }

    /// <summary>Loads and validates a portable project before replacing current application state.</summary>
    public async Task<bool> LoadProjectAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            StatusText = "Loading Pixel Voxel project...";
            PixelVoxelProject project = await _projectSerializer.LoadAsync(path, cancellationToken);
            VoxelMeshBuildResult meshResult = await Task.Run(
                () => _mesher.Build(project.Document, cancellationToken),
                cancellationToken);
            ApplyProjectSettings(project.Settings);
            ApplyLoadedDocument(
                project.Document,
                project.SourceViews,
                meshResult,
                Path.GetFullPath(path),
                $"Project: {Path.GetFileName(path)}");
            _editHistory.MarkClean();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Project load cancelled.";
            return false;
        }
        catch (Exception exception)
        {
            StatusText = $"Project load failed: {exception.Message}";
            return false;
        }
    }

    /// <summary>Begins one left-button edit transaction at a physical viewport position.</summary>
    public bool BeginEditStroke(
        float targetX,
        float targetY,
        int targetWidth,
        int targetHeight,
        bool paintAllFaces)
    {
        if (SelectedEditTool == VoxelEditTool.View) return false;
        VoxelPickResult? pick = PickViewport(targetX, targetY, targetWidth, targetHeight);
        if (pick is null) return false;
        if (SelectedEditTool == VoxelEditTool.Select)
        {
            SelectPickedVoxel(pick.Coordinate);
            return false;
        }

        _editStroke.Clear();
        _strokePaintAllFaces = paintAllFaces;
        _editStroke.Add(pick);
        _hoverPick = pick;
        RefreshEditorOverlay();
        StatusText = $"{SelectedEditTool}: {pick.Coordinate} {pick.Face}";
        return true;
    }

    /// <summary>Adds one drag sample to the current edit transaction.</summary>
    public void ContinueEditStroke(float targetX, float targetY, int targetWidth, int targetHeight)
    {
        if (_editStroke.Count == 0) return;
        VoxelPickResult? pick = PickViewport(targetX, targetY, targetWidth, targetHeight);
        if (pick is null || Equals(_hoverPick, pick)) return;
        _hoverPick = pick;
        if (!_editStroke.Contains(pick)) _editStroke.Add(pick);
        RefreshEditorOverlay();
    }

    /// <summary>Commits all samples from one pointer drag as one Undo entry.</summary>
    public void EndEditStroke()
    {
        if (_document is null || _editStroke.Count == 0)
        {
            _editStroke.Clear();
            return;
        }

        try
        {
            switch (SelectedEditTool)
            {
                case VoxelEditTool.Add:
                {
                    VoxelCoordinate[] targets = _editStroke
                        .Select(pick => pick.AdjacentCoordinate)
                        .Distinct()
                        .Where(IsInsideCurrentDocument)
                        .Where(coordinate => !_document.Storage.TryGetCell(coordinate, out _))
                        .ToArray();
                    CommitEdit(new AddVoxelsCommand(targets, ToRgba(_editColor)));
                    break;
                }
                case VoxelEditTool.Erase:
                    CommitEdit(new EraseVoxelsCommand(_editStroke.Select(pick => pick.Coordinate)));
                    break;
                case VoxelEditTool.Paint:
                    CommitPaintStroke(_editStroke, _strokePaintAllFaces);
                    break;
            }
        }
        catch (Exception exception)
        {
            StatusText = $"Edit rejected: {exception.Message}";
        }
        finally
        {
            _editStroke.Clear();
            _hoverPick = null;
            RefreshEditorOverlay();
        }
    }

    public void Undo()
    {
        if (_document is null || !_editHistory.Undo(_document)) return;
        StatusText = "Undo";
        ScheduleMeshRebuild();
    }

    public void Redo()
    {
        if (_document is null || !_editHistory.Redo(_document)) return;
        StatusText = "Redo";
        ScheduleMeshRebuild();
    }

    public void ClearSelection()
    {
        _selectionAnchor = null;
        _selection = null;
        SelectionSummary = "No selection";
        RefreshEditorOverlay();
    }

    public void UpdateEditorHover(float targetX, float targetY, int targetWidth, int targetHeight)
    {
        VoxelPickResult? pick = PickViewport(targetX, targetY, targetWidth, targetHeight);
        if (Equals(_hoverPick, pick)) return;
        _hoverPick = pick;
        RefreshEditorOverlay();
    }

    public void ClearEditorHover()
    {
        if (_hoverPick is null) return;
        _hoverPick = null;
        RefreshEditorOverlay();
    }

    public void DeleteSelection()
    {
        if (_document is null || _selection is not VoxelSelectionBox selection) return;
        CommitEdit(new EraseVoxelsCommand(
            _document.Storage.GetOccupiedCells()
                .Where(entry => selection.Contains(entry.Coordinate))
                .Select(entry => entry.Coordinate)));
        ClearSelection();
    }

    public void MoveSelection(int x, int y, int z)
    {
        if (_selection is not VoxelSelectionBox selection) return;
        VoxelCoordinate delta = new(x, y, z);
        try
        {
            CommitEdit(new MoveVoxelSelectionCommand(selection, delta));
            _selection = new VoxelSelectionBox(
                Offset(selection.Minimum, delta),
                Offset(selection.Maximum, delta));
            UpdateSelectionSummary();
            RefreshEditorOverlay();
        }
        catch (Exception exception)
        {
            StatusText = $"Move rejected: {exception.Message}";
        }
    }

    public int CountResizeClippedVoxels()
    {
        if (_document is null) return 0;
        VoxelDimensions dimensions = new(ResizeWidth, ResizeHeight, ResizeDepth);
        return _document.Storage.GetOccupiedCells().Count(entry =>
            entry.Coordinate.X < 0 || entry.Coordinate.X >= dimensions.Width ||
            entry.Coordinate.Y < 0 || entry.Coordinate.Y >= dimensions.Height ||
            entry.Coordinate.Z < 0 || entry.Coordinate.Z >= dimensions.Depth);
    }

    public void ResizeVolume(bool allowClipping)
    {
        try
        {
            CommitEdit(new ResizeVoxelVolumeCommand(
                new VoxelDimensions(ResizeWidth, ResizeHeight, ResizeDepth),
                allowClipping));
            ClearSelection();
        }
        catch (Exception exception)
        {
            StatusText = $"Resize rejected: {exception.Message}";
        }
    }

    public void Dispose()
    {
        _importCancellation?.Cancel();
        _importCancellation?.Dispose();
        _exportCancellation?.Cancel();
        _exportCancellation?.Dispose();
        _meshCancellation?.Cancel();
        _meshCancellation?.Dispose();
        _editHistory.Changed -= OnEditHistoryChanged;
        foreach (ImportFaceSlotViewModel slot in ImportFaceSlots)
        {
            slot.AlignmentChanged -= OnImportFaceAlignmentChanged;
            slot.Dispose();
        }
        _viewportBitmap?.Dispose();
        _settingsStore.Dispose();
    }

    private VoxelPickResult? PickViewport(
        float targetX,
        float targetY,
        int targetWidth,
        int targetHeight)
    {
        if (_document is null || _renderTransform is null || _renderLayout is null) return null;
        PixelViewportMapping mapping = PixelViewportMapping.Create(
            targetWidth,
            targetHeight,
            _renderLayout.Width,
            _renderLayout.Height,
            _manualZoomScale);
        return mapping.TryMapToLogical(targetX, targetY, out System.Numerics.Vector2 logical)
            ? _voxelPicker.Pick(_document, _renderTransform, logical)
            : null;
    }

    private void SelectPickedVoxel(VoxelCoordinate coordinate)
    {
        if (_selectionAnchor is null)
        {
            _selection = null;
            _selectionAnchor = coordinate;
            SelectionSummary = $"Selection start: {coordinate.X}, {coordinate.Y}, {coordinate.Z}";
            RefreshEditorOverlay();
            return;
        }

        _selection = new VoxelSelectionBox(_selectionAnchor.Value, coordinate);
        _selectionAnchor = null;
        UpdateSelectionSummary();
        RefreshEditorOverlay();
    }

    private void UpdateSelectionSummary()
    {
        if (_selection is not VoxelSelectionBox selection)
        {
            SelectionSummary = "No selection";
            return;
        }

        SelectionSummary =
            $"{selection.Minimum.X},{selection.Minimum.Y},{selection.Minimum.Z} → " +
            $"{selection.Maximum.X},{selection.Maximum.Y},{selection.Maximum.Z}";
    }

    private void RefreshEditorOverlay(bool render = true)
    {
        bool paintsColor = SelectedEditTool == VoxelEditTool.Paint;
        IReadOnlyCollection<VoxelPickResult> preview = _editStroke.Count > 0
            ? _editStroke
            : paintsColor && _hoverPick is not null ? [_hoverPick] : [];
        _displayMesh = _mesh?.WithEditorOverlay(
            _selection,
            paintsColor ? null : _hoverPick,
            preview,
            paintsColor ? ToRgba(_editColor) : null);
        OnPropertyChanged(nameof(CurrentMesh));
        if (render) RenderCurrentScene();
    }

    private void CommitEdit(IVoxelEditCommand command)
    {
        if (_document is null) return;
        if (!_editHistory.Execute(_document, command)) return;
        StatusText = command.Description;
        ScheduleMeshRebuild();
    }

    private void CommitPaintStroke(IReadOnlyList<VoxelPickResult> picks, bool paintAllFaces)
    {
        if (_document is null) return;
        Dictionary<VoxelCoordinate, VoxelCell> before = [];
        Dictionary<VoxelCoordinate, VoxelCell> after = [];
        Rgba32Color color = ToRgba(_editColor);
        foreach (VoxelPickResult pick in picks)
        {
            if (!_document.Storage.TryGetCell(pick.Coordinate, out VoxelCell? source) || source is null) continue;
            if (!before.ContainsKey(pick.Coordinate)) before.Add(pick.Coordinate, source);
            VoxelCell current = after.TryGetValue(pick.Coordinate, out VoxelCell? prior) ? prior : source;
            after[pick.Coordinate] = paintAllFaces
                ? current.WithAllFaceColors(color)
                : current.WithFaceColor(pick.Face, color);
        }

        VoxelDimensions dimensions = _document.Storage.Dimensions;
        VoxelChange[] changes = before.Keys
            .OrderBy(coordinate => coordinate.X)
            .ThenBy(coordinate => coordinate.Y)
            .ThenBy(coordinate => coordinate.Z)
            .Where(coordinate => !before[coordinate].Equals(after[coordinate]))
            .Select(coordinate => new VoxelChange(coordinate, before[coordinate], after[coordinate]))
            .ToArray();
        if (_editHistory.Execute(
            _document,
            new VoxelChangeSet("Paint voxel stroke", dimensions, dimensions, changes)))
        {
            StatusText = "Paint voxel stroke";
            ScheduleMeshRebuild();
        }
    }

    private void ScheduleMeshRebuild()
    {
        if (_document is null) return;
        _meshCancellation?.Cancel();
        _meshCancellation?.Dispose();
        _meshCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _meshCancellation.Token;
        long revision = ++_meshRevision;
        VoxelDocument source = _document;
        long documentRevision = source.Revision;
        VoxelDocument snapshot = new(source.Storage);
        _ = RebuildMeshAsync(source, documentRevision, snapshot, revision, cancellationToken);
    }

    private async Task RebuildMeshAsync(
        VoxelDocument source,
        long documentRevision,
        VoxelDocument snapshot,
        long revision,
        CancellationToken cancellationToken)
    {
        try
        {
            VoxelMeshBuildResult result = await Task.Run(
                () => _mesher.Build(snapshot, cancellationToken),
                cancellationToken);
            if (!ReferenceEquals(_document, source) ||
                source.Revision != documentRevision ||
                revision != _meshRevision ||
                cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _mesh = result.Mesh;
            RefreshEditorOverlay(render: false);
            OnPropertyChanged(nameof(CanExport));
            if (!result.IsSuccess)
            {
                _renderTransform = null;
                ViewportBitmap = null;
                ViewportMessage = result.Diagnostic ?? "The render cache could not be created.";
                RenderStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            VoxelDimensions dimensions = source.Storage.Dimensions;
            _renderLayout = _layoutResolver.Resolve(
                dimensions,
                Math.Max(1, _sourcePixelWidth),
                Math.Max(1, _sourcePixelHeight));
            ViewportMessage = string.Empty;
            RenderCurrentScene();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (revision == _meshRevision) StatusText = $"Mesh rebuild failed: {exception.Message}";
        }
    }

    private void ApplyLoadedDocument(
        VoxelDocument document,
        OrthographicViewSet? sourceViews,
        VoxelMeshBuildResult meshResult,
        string? projectPath,
        string summary)
    {
        _meshCancellation?.Cancel();
        _document = document;
        _sourceViews = sourceViews;
        _mesh = meshResult.Mesh;
        _projectPath = projectPath;
        _sourcePixelWidth = sourceViews?.Views.Max(pair => pair.Value.Width) ?? document.Storage.Dimensions.Width;
        _sourcePixelHeight = sourceViews?.Views.Max(pair => pair.Value.Height) ?? document.Storage.Dimensions.Height;
        VoxelDimensions dimensions = document.Storage.Dimensions;
        ResizeWidth = dimensions.Width;
        ResizeHeight = dimensions.Height;
        ResizeDepth = dimensions.Depth;
        _renderLayout = _layoutResolver.Resolve(
            dimensions,
            Math.Max(1, _sourcePixelWidth),
            Math.Max(1, _sourcePixelHeight));
        _editHistory.Clear();
        ClearSelection();
        RefreshEditorOverlay(render: false);
        ImportSummary =
            $"{summary}{Environment.NewLine}" +
            $"Volume: {dimensions.Width} × {dimensions.Height} × {dimensions.Depth}{Environment.NewLine}" +
            $"Occupied: {document.Storage.OccupiedCount:N0}{Environment.NewLine}" +
            $"Exposed faces: {meshResult.ActualExposedFaceCount:N0}";
        OnPropertyChanged(nameof(CurrentDocument));
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(ProjectPath));
        OnPropertyChanged(nameof(WindowTitle));

        if (!meshResult.IsSuccess)
        {
            _renderTransform = null;
            ViewportBitmap = null;
            ViewportMessage = meshResult.Diagnostic ?? "The render cache could not be created.";
            StatusText = "Project loaded; render-cache limit exceeded";
            RenderStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        ViewportMessage = string.Empty;
        StatusText = projectPath is null ? "New editable project" : $"Loaded {Path.GetFileName(projectPath)}";
        RenderCurrentScene();
    }

    private PixelVoxelProjectSettings CaptureProjectSettings() =>
        new(
            _camera.Preset.ToString(),
            _camera.YawDegrees,
            _camera.PitchDegrees,
            _modelRotation.YawDegrees,
            _modelRotation.PitchDegrees,
            _renderStyle.Background,
            _lightingEnabled,
            _lightAzimuth,
            _lightElevation,
            _ambient,
            _intensity,
            _outlineEnabled,
            _outlineMode == VoxelOutlineMode.SilhouetteAndDepth,
            ToRgba(_outlineColor),
            _exportDirectionCount,
            _exportTrueIsometric,
            _exportTransparentBackground,
            _modelRotation.RollDegrees);

    private void ApplyProjectSettings(PixelVoxelProjectSettings settings)
    {
        VoxelCameraPreset preset = Enum.TryParse(settings.CameraPreset, out VoxelCameraPreset parsed) && Enum.IsDefined(parsed)
            ? parsed
            : VoxelCameraPreset.Free;
        float yaw = FiniteOrDefault(settings.CameraYaw, -45f);
        float pitch = FiniteOrDefault(settings.CameraPitch, -30f);
        _rawYawDegrees = yaw;
        _rawPitchDegrees = pitch;
        _camera = new VoxelCameraState(
            preset == VoxelCameraPreset.Free ? VoxelViewMode.FreeView : VoxelViewMode.PixelPreview,
            preset,
            yaw,
            pitch,
            0f,
            0f,
            1f);
        _rawModelYawDegrees = FiniteOrDefault(settings.ModelYaw, 0f);
        _rawModelPitchDegrees = FiniteOrDefault(settings.ModelPitch, 0f);
        _rawModelRollDegrees = FiniteOrDefault(settings.ModelRoll, 0f);
        _modelRotation = new VoxelModelRotationState(
            _rawModelYawDegrees,
            _rawModelPitchDegrees,
            _rawModelRollDegrees);
        _backgroundColor = new AvaloniaColor(255, settings.BackgroundColor.Red, settings.BackgroundColor.Green, settings.BackgroundColor.Blue);
        _lightingEnabled = settings.LightingEnabled;
        _lightAzimuth = Math.Clamp(FiniteOrDefault(settings.LightAzimuth, -45f), -180f, 180f);
        _lightElevation = Math.Clamp(FiniteOrDefault(settings.LightElevation, 45f), -90f, 90f);
        _ambient = Math.Clamp(FiniteOrDefault(settings.LightAmbient, 0.35f), 0f, 1f);
        _intensity = Math.Clamp(FiniteOrDefault(settings.LightIntensity, 0.65f), 0f, 1f);
        _outlineEnabled = settings.OutlineEnabled;
        _outlineMode = settings.OutlineIncludeDepth ? VoxelOutlineMode.SilhouetteAndDepth : VoxelOutlineMode.Silhouette;
        _outlineColor = new AvaloniaColor(255, settings.OutlineColor.Red, settings.OutlineColor.Green, settings.OutlineColor.Blue);
        _exportDirectionCount = settings.ExportDirectionCount is 4 or 8 or 16 ? settings.ExportDirectionCount : 8;
        _exportTrueIsometric = settings.ExportTrueIsometric;
        _exportTransparentBackground = settings.ExportTransparentBackground;
        _renderStyle = BuildRenderStyle();
        CameraSummary = $"Project · Yaw {yaw:0}° · Pitch {pitch:0}°";
        ObjectRotationSummary =
            $"Object · Yaw {_rawModelYawDegrees:0}° · Pitch {_rawModelPitchDegrees:0}° · Tilt {_rawModelRollDegrees:0}°";
        UpdateExportSummary();
        foreach (string property in new[]
        {
            nameof(CurrentCameraState), nameof(CurrentModelRotation), nameof(ModelRollDegrees), nameof(BackgroundColor),
            nameof(LightingEnabled), nameof(LightAzimuth), nameof(LightElevation), nameof(Ambient),
            nameof(Intensity), nameof(OutlineEnabled), nameof(IncludeDepthOutline), nameof(OutlineColor),
            nameof(ExportDirectionCount), nameof(ExportTrueIsometric), nameof(ExportTransparentBackground),
        })
        {
            OnPropertyChanged(property);
        }
    }

    private void OnEditHistoryChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(IsProjectDirty));
        OnPropertyChanged(nameof(WindowTitle));
    }

    private bool IsInsideCurrentDocument(VoxelCoordinate coordinate)
    {
        if (_document is null) return false;
        VoxelDimensions dimensions = _document.Storage.Dimensions;
        return coordinate.X >= 0 && coordinate.X < dimensions.Width &&
            coordinate.Y >= 0 && coordinate.Y < dimensions.Height &&
            coordinate.Z >= 0 && coordinate.Z < dimensions.Depth;
    }

    private static VoxelCoordinate Offset(VoxelCoordinate coordinate, VoxelCoordinate delta) =>
        new(
            checked(coordinate.X + delta.X),
            checked(coordinate.Y + delta.Y),
            checked(coordinate.Z + delta.Z));

    private async Task RunInspectionAsync(
        Func<SixViewImportDraft> inspect,
        IReadOnlyList<RecentFaceAlignmentSettings>? restoredAlignment,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(inspect);
        long revision = Interlocked.Increment(ref _importRevision);
        _importCancellation?.Cancel();
        _importCancellation?.Dispose();
        _importCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        CancellationToken cancellationToken = _importCancellation.Token;
        StatusText = $"Inspecting PNG sources (revision {revision})...";

        try
        {
            SixViewImportDraft draft = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return inspect();
            }, cancellationToken);

            if (revision == ImportRevision && !cancellationToken.IsCancellationRequested)
            {
                ApplyImportDraft(draft, restoredAlignment);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (revision == ImportRevision)
            {
                StatusText = exception.Message;
                ImportSummary = $"Import inspection failed:{Environment.NewLine}{exception.Message}";
            }
        }
    }

    private void ApplyImportDraft(
        SixViewImportDraft draft,
        IReadOnlyList<RecentFaceAlignmentSettings>? restoredAlignment)
    {
        _importDraft = draft;
        SixViewAlignment defaults = _importer.CreateDefaultAlignment(draft);
        _suppressImportPreviewRefresh = true;
        try
        {
            foreach (ImportFaceSlotViewModel card in ImportFaceSlots)
            {
                card.ClearSource();
                card.SetAdjustment(false, false, 0, 0);
            }

            foreach (SixViewFaceAlignment assignment in defaults.Faces)
            {
                ImportFaceSlotViewModel card = ImportFaceSlots.Single(
                    item => item.TargetFace == assignment.TargetFace);
                SixViewSourceSlot source = draft.Slots.Single(
                    item => item.SourceIndex == assignment.SourceSlotIndex);
                card.AssignSource(source.SourceIndex, GetSourceDisplayName(draft, source));
            }

            if (restoredAlignment is not null)
            {
                foreach (RecentFaceAlignmentSettings saved in restoredAlignment)
                {
                    ImportFaceSlotViewModel? card = ImportFaceSlots.FirstOrDefault(
                        item => item.TargetFace == saved.TargetFace);
                    SixViewSourceSlot? source = draft.Slots.FirstOrDefault(
                        item => item.SourceIndex == saved.SourceSlotIndex);
                    if (card is null || source is null) continue;
                    ImportFaceSlotViewModel? sourceOwner = ImportFaceSlots.FirstOrDefault(
                        item => item.SourceSlotIndex == source.SourceIndex);
                    if (sourceOwner is not null && sourceOwner != card)
                    {
                        SwapImportSourcesWithoutRefresh(sourceOwner, card);
                    }

                    card.SetAdjustment(
                        saved.FlipHorizontal,
                        saved.FlipVertical,
                        saved.OffsetX,
                        saved.OffsetY);
                }
            }
        }
        finally
        {
            _suppressImportPreviewRefresh = false;
        }

        IsImportDraftLoaded = true;
        RefreshAlignmentPreview();
        StatusText = CanApplyImport
            ? "PNG inspection complete. Review alignment and choose Apply / Reconstruct."
            : "PNG inspection complete with blocking diagnostics.";
    }

    private void RefreshAlignmentPreview()
    {
        if (_suppressImportPreviewRefresh || _importDraft is null) return;
        _alignmentPreview = _importer.PreviewAlignment(_importDraft, BuildCurrentAlignment());
        CanApplyImport = _alignmentPreview.CanApply;

        foreach (ImportFaceSlotViewModel card in ImportFaceSlots)
        {
            ImportDiagnostic? diagnostic = _alignmentPreview.Diagnostics
                .Where(item =>
                    item.Face == card.TargetFace ||
                    (card.HasSource && item.SourceSlotIndex == card.SourceSlotIndex))
                .OrderByDescending(item => item.Severity)
                .FirstOrDefault();
            _alignmentPreview.Views.TryGetValue(card.TargetFace, out OrthographicImage? image);
            SixViewSlotInfo? statistics = _alignmentPreview.Slots.FirstOrDefault(
                item => item.Face == card.TargetFace);
            string text = diagnostic?.Message ??
                (statistics is null
                    ? "No transformed view"
                    : $"Ready · {statistics.OpaquePixelCount:N0} opaque");
            card.SetPreview(
                image is null ? null : CreateImportPreviewBitmap(image),
                text,
                diagnostic?.Severity);
        }

        string diagnosticLines = _alignmentPreview.Diagnostics.Count == 0
            ? "No import diagnostics."
            : string.Join(
                Environment.NewLine,
                _alignmentPreview.Diagnostics.Take(16).Select(item =>
                    $"[{item.Severity}] {item.Message}"));
        ImportSummary =
            $"Draft: {Path.GetFileName(_importDraft.SourcePath)}{Environment.NewLine}" +
            $"Sources: {_importDraft.Slots.Count}/6{Environment.NewLine}" +
            $"Apply ready: {CanApplyImport}{Environment.NewLine}{Environment.NewLine}" +
            diagnosticLines;
    }

    private SixViewAlignment BuildCurrentAlignment() =>
        new(ImportFaceSlots
            .Where(card => card.HasSource)
            .Select(card => new SixViewFaceAlignment(
                card.SourceSlotIndex,
                card.TargetFace,
                card.FlipHorizontal,
                card.FlipVertical,
                card.OffsetX,
                card.OffsetY)));

    private RecentFaceAlignmentSettings[] CaptureAlignmentSettings() =>
        ImportFaceSlots
            .Where(card => card.HasSource)
            .Select(card => new RecentFaceAlignmentSettings
            {
                SourceSlotIndex = card.SourceSlotIndex,
                TargetFace = card.TargetFace,
                FlipHorizontal = card.FlipHorizontal,
                FlipVertical = card.FlipVertical,
                OffsetX = card.OffsetX,
                OffsetY = card.OffsetY,
            })
            .ToArray();

    private void OnImportFaceAlignmentChanged(object? sender, EventArgs e) =>
        RefreshAlignmentPreview();

    private static string GetSourceDisplayName(
        SixViewImportDraft draft,
        SixViewSourceSlot source) =>
        draft.SourceKind == SixViewImportSourceKind.HorizontalSheet
            ? $"Slot {source.SourceIndex + 1}"
            : Path.GetFileName(source.SourcePath);

    private static void SwapImportSourcesWithoutRefresh(
        ImportFaceSlotViewModel first,
        ImportFaceSlotViewModel second)
    {
        int firstIndex = first.SourceSlotIndex;
        string firstName = first.SourceName;
        if (second.HasSource) first.AssignSource(second.SourceSlotIndex, second.SourceName);
        else first.ClearSource();
        if (firstIndex >= 0) second.AssignSource(firstIndex, firstName);
        else second.ClearSource();
    }

    private async Task RunExportAsync(
        Func<CancellationToken, Task<SpriteExportResult>> export,
        CancellationToken token)
    {
        _exportCancellation?.Cancel();
        _exportCancellation?.Dispose();
        _exportCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        CancellationToken cancellationToken = _exportCancellation.Token;
        StatusText = "Rendering and writing sprite output...";

        try
        {
            SpriteExportResult result = await export(cancellationToken);
            StatusText = result.JsonPath is null
                ? $"Exported {result.Width}×{result.Height} PNG: {Path.GetFileName(result.PngPath)}"
                : $"Exported {result.FrameCount} frames: {Path.GetFileName(result.PngPath)} + JSON";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Sprite export cancelled.";
        }
        catch (Exception exception)
        {
            StatusText = $"Sprite export failed: {exception.Message}";
        }
    }

    private async Task RunImportAsync(
        Func<CancellationToken, SixViewImportResult> import,
        CancellationToken token)
    {
        long revision = Interlocked.Increment(ref _importRevision);
        _importCancellation?.Cancel();
        _importCancellation?.Dispose();
        _importCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        CancellationToken cancellationToken = _importCancellation.Token;
        StatusText = $"Importing revision {revision}...";
        ViewportMessage = "Reconstructing six-view pixels...";

        try
        {
            ImportWorkResult result = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                SixViewImportResult imported = import(cancellationToken);
                VoxelDocument document = _reconstructor.Reconstruct(imported.Views);
                VoxelMeshBuildResult meshResult = _mesher.Build(document, cancellationToken);
                return new ImportWorkResult(imported, document, meshResult);
            }, cancellationToken);

            if (revision == ImportRevision && !cancellationToken.IsCancellationRequested)
            {
                ApplyImport(result);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (revision == ImportRevision) ReportError(exception.Message);
        }
    }

    private void ApplyImport(ImportWorkResult result)
    {
        _meshCancellation?.Cancel();
        _document = result.Document;
        _sourceViews = result.Import.Views;
        _mesh = result.MeshResult.Mesh;
        _projectPath = null;
        _editHistory.Clear();
        ClearSelection();
        RefreshEditorOverlay(render: false);
        OnPropertyChanged(nameof(CurrentDocument));
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(ProjectPath));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CanExport));
        _rawModelYawDegrees = 0f;
        _rawModelPitchDegrees = 0f;
        _rawModelRollDegrees = 0f;
        _modelRotation = VoxelModelRotationState.Identity;
        ObjectRotationSummary = "Object · Yaw 0° · Pitch 0° · Tilt 0°";
        OnPropertyChanged(nameof(CurrentModelRotation));
        SixViewSlotInfo sourceSlot = result.Import.Slots[0];
        _sourcePixelWidth = sourceSlot.Width;
        _sourcePixelHeight = sourceSlot.Height;
        VoxelDimensions dimensions = result.Document.Storage.Dimensions;
        ResizeWidth = dimensions.Width;
        ResizeHeight = dimensions.Height;
        ResizeDepth = dimensions.Depth;
        _renderLayout = _layoutResolver.Resolve(dimensions, _sourcePixelWidth, _sourcePixelHeight);
        AddCurrentImportToRecent();
        string slotLines = string.Join(
            Environment.NewLine,
            result.Import.Slots.Select(slot =>
                $"{slot.Face}: {slot.Width}×{slot.Height}, {slot.OpaquePixelCount:N0} opaque"));
        long candidateCount = checked((long)dimensions.Width * dimensions.Height * dimensions.Depth);
        ImportSummary =
            $"Source: {Path.GetFileName(result.Import.SourcePath)}{Environment.NewLine}" +
            $"Logical canvas: {_sourcePixelWidth} × {_sourcePixelHeight}{Environment.NewLine}" +
            $"{slotLines}{Environment.NewLine}{Environment.NewLine}" +
            $"Volume: {dimensions.Width} × {dimensions.Height} × {dimensions.Depth}{Environment.NewLine}" +
            $"Candidates: {candidateCount:N0}{Environment.NewLine}" +
            $"Occupied: {result.Document.Storage.OccupiedCount:N0}{Environment.NewLine}" +
            $"Exposed faces: {result.MeshResult.ActualExposedFaceCount:N0}";

        if (!result.MeshResult.IsSuccess)
        {
            _renderTransform = null;
            ViewportBitmap = null;
            ViewportMessage = result.MeshResult.Diagnostic ?? "The render cache could not be created.";
            StatusText = "Reconstruction complete; render-cache limit exceeded";
            RenderStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        ViewportMessage = string.Empty;
        LoadSavedDefault();
        StatusText = $"Loaded {result.Document.Storage.OccupiedCount:N0} voxels";
    }

    private void RenderCurrentScene()
    {
        if (_mesh is null || _renderLayout is null) return;
        VoxelMeshData renderMesh = _displayMesh ?? _mesh;
        PixelRenderSettings settings = new(
            _renderLayout.Width,
            _renderLayout.Height,
            1,
            _renderStyle.Background);
        _renderTransform = _transformResolver.Resolve(
            _camera,
            _modelRotation,
            _mesh.Dimensions,
            settings);

        if (IsCpuFallbackActive)
        {
            PixelFramebuffer frame = _pixelArtRasterizer.Render(
                renderMesh,
                _renderTransform,
                _renderLayout,
                _renderStyle);
            ViewportBitmap = CreateBitmap(frame);
        }
        else
        {
            ViewportBitmap = null;
        }

        RenderStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyFreeCamera()
    {
        float yaw = VoxelCameraMotion.Snap(_rawYawDegrees);
        float pitch = VoxelCameraMotion.Snap(_rawPitchDegrees);
        bool faceSnapped = false;
        if (CameraFaceSnapEnabled)
        {
            VoxelCameraFaceSnap snap = VoxelCameraMotion.SnapToFace(yaw, pitch);
            yaw = snap.YawDegrees;
            pitch = snap.PitchDegrees;
            faceSnapped = snap.IsSnapped;
        }

        _isCameraFaceSnapped = faceSnapped;

        if (_camera.Mode == VoxelViewMode.FreeView &&
            _camera.YawDegrees == yaw &&
            _camera.PitchDegrees == pitch)
        {
            return;
        }

        _camera = _camera with
        {
            Mode = VoxelViewMode.FreeView,
            Preset = VoxelCameraPreset.Free,
            YawDegrees = yaw,
            PitchDegrees = pitch,
        };
        CameraSummary = faceSnapped
            ? $"Free View · Yaw {yaw:0}° · Pitch {pitch:0}° · Face Snap"
            : $"Free View · Yaw {yaw:0}° · Pitch {pitch:0}°";
        RenderCurrentScene();
    }

    private void ApplyModelRotation()
    {
        float yaw = VoxelCameraMotion.Snap(_rawModelYawDegrees);
        float pitch = VoxelCameraMotion.Snap(_rawModelPitchDegrees);
        float roll = VoxelCameraMotion.Snap(_rawModelRollDegrees);
        if (_modelRotation.YawDegrees == yaw &&
            _modelRotation.PitchDegrees == pitch &&
            _modelRotation.RollDegrees == roll)
        {
            return;
        }

        _modelRotation = new VoxelModelRotationState(yaw, pitch, roll);
        ObjectRotationSummary = $"Object · Yaw {yaw:0}° · Pitch {pitch:0}° · Tilt {roll:0}°";
        OnPropertyChanged(nameof(CurrentModelRotation));
        OnPropertyChanged(nameof(ModelRollDegrees));
        RenderCurrentScene();
    }

    private void ResetModelRotationAxis(bool resetYaw, bool resetPitch)
    {
        if (resetYaw)
        {
            _rawModelYawDegrees = 0f;
        }

        if (resetPitch)
        {
            _rawModelPitchDegrees = 0f;
        }

        ApplyModelRotation();
    }

    private void SetCamera(float yaw, float pitch, VoxelViewMode mode, VoxelCameraPreset preset)
    {
        _rawYawDegrees = yaw;
        _rawPitchDegrees = pitch;
        _isCameraFaceSnapped = false;
        _camera = _camera with
        {
            Mode = mode,
            Preset = preset,
            YawDegrees = yaw,
            PitchDegrees = pitch,
        };
        RenderCurrentScene();
    }

    private void VisualStyleChanged()
    {
        _renderStyle = BuildRenderStyle();
        SaveSettings();
        RenderCurrentScene();
    }

    private void AddCurrentImportToRecent()
    {
        if (_importDraft is null) return;
        string[] paths = _importDraft.Slots
            .Select(slot => slot.SourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        RecentImportSettings recent = new()
        {
            SourceKind = _importDraft.SourceKind,
            Paths = paths,
            Alignments = CaptureAlignmentSettings(),
        };
        string identity = GetRecentIdentity(recent);
        RecentImports = new[] { recent }
            .Concat(_recentImports.Where(item => GetRecentIdentity(item) != identity))
            .Take(10)
            .ToArray();
        SelectedRecentImport = RecentImports.FirstOrDefault();
        SaveSettings();
    }

    private static string GetRecentIdentity(RecentImportSettings recent) =>
        $"{recent.SourceKind}:{string.Join('|', recent.Paths.Select(path => path.Trim().ToUpperInvariant()))}";

    private void UpdateExportSummary()
    {
        string camera = _exportTrueIsometric ? "True Isometric" : "Pixel 2:1";
        string background = _exportTransparentBackground ? "Transparent" : "Viewport background";
        ExportSummary = $"{_exportDirectionCount} directions · {camera} · {background}";
    }

    private VoxelRenderStyle BuildRenderStyle() =>
        new(
            ToRgba(_backgroundColor),
            new VoxelLightingSettings(
                _lightingEnabled,
                _lightAzimuth,
                _lightElevation,
                _ambient,
                _intensity),
            new VoxelOutlineSettings(
                _outlineEnabled,
                _outlineMode,
                ToRgba(_outlineColor),
                0.5f));

    private void SaveSettings() =>
        _settingsStore.SaveDebounced(new ViewportUserSettings
        {
            DefaultYaw = _savedDefaultYaw,
            DefaultPitch = _savedDefaultPitch,
            CameraFaceSnapEnabled = _cameraFaceSnapEnabled,
            ZoomIsFit = !_manualZoomScale.HasValue,
            ManualZoomScale = _manualZoomScale ?? 6,
            AnimationSpeed = _animationSpeed,
            LightingEnabled = _lightingEnabled,
            LightAzimuth = _lightAzimuth,
            LightElevation = _lightElevation,
            Ambient = _ambient,
            Intensity = _intensity,
            OutlineEnabled = _outlineEnabled,
            OutlineMode = _outlineMode,
            OutlineColor = ToHex(_outlineColor),
            BackgroundColor = ToHex(_backgroundColor),
            ExportDirectionCount = _exportDirectionCount,
            ExportTrueIsometric = _exportTrueIsometric,
            ExportTransparentBackground = _exportTransparentBackground,
            RecentImports = _recentImports,
        });

    private string GetSeparatePath(VoxelFace face) =>
        _separatePaths.TryGetValue(face, out string? path)
            ? Path.GetFileName(path)
            : "Not selected";

    private static string GetPathPropertyName(VoxelFace face) => $"{face}Path";

    private static string GetPresetName(VoxelCameraPreset preset) => preset switch
    {
        VoxelCameraPreset.Pixel2To1 => "Pixel 2:1",
        VoxelCameraPreset.TrueIsometric => "True Isometric",
        VoxelCameraPreset.Front => "Front",
        VoxelCameraPreset.Right => "Right",
        VoxelCameraPreset.Top => "Top",
        _ => "Free",
    };

    private static float FiniteOrDefault(float value, float fallback) =>
        float.IsFinite(value) ? value : fallback;

    private static Rgba32Color ToRgba(AvaloniaColor color) =>
        new(color.R, color.G, color.B, 255);

    private static string ToHex(AvaloniaColor color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static AvaloniaColor ParseColor(string? value, AvaloniaColor fallback)
    {
        if (value is { Length: 7 } && value[0] == '#' &&
            byte.TryParse(value.AsSpan(1, 2), System.Globalization.NumberStyles.HexNumber, null, out byte red) &&
            byte.TryParse(value.AsSpan(3, 2), System.Globalization.NumberStyles.HexNumber, null, out byte green) &&
            byte.TryParse(value.AsSpan(5, 2), System.Globalization.NumberStyles.HexNumber, null, out byte blue))
        {
            return new AvaloniaColor(255, red, green, blue);
        }

        return fallback;
    }

    private static string? GetInvalidSettingsDiagnostic(ViewportUserSettings settings)
    {
        bool invalid =
            !float.IsFinite(settings.DefaultYaw) ||
            !float.IsFinite(settings.DefaultPitch) ||
            settings.ManualZoomScale is < 1 or > 16 ||
            !float.IsFinite(settings.AnimationSpeed) || settings.AnimationSpeed is < 5f or > 180f ||
            !float.IsFinite(settings.LightAzimuth) || settings.LightAzimuth is < -180f or > 180f ||
            !float.IsFinite(settings.LightElevation) || settings.LightElevation is < -90f or > 90f ||
            !float.IsFinite(settings.Ambient) || settings.Ambient is < 0f or > 1f ||
            !float.IsFinite(settings.Intensity) || settings.Intensity is < 0f or > 1f ||
            settings.ExportDirectionCount is not (4 or 8 or 16) ||
            !Enum.IsDefined(settings.OutlineMode) ||
            !IsOpaqueHexColor(settings.OutlineColor) ||
            !IsOpaqueHexColor(settings.BackgroundColor);
        return invalid
            ? "Some viewport settings were invalid and have been reset to safe values."
            : null;
    }

    private static bool IsOpaqueHexColor(string? value) =>
        value is { Length: 7 } &&
        value[0] == '#' &&
        int.TryParse(value.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out _);

    private static WriteableBitmap CreateImportPreviewBitmap(OrthographicImage image)
    {
        Rgba32Color[] pixels = new Rgba32Color[checked(image.Width * image.Height)];
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Rgba32Color source = image.GetPixel(x, y);
                pixels[(y * image.Width) + x] = source.Alpha is > 0 and < byte.MaxValue
                    ? new Rgba32Color(255, 0, 255, 255)
                    : source;
            }
        }

        return CreateBitmap(image.Width, image.Height, pixels);
    }

    private static WriteableBitmap CreateBitmap(PixelFramebuffer frame) =>
        CreateBitmap(frame.Width, frame.Height, frame.Pixels.Span);

    private static WriteableBitmap CreateBitmap(
        int width,
        int height,
        ReadOnlySpan<Rgba32Color> pixels)
    {
        WriteableBitmap bitmap = new(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Unpremul);
        byte[] bytes = new byte[checked(width * height * 4)];
        for (int index = 0; index < pixels.Length; index++)
        {
            int byteIndex = index * 4;
            bytes[byteIndex] = pixels[index].Red;
            bytes[byteIndex + 1] = pixels[index].Green;
            bytes[byteIndex + 2] = pixels[index].Blue;
            bytes[byteIndex + 3] = pixels[index].Alpha;
        }

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        int sourceRowBytes = width * 4;
        for (int row = 0; row < height; row++)
        {
            Marshal.Copy(
                bytes,
                row * sourceRowBytes,
                IntPtr.Add(framebuffer.Address, row * framebuffer.RowBytes),
                sourceRowBytes);
        }

        return bitmap;
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

    private sealed record ImportWorkResult(
        SixViewImportResult Import,
        VoxelDocument Document,
        VoxelMeshBuildResult MeshResult);
}
