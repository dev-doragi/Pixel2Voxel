using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;
using Avalonia;
using Avalonia.Controls;
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
    private readonly AsepriteAnimationImporter _animationImporter;
    private readonly IVoxelReconstructor _reconstructor;
    private readonly VoxelSurfaceMesher _mesher;
    private readonly PixelArtVoxelRasterizer _pixelArtRasterizer;
    private readonly VoxelRenderTransformResolver _transformResolver;
    private readonly PixelRenderLayoutResolver _layoutResolver;
    private readonly ViewportSettingsStore _settingsStore;
    private readonly SpriteExportCoordinator _spriteExportCoordinator;
    private readonly IProjectSerializer _projectSerializer;
    private readonly GifAnimationExporter _gifExporter;
    private readonly IObjExporter _objExporter;
    private readonly VoxelPicker _voxelPicker;
    private readonly VoxelEditHistory _editHistory = new();
    private readonly Dictionary<VoxelFace, string> _separatePaths = [];
    private readonly Dictionary<VoxelFace, AsepriteFaceAnimationSource> _animatedFaceSources = [];
    private readonly Dictionary<(VoxelFace Face, int X, int Y), Rgba32Color> _sourceMaskEdits = [];
    private readonly List<ProjectFrameState> _projectFrames = [];
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
    private string _importSummary = "No orthographic input loaded.";
    private string _statusText = "Ready";
    private string _viewportMessage = "Import a 6×1 sheet or assign six PNG files.";
    private string _cameraSummary = "Pixel Preview · Pixel 2:1";
    private string _objectRotationSummary = "Object · Yaw 0° · Pitch 0° · Roll 0°";
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
    private float _cameraFaceSnapAngle;
    private bool _horizontalAnimationEnabled;
    private bool _verticalAnimationEnabled;
    private bool _rollAnimationEnabled;
    private bool _animationPreviewPlaying;
    private VoxelModelRotationState _animationBaseRotation = VoxelModelRotationState.Identity;
    private double _animationElapsedSeconds;
    private int _currentFrameIndex;
    private bool _timelinePlaying;
    private double _timelineElapsedMilliseconds;
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
    private bool _openGlUnavailable;
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
    private int _unobservedXLength = 1;
    private int _unobservedYLength = 1;
    private int _unobservedZLength = 1;
    private long _meshRevision;
    private long _importRevision;
    private bool _paletteDirty;
    private bool _projectSettingsDirty;
    private int _animationFramesPerSecond = 12;
    private int _gifExportResizePercent = 400;
    private bool _leftPanelVisible = true;
    private bool _rightPanelVisible = true;
    private double _leftPanelWidth = 360;
    private double _rightPanelWidth = 320;
    private string _inspectorSearchText = string.Empty;
    private bool _editCategoryExpanded = true;
    private bool _cameraCategoryExpanded = true;
    private bool _animationCategoryExpanded = true;
    private bool _renderingCategoryExpanded = true;
    private bool _exportCategoryExpanded = true;
    private WorkspaceMode _activeWorkspace = WorkspaceMode.Import;
    private ImportWorkflowStep _activeImportStep = ImportWorkflowStep.Source;
    private ImportFaceSlotViewModel? _selectedImportFaceSlot;
    private ExportWorkflowMode _selectedExportMode = ExportWorkflowMode.DirectionSheet;
    private double _responsiveWindowWidth = 1360;

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
        VoxelPicker? voxelPicker = null,
        GifAnimationExporter? gifExporter = null,
        IObjExporter? objExporter = null,
        AsepriteAnimationImporter? animationImporter = null)
    {
        _importer = importer;
        _animationImporter = animationImporter ?? new AsepriteAnimationImporter();
        _reconstructor = reconstructor;
        _mesher = mesher;
        _pixelArtRasterizer = pixelArtRasterizer;
        _transformResolver = transformResolver;
        _layoutResolver = layoutResolver;
        _settingsStore = settingsStore;
        _spriteExportCoordinator = spriteExportCoordinator;
        _projectSerializer = projectSerializer ?? new PxvProjectSerializer(new PngPixelWriter(), new PngPixelReader());
        _voxelPicker = voxelPicker ?? new VoxelPicker();
        _gifExporter = gifExporter ?? new GifAnimationExporter();
        _objExporter = objExporter ?? new UnityObjExporter(new PngPixelWriter());
        _editHistory.Changed += OnEditHistoryChanged;

        ImportFaceSlots = new ObservableCollection<ImportFaceSlotViewModel>(
            TargetFaceOrder.Select(face => new ImportFaceSlotViewModel(face)));
        foreach (ImportFaceSlotViewModel slot in ImportFaceSlots)
        {
            slot.AlignmentChanged += OnImportFaceAlignmentChanged;
        }
        ProjectPalette = [];

        (ViewportUserSettings settings, string? diagnostic) = _settingsStore.Load();
        diagnostic ??= GetInvalidSettingsDiagnostic(settings);
        _savedDefaultYaw = VoxelCameraMotion.WrapAngle(FiniteOrDefault(settings.DefaultYaw, -45f));
        _savedDefaultPitch = VoxelCameraMotion.WrapAngle(FiniteOrDefault(settings.DefaultPitch, -30f));
        _rawYawDegrees = _savedDefaultYaw;
        _rawPitchDegrees = _savedDefaultPitch;
        _cameraFaceSnapEnabled = settings.CameraFaceSnapEnabled;
        _cameraFaceSnapAngle = Math.Clamp(FiniteOrDefault(settings.CameraFaceSnapAngle, 10f), 1f, 30f);
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
        _animationFramesPerSecond = Math.Clamp(settings.AnimationFramesPerSecond, 1, 60);
        _gifExportResizePercent = Math.Clamp(settings.GifExportResizePercent, 25, 1000);
        _leftPanelVisible = settings.LeftPanelVisible;
        _rightPanelVisible = settings.RightPanelVisible;
        _leftPanelWidth = Math.Clamp(settings.LeftPanelWidth, 260, 600);
        _rightPanelWidth = Math.Clamp(settings.RightPanelWidth, 280, 600);
        _editCategoryExpanded = settings.EditCategoryExpanded;
        _cameraCategoryExpanded = settings.CameraCategoryExpanded;
        _animationCategoryExpanded = settings.AnimationCategoryExpanded;
        _renderingCategoryExpanded = settings.RenderingCategoryExpanded;
        _exportCategoryExpanded = settings.ExportCategoryExpanded;
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

    public ObservableCollection<AvaloniaColor> ProjectPalette { get; }

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
    public bool IsProjectDirty => _document is not null &&
        (_projectPath is null || _editHistory.IsDirty || _paletteDirty || _projectSettingsDirty);

    /// <summary>Gets whether the most recent edit can be undone.</summary>
    public bool CanUndo => _editHistory.CanUndo;

    /// <summary>Gets whether an undone edit can be reapplied.</summary>
    public bool CanRedo => _editHistory.CanRedo;

    /// <summary>Gets the current project path when it has been saved.</summary>
    public string? ProjectPath => _projectPath;

    /// <summary>Gets the window title including the unsaved marker.</summary>
    public string WindowTitle => $"Pixel2Voxel{(_projectPath is null ? string.Empty : $" — {Path.GetFileName(_projectPath)}")}{(IsProjectDirty ? " *" : string.Empty)}";

    public WorkspaceMode ActiveWorkspace
    {
        get => _activeWorkspace;
        set
        {
            if (!SetField(ref _activeWorkspace, value)) return;
            foreach (string property in new[]
            {
                nameof(IsImportWorkspace), nameof(IsEditWorkspace),
                nameof(IsAnimateWorkspace), nameof(IsExportWorkspace),
                nameof(IsViewportWorkspace), nameof(WorkspaceTitle),
                nameof(InspectorTabIndex),
            }) OnPropertyChanged(property);
        }
    }

    public bool IsImportWorkspace => ActiveWorkspace == WorkspaceMode.Import;
    public bool IsEditWorkspace => ActiveWorkspace == WorkspaceMode.Edit;
    public bool IsAnimateWorkspace => ActiveWorkspace == WorkspaceMode.Animate;
    public bool IsExportWorkspace => ActiveWorkspace == WorkspaceMode.Export;
    public bool IsViewportWorkspace => ActiveWorkspace != WorkspaceMode.Import;
    public string WorkspaceTitle => ActiveWorkspace switch
    {
        WorkspaceMode.Import => "Import",
        WorkspaceMode.Edit => "Voxel Edit",
        WorkspaceMode.Animate => "Animation",
        WorkspaceMode.Export => "Export",
        _ => "Workspace",
    };
    public int InspectorTabIndex => ActiveWorkspace switch
    {
        WorkspaceMode.Animate => 2,
        WorkspaceMode.Export => 4,
        _ => 0,
    };

    public ImportWorkflowStep ActiveImportStep
    {
        get => _activeImportStep;
        set
        {
            if (value != ImportWorkflowStep.Source && !IsImportDraftLoaded) return;
            if (value == ImportWorkflowStep.Reconstruct && !CanApplyImport) return;
            if (!SetField(ref _activeImportStep, value)) return;
            foreach (string property in new[]
            {
                nameof(IsImportSourceStep), nameof(IsImportMapStep),
                nameof(IsImportValidateStep), nameof(IsImportReconstructStep),
                nameof(CanGoToPreviousImportStep), nameof(CanGoToNextImportStep),
                nameof(NextImportStepLabel),
            }) OnPropertyChanged(property);
        }
    }

    public bool IsImportSourceStep => ActiveImportStep == ImportWorkflowStep.Source;
    public bool IsImportMapStep => ActiveImportStep == ImportWorkflowStep.MapAndAlign;
    public bool IsImportValidateStep => ActiveImportStep == ImportWorkflowStep.Validate;
    public bool IsImportReconstructStep => ActiveImportStep == ImportWorkflowStep.Reconstruct;
    public bool CanOpenImportReview => IsImportDraftLoaded;
    public bool CanGoToPreviousImportStep => ActiveImportStep != ImportWorkflowStep.Source;
    public bool CanGoToNextImportStep => ActiveImportStep switch
    {
        ImportWorkflowStep.Source => IsImportDraftLoaded,
        ImportWorkflowStep.MapAndAlign => IsImportDraftLoaded,
        ImportWorkflowStep.Validate => CanApplyImport,
        _ => false,
    };
    public string NextImportStepLabel => ActiveImportStep switch
    {
        ImportWorkflowStep.Source => "Review mapping",
        ImportWorkflowStep.MapAndAlign => "Validate",
        ImportWorkflowStep.Validate => "Continue to build",
        _ => "Complete",
    };
    public string ImportValidationStatus => !IsImportDraftLoaded
        ? "Choose a source to continue"
        : CanApplyImport
            ? "Ready · selected views passed validation"
            : "Blocked · resolve the highlighted source issues";

    public ImportFaceSlotViewModel? SelectedImportFaceSlot
    {
        get => _selectedImportFaceSlot;
        set => SetField(ref _selectedImportFaceSlot, value);
    }

    public void MoveToPreviousImportStep()
    {
        if (!CanGoToPreviousImportStep) return;
        ActiveImportStep = (ImportWorkflowStep)((int)ActiveImportStep - 1);
    }

    public void MoveToNextImportStep()
    {
        if (!CanGoToNextImportStep) return;
        ActiveImportStep = (ImportWorkflowStep)((int)ActiveImportStep + 1);
    }

    public ExportWorkflowMode SelectedExportMode
    {
        get => _selectedExportMode;
        set
        {
            if (!SetField(ref _selectedExportMode, value)) return;
            foreach (string property in new[]
            {
                nameof(IsCurrentViewExport), nameof(IsDirectionSheetExport),
                nameof(IsAnimatedGifExport), nameof(IsAnimationSheetExport),
                nameof(IsTrimmedAtlasExport),
                nameof(IsUnityObjExport), nameof(PrimaryExportLabel), nameof(CanPrimaryExport),
            }) OnPropertyChanged(property);
        }
    }
    public bool IsCurrentViewExport => SelectedExportMode == ExportWorkflowMode.CurrentView;
    public bool IsDirectionSheetExport => SelectedExportMode == ExportWorkflowMode.DirectionSheet;
    public bool IsAnimatedGifExport => SelectedExportMode == ExportWorkflowMode.AnimatedGif;
    public bool IsAnimationSheetExport => SelectedExportMode == ExportWorkflowMode.AnimationSheet;
    public bool IsTrimmedAtlasExport => SelectedExportMode == ExportWorkflowMode.TrimmedAtlas;
    public bool IsUnityObjExport => SelectedExportMode == ExportWorkflowMode.UnityObj;
    public string PrimaryExportLabel => SelectedExportMode switch
    {
        ExportWorkflowMode.CurrentView => "Export current view...",
        ExportWorkflowMode.DirectionSheet => "Export direction sheet...",
        ExportWorkflowMode.AnimatedGif => "Export animated GIF...",
        ExportWorkflowMode.AnimationSheet => "Export animation sheet...",
        ExportWorkflowMode.TrimmedAtlas => "Export trimmed atlas...",
        ExportWorkflowMode.UnityObj => "Export Unity OBJ package...",
        _ => "Export...",
    };

    public double ResponsiveWindowWidth
    {
        get => _responsiveWindowWidth;
        set
        {
            if (!SetField(ref _responsiveWindowWidth, value)) return;
            OnPropertyChanged(nameof(LeftPanelGridWidth));
            OnPropertyChanged(nameof(RightPanelGridWidth));
        }
    }

    public VoxelEditTool SelectedEditTool
    {
        get => _selectedEditTool;
        set
        {
            if (!SetField(ref _selectedEditTool, value)) return;
            _editStroke.Clear();
            _hoverPick = null;
            OnPropertyChanged(nameof(IsViewMode));
            foreach (string property in new[]
            {
                nameof(IsViewTool), nameof(IsAddTool), nameof(IsEraseTool),
                nameof(IsPaintTool), nameof(IsEyedropperTool), nameof(IsSelectTool),
                nameof(ActiveToolTitle), nameof(ActiveToolHint),
            }) OnPropertyChanged(property);
            RefreshEditorOverlay();
        }
    }

    /// <summary>Gets whether left-button viewport input navigates instead of editing.</summary>
    public bool IsViewMode => SelectedEditTool == VoxelEditTool.View;

    public string ActiveToolTitle => SelectedEditTool switch
    {
        VoxelEditTool.View => "View",
        VoxelEditTool.Add => "Add voxel",
        VoxelEditTool.Erase => "Erase voxel",
        VoxelEditTool.Paint => "Paint faces",
        VoxelEditTool.Eyedropper => "Pick source color",
        VoxelEditTool.Select => "Box select",
        _ => "Edit",
    };

    public string ActiveToolHint => SelectedEditTool switch
    {
        VoxelEditTool.View => "Right drag orbit  ·  Middle drag pan  ·  Wheel zoom",
        VoxelEditTool.Add => "Left click or drag on a visible face",
        VoxelEditTool.Erase => "Left click or drag over voxels",
        VoxelEditTool.Paint => "Left drag to paint  ·  Shift paints all faces",
        VoxelEditTool.Eyedropper => "Click a voxel face to sample its source color",
        VoxelEditTool.Select => "Click two voxels to define a selection",
        _ => string.Empty,
    };

    public AvaloniaColor EditColor
    {
        get => _editColor;
        set
        {
            if (!SetField(ref _editColor, value)) return;
            if (SelectedEditTool == VoxelEditTool.Paint && (_hoverPick is not null || _editStroke.Count > 0))
            {
                RefreshEditorOverlay();
            }
        }
    }

    public bool LeftPanelVisible
    {
        get => _leftPanelVisible;
        set
        {
            if (!SetField(ref _leftPanelVisible, value)) return;
            OnPropertyChanged(nameof(IsLeftPanelCollapsed));
            OnPropertyChanged(nameof(LeftPanelGridWidth));
            SaveSettings();
        }
    }
    public bool IsLeftPanelCollapsed => !LeftPanelVisible;
    public bool RightPanelVisible
    {
        get => _rightPanelVisible;
        set
        {
            if (!SetField(ref _rightPanelVisible, value)) return;
            OnPropertyChanged(nameof(RightPanelGridWidth));
            SaveSettings();
        }
    }
    public double LeftPanelWidth
    {
        get => _leftPanelWidth;
        set
        {
            double width = Math.Clamp(value, 260, 600);
            if (!SetField(ref _leftPanelWidth, width)) return;
            OnPropertyChanged(nameof(LeftPanelGridWidth));
            SaveSettings();
        }
    }
    public double RightPanelWidth
    {
        get => _rightPanelWidth;
        set
        {
            double width = Math.Clamp(value, 280, 600);
            if (!SetField(ref _rightPanelWidth, width)) return;
            OnPropertyChanged(nameof(RightPanelGridWidth));
            SaveSettings();
        }
    }
    public GridLength LeftPanelGridWidth
    {
        get => new(LeftPanelVisible && ResponsiveWindowWidth >= 820 ? LeftPanelWidth : 0, GridUnitType.Pixel);
        set
        {
            if (LeftPanelVisible && value.IsAbsolute && value.Value >= 260)
                LeftPanelWidth = value.Value;
        }
    }
    public GridLength RightPanelGridWidth
    {
        get => new(RightPanelVisible && ResponsiveWindowWidth >= 1180 ? RightPanelWidth : 0, GridUnitType.Pixel);
        set
        {
            if (RightPanelVisible && value.IsAbsolute && value.Value >= 280)
                RightPanelWidth = value.Value;
        }
    }
    public string InspectorSearchText
    {
        get => _inspectorSearchText;
        set
        {
            if (!SetField(ref _inspectorSearchText, value ?? string.Empty)) return;
            foreach (string property in new[] { nameof(ShowEditCategory), nameof(ShowCameraCategory), nameof(ShowAnimationCategory), nameof(ShowRenderingCategory), nameof(ShowExportCategory) }) OnPropertyChanged(property);
        }
    }
    public bool ShowEditCategory => MatchesInspector("edit palette voxel color selection resize eyedropper");
    public bool ShowCameraCategory => MatchesInspector("camera view zoom preset orbit");
    public bool ShowAnimationCategory => MatchesInspector("animation timeline yaw pitch roll speed fps gif sheet atlas trim");
    public bool ShowRenderingCategory => MatchesInspector("render lighting outline background color");
    public bool ShowExportCategory => MatchesInspector("export png sheet json gif obj unity atlas trim maxrects");
    public bool EditCategoryExpanded { get => _editCategoryExpanded; set { if (SetField(ref _editCategoryExpanded, value)) SaveSettings(); } }
    public bool CameraCategoryExpanded { get => _cameraCategoryExpanded; set { if (SetField(ref _cameraCategoryExpanded, value)) SaveSettings(); } }
    public bool AnimationCategoryExpanded { get => _animationCategoryExpanded; set { if (SetField(ref _animationCategoryExpanded, value)) SaveSettings(); } }
    public bool RenderingCategoryExpanded { get => _renderingCategoryExpanded; set { if (SetField(ref _renderingCategoryExpanded, value)) SaveSettings(); } }
    public bool ExportCategoryExpanded { get => _exportCategoryExpanded; set { if (SetField(ref _exportCategoryExpanded, value)) SaveSettings(); } }
    public bool IsViewTool => SelectedEditTool == VoxelEditTool.View;
    public bool IsAddTool => SelectedEditTool == VoxelEditTool.Add;
    public bool IsEraseTool => SelectedEditTool == VoxelEditTool.Erase;
    public bool IsPaintTool => SelectedEditTool == VoxelEditTool.Paint;
    public bool IsEyedropperTool => SelectedEditTool == VoxelEditTool.Eyedropper;
    public bool IsSelectTool => SelectedEditTool == VoxelEditTool.Select;

    public void AddCurrentColorToPalette()
    {
        if (_document is null) { StatusText = "Create or load a project before editing its palette."; return; }
        AvaloniaColor color = EditColor;
        if (ProjectPalette.Contains(color)) { StatusText = "That color is already in the project palette."; return; }
        if (ProjectPalette.Count >= 32) { StatusText = "The project palette is limited to 32 colors."; return; }
        ProjectPalette.Add(color);
        MarkPaletteDirty();
    }

    public void RemovePaletteColor(AvaloniaColor color)
    {
        if (ProjectPalette.Remove(color)) MarkPaletteDirty();
    }

    public void SelectPaletteColor(AvaloniaColor color) => EditColor = color;

    private bool MatchesInspector(string terms) => string.IsNullOrWhiteSpace(_inspectorSearchText) ||
        terms.Contains(_inspectorSearchText.Trim(), StringComparison.OrdinalIgnoreCase);

    private void MarkPaletteDirty()
    {
        _paletteDirty = true;
        OnPropertyChanged(nameof(IsProjectDirty));
        OnPropertyChanged(nameof(WindowTitle));
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
        private set
        {
            if (!SetField(ref _isImportDraftLoaded, value)) return;
            OnPropertyChanged(nameof(CanOpenImportReview));
            OnPropertyChanged(nameof(CanGoToNextImportStep));
            OnPropertyChanged(nameof(ImportValidationStatus));
        }
    }

    public bool CanApplyImport
    {
        get => _canApplyImport;
        private set
        {
            if (!SetField(ref _canApplyImport, value)) return;
            OnPropertyChanged(nameof(CanGoToNextImportStep));
            OnPropertyChanged(nameof(ImportValidationStatus));
        }
    }

    public bool CanExport => _mesh is not null && _renderLayout is not null;
    public bool CanExportAnimation => CanExport && (HasMultipleFrames || HasAnimationAxis);
    public bool CanPrimaryExport => SelectedExportMode is ExportWorkflowMode.AnimatedGif or ExportWorkflowMode.AnimationSheet
        ? CanExportAnimation
        : CanExport;

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

    public bool YawAnimationEnabled
    {
        get => _horizontalAnimationEnabled;
        set
        {
            if (!SetField(ref _horizontalAnimationEnabled, value)) return;
            AnimationAxisSelectionChanged();
        }
    }

    public bool PitchAnimationEnabled
    {
        get => _verticalAnimationEnabled;
        set
        {
            if (!SetField(ref _verticalAnimationEnabled, value)) return;
            AnimationAxisSelectionChanged();
        }
    }

    public bool RollAnimationEnabled
    {
        get => _rollAnimationEnabled;
        set
        {
            if (!SetField(ref _rollAnimationEnabled, value)) return;
            AnimationAxisSelectionChanged();
        }
    }

    public bool HasAnimationAxis =>
        YawAnimationEnabled || PitchAnimationEnabled || RollAnimationEnabled;

    public bool IsAnimationActive => _animationPreviewPlaying && HasAnimationAxis;
    public bool AnimationPreviewPlaying => _animationPreviewPlaying;
    public string AnimationPlayPauseLabel => _animationPreviewPlaying ? "Pause preview" : "Play preview";
    public int FrameCount => Math.Max(1, _projectFrames.Count);
    public int FrameLastIndex => FrameCount - 1;
    public int CurrentFrameIndex
    {
        get => _currentFrameIndex;
        set => SelectTimelineFrame(Math.Clamp(value, 0, FrameLastIndex));
    }
    public int CurrentFrameNumber => _currentFrameIndex + 1;
    public bool HasMultipleFrames => _projectFrames.Count > 1;
    public bool TimelinePlaying => _timelinePlaying;
    public string TimelinePlayPauseLabel => _timelinePlaying ? "Pause timeline" : "Play timeline";
    public string CurrentFrameSummary => _projectFrames.Count == 0
        ? "Frame 1 / 1 · 100 ms"
        : $"Frame {CurrentFrameNumber} / {FrameCount} · {_projectFrames[_currentFrameIndex].Name} · {_projectFrames[_currentFrameIndex].DurationMilliseconds} ms";

    public void ToggleTimelinePlayback()
    {
        if (!HasMultipleFrames)
        {
            StatusText = "Import an animation with at least two frames first.";
            return;
        }

        _timelinePlaying = !_timelinePlaying;
        _timelineElapsedMilliseconds = 0d;
        OnPropertyChanged(nameof(TimelinePlaying));
        OnPropertyChanged(nameof(TimelinePlayPauseLabel));
        StatusText = _timelinePlaying ? "Timeline playing" : "Timeline paused";
    }

    public void SelectPreviousFrame() => CurrentFrameIndex =
        _currentFrameIndex <= 0 ? FrameLastIndex : _currentFrameIndex - 1;

    public void SelectNextFrame() => CurrentFrameIndex =
        _currentFrameIndex >= FrameLastIndex ? 0 : _currentFrameIndex + 1;

    public void ToggleAnimationPreview()
    {
        if (!HasAnimationAxis)
        {
            StatusText = "Select at least one rotation axis";
            return;
        }

        _animationPreviewPlaying = !_animationPreviewPlaying;
        if (_animationPreviewPlaying)
        {
            _animationBaseRotation = _modelRotation;
            _animationElapsedSeconds = 0d;
        }
        NotifyAnimationPlaybackChanged();
        StatusText = _animationPreviewPlaying ? "Rotation preview playing" : "Rotation preview paused";
    }

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

    public float CameraFaceSnapAngle
    {
        get => _cameraFaceSnapAngle;
        set
        {
            float clamped = Math.Clamp(FiniteOrDefault(value, 10f), 1f, 30f);
            if (!SetField(ref _cameraFaceSnapAngle, clamped)) return;
            if (_camera.Mode == VoxelViewMode.FreeView) ApplyFreeCamera();
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

    public int AnimationFramesPerSecond
    {
        get => _animationFramesPerSecond;
        set { if (SetField(ref _animationFramesPerSecond, Math.Clamp(value, 1, 60))) SaveSettings(); }
    }

    public int GifExportResizePercent
    {
        get => _gifExportResizePercent;
        set { if (SetField(ref _gifExportResizePercent, Math.Clamp(value, 25, 1000))) SaveSettings(); }
    }

    public string AnimationExportCameraSummary =>
        "Current editor angle · centered · pan/zoom ignored";

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
    public int AssignedAnimationFaceCount => _animatedFaceSources.Count;
    public bool CanImportAssignedAnimation => AssignedAnimationFaceCount > 0;
    public string AssignedAnimationSummary => AssignedAnimationFaceCount == 0
        ? "No animated faces assigned"
        : $"{AssignedAnimationFaceCount} animated face(s) assigned";

    /// <summary>Gets or sets the X length used only when selected views do not observe X.</summary>
    public int UnobservedXLength
    {
        get => _unobservedXLength;
        set => SetField(ref _unobservedXLength, Math.Clamp(value, 1, 1024));
    }

    /// <summary>Gets or sets the Y length used only when selected views do not observe Y.</summary>
    public int UnobservedYLength
    {
        get => _unobservedYLength;
        set => SetField(ref _unobservedYLength, Math.Clamp(value, 1, 1024));
    }

    /// <summary>Gets or sets the Z length used only when selected views do not observe Z.</summary>
    public int UnobservedZLength
    {
        get => _unobservedZLength;
        set => SetField(ref _unobservedZLength, Math.Clamp(value, 1, 1024));
    }

    public Task LoadHorizontalSheetAsync(string path) =>
        RunInspectionAsync(() => _importer.InspectHorizontalSheet(path), null, default);

    public Task LoadSeparateFilesAsync(IEnumerable<string> paths) =>
        RunInspectionAsync(() => _importer.InspectSeparate(paths), null, default);

    public void SetAnimatedFaceSource(VoxelFace face, string pngPath, string jsonPath)
    {
        _animatedFaceSources[face] = new AsepriteFaceAnimationSource(pngPath, jsonPath);
        OnPropertyChanged(nameof(AssignedAnimationFaceCount));
        OnPropertyChanged(nameof(CanImportAssignedAnimation));
        OnPropertyChanged(nameof(AssignedAnimationSummary));
        StatusText = $"Assigned animated {face}: {Path.GetFileName(pngPath)} + {Path.GetFileName(jsonPath)}";
    }

    public Task<bool> ImportAssignedAnimationAsync(CancellationToken cancellationToken = default) =>
        LoadAsepriteAnimationAsync(new Dictionary<VoxelFace, AsepriteFaceAnimationSource>(_animatedFaceSources), cancellationToken);

    /// <summary>Imports synchronized Aseprite face animations into independently editable frames.</summary>
    public async Task<bool> LoadAsepriteAnimationAsync(
        IReadOnlyDictionary<VoxelFace, AsepriteFaceAnimationSource> sources,
        CancellationToken cancellationToken = default)
    {
        try
        {
            StatusText = "Importing synchronized Aseprite animation...";
            VoxelReconstructionOptions options = new(UnobservedXLength, UnobservedYLength, UnobservedZLength);
            (OrthographicAnimation Animation, PixelVoxelProjectFrame[] Frames, VoxelMeshBuildResult[] Meshes) result =
                await Task.Run(() =>
                {
                    OrthographicAnimation animation = _animationImporter.Import(sources);
                    PixelVoxelProjectFrame[] frames = new PixelVoxelProjectFrame[animation.Frames.Count];
                    VoxelMeshBuildResult[] meshes = new VoxelMeshBuildResult[animation.Frames.Count];
                    for (int index = 0; index < animation.Frames.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        VoxelDocument document = _reconstructor.Reconstruct(animation.Frames[index], options);
                        meshes[index] = _mesher.Build(document, cancellationToken);
                        frames[index] = new PixelVoxelProjectFrame(
                            animation.FrameNames[index],
                            animation.DurationsMilliseconds[index],
                            document,
                            animation.Frames[index]);
                    }
                    return (animation, frames, meshes);
                }, cancellationToken);

            PixelVoxelProject project = new(result.Frames, 0, CaptureProjectSettings());
            ReplaceProjectPalette([]);
            ApplyLoadedFrames(project, result.Meshes, null);
            StatusText = $"Imported {result.Frames.Length} synchronized animation frames";
            ActiveWorkspace = WorkspaceMode.Animate;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusText = "Animation import cancelled.";
            return false;
        }
        catch (Exception exception)
        {
            StatusText = $"Animation import failed: {exception.Message}";
            return false;
        }
    }

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
        SixViewAlignmentPreview preview = _alignmentPreview!;
        return RunImportAsync(
            _ => new SixViewImportResult(
                draft.SourcePath,
                new OrthographicViewSet(preview.Views),
                preview.Slots,
                preview.Diagnostics
                    .Where(item => item.Severity != ImportDiagnosticSeverity.Error)
                    .Select(item => item.Message)),
            default);
    }

    /// <summary>Paints or erases one transformed source-mask pixel before reconstruction.</summary>
    public void SetImportSourceMaskPixel(VoxelFace face, int x, int y, bool occupied)
    {
        if (_alignmentPreview is null || !_alignmentPreview.Views.TryGetValue(face, out OrthographicImage? image)) return;
        Rgba32Color color = occupied
            ? ResolveSourceMaskPaintColor(image, x, y)
            : default;
        _sourceMaskEdits[(face, x, y)] = color;
        RefreshAlignmentPreview();
    }

    public void EraseSelectedImportConflicts()
    {
        if (_alignmentPreview is null || SelectedImportFaceSlot is null) return;
        OrthographicViewSet views = new(_alignmentPreview.Views);
        VoxelDocument provisional = _reconstructor.Reconstruct(views, CurrentReconstructionOptions());
        VoxelReprojectionAnalysis analysis = new VoxelReprojectionAnalyzer().Analyze(
            views, provisional, CurrentReconstructionOptions());
        foreach (VoxelReprojectionConflict conflict in analysis.Conflicts.Where(item =>
                     item.Face == SelectedImportFaceSlot.TargetFace && item.SourceOccupied))
        {
            _sourceMaskEdits[(conflict.Face, conflict.X, conflict.Y)] = default;
        }
        RefreshAlignmentPreview();
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

    public async Task ExportAnimationSheetAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_mesh is null || _renderLayout is null) { StatusText = "Load and reconstruct a model before exporting."; return; }
        try
        {
            StatusText = HasMultipleFrames ? "Rendering project timeline sheet..." : "Rendering rotation animation sheet...";
            SpriteExportResult result = HasMultipleFrames
                ? await _spriteExportCoordinator.ExportTimelineSheetAsync(
                    path, GetTimelineRenderFrames(), _camera, _modelRotation,
                    ResolveTimelineExportLayout(), _renderStyle, _exportTransparentBackground, cancellationToken)
                : await _spriteExportCoordinator.ExportRotationSheetAsync(path,
                    _animationFramesPerSecond, _animationSpeed, YawAnimationEnabled, PitchAnimationEnabled,
                    RollAnimationEnabled, _camera, _modelRotation, _mesh, _renderLayout, _renderStyle,
                    _exportTransparentBackground, cancellationToken);
            StatusText = $"Exported {result.FrameCount} animation frames: {Path.GetFileName(result.PngPath)} + JSON";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusText = $"Animation sheet export failed: {exception.Message}";
        }
    }

    public async Task ExportAnimatedGifAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_mesh is null || _renderLayout is null) { StatusText = "Load and reconstruct a model before exporting."; return; }
        try
        {
            VoxelCameraState camera = _camera;
            VoxelModelRotationState baseRotation = _modelRotation;
            StatusText = "Rendering animated GIF...";
            SpriteFrame[] frames = HasMultipleFrames
                ? await _spriteExportCoordinator.RenderTimelineFramesAsync(
                    GetTimelineRenderFrames(), camera, baseRotation, ResolveTimelineExportLayout(),
                    _renderStyle, _exportTransparentBackground, cancellationToken)
                : await _spriteExportCoordinator.RenderRotationFramesAsync(
                    _animationFramesPerSecond, _animationSpeed, YawAnimationEnabled, PitchAnimationEnabled,
                    RollAnimationEnabled, camera, baseRotation, _mesh, _renderLayout, _renderStyle,
                    _exportTransparentBackground, cancellationToken);
            GifExportResult result = await _gifExporter.ExportAsync(
                path, frames, cancellationToken, _gifExportResizePercent);
            StatusText = result.WasQuantized
                ? $"Exported {result.FrameCount} GIF frames (colors quantized to GIF palette)."
                : $"Exported {result.FrameCount} GIF frames: {Path.GetFileName(result.GifPath)}";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusText = $"GIF export failed: {exception.Message}";
        }
    }

    public async Task ExportUnityObjPackageAsync(string directory, string baseName, CancellationToken cancellationToken = default)
    {
        if (_mesh is null) { StatusText = "Load and reconstruct a model before exporting."; return; }
        try
        {
            StatusText = "Writing Unity OBJ package...";
            ObjExportResult result = await _objExporter.ExportAsync(new ObjExportRequest(directory, baseName, _mesh), cancellationToken);
            StatusText = $"Exported {result.FaceCount:N0} faces and {result.PaletteColorCount} palette colors: {Path.GetFileName(result.ObjPath)}";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusText = $"Unity OBJ export failed: {exception.Message}";
        }
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

    /// <summary>Pans the orthographic camera in framebuffer pixels.</summary>
    public void Pan(float deltaX, float deltaY)
    {
        if (!float.IsFinite(deltaX) || !float.IsFinite(deltaY)) return;
        _camera = _camera with
        {
            PanX = _camera.PanX + deltaX,
            PanY = _camera.PanY + deltaY,
        };
        RenderCurrentScene();
    }

    /// <summary>Rotates the object around a local gizmo axis or the current view axis.</summary>
    public void RotateObject(RotationGizmoAxis axis, float degrees)
    {
        if (axis == RotationGizmoAxis.None || !float.IsFinite(degrees) || degrees == 0f) return;
        _modelRotation = axis switch
        {
            RotationGizmoAxis.LocalX => _modelRotation.RotateLocal(Vector3.UnitX, degrees),
            RotationGizmoAxis.LocalY => _modelRotation.RotateLocal(Vector3.UnitY, degrees),
            RotationGizmoAxis.LocalZ => _modelRotation.RotateLocal(Vector3.UnitZ, degrees),
            RotationGizmoAxis.View => RotateAroundViewAxis(_modelRotation, degrees),
            _ => _modelRotation,
        };
        SyncModelRotationDisplay();
    }

    /// <summary>Applies one deterministic gizmo drag from its captured starting orientation.</summary>
    public void SetObjectRotationFromDrag(
        VoxelModelRotationState start,
        RotationGizmoAxis axis,
        float totalDegrees)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (axis == RotationGizmoAxis.None || !float.IsFinite(totalDegrees)) return;
        _modelRotation = axis switch
        {
            RotationGizmoAxis.LocalX => start.RotateFixedLocal(0f, totalDegrees, 0f),
            RotationGizmoAxis.LocalY => start.RotateFixedLocal(totalDegrees, 0f, 0f),
            RotationGizmoAxis.LocalZ => start.RotateFixedLocal(0f, 0f, totalDegrees),
            RotationGizmoAxis.View => RotateAroundViewAxis(start, totalDegrees),
            _ => start,
        };
        SyncModelRotationDisplay();
    }

    /// <summary>Restores an object orientation captured before an interactive drag.</summary>
    public void RestoreObjectRotation(VoxelModelRotationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _modelRotation = state;
        SyncModelRotationDisplay();
    }

    /// <summary>Enters unrestricted orbit mode at the current camera angle.</summary>
    public void EnterFreeView()
    {
        _rawYawDegrees = _camera.YawDegrees;
        _rawPitchDegrees = _camera.PitchDegrees;
        ApplyFreeCamera();
        CameraSummary = $"Free View · Yaw {_camera.YawDegrees:0}° · Pitch {_camera.PitchDegrees:0}°";
        StatusText = "Free rotation enabled";
    }

    /// <summary>Stops rotation animation and restores the complete object-local orientation.</summary>
    public void ResetObject()
    {
        StopRotationAnimations();
        _modelRotation = VoxelModelRotationState.Identity;
        SyncModelRotationDisplay();
        StatusText = "Object rotation reset; animation stopped";
    }

    /// <summary>Restores the initial camera, fit zoom, and all interactive/animated model rotation.</summary>
    public void ResetCamera()
    {
        StopRotationAnimations();
        _modelRotation = VoxelModelRotationState.Identity;
        _rawModelYawDegrees = 0f;
        _rawModelPitchDegrees = 0f;
        _rawModelRollDegrees = 0f;
        ObjectRotationSummary = "Object · Yaw 0° · Pitch 0° · Roll 0°";
        OnPropertyChanged(nameof(CurrentModelRotation));
        OnPropertyChanged(nameof(ModelRollDegrees));

        _rawYawDegrees = -45f;
        _rawPitchDegrees = -30f;
        _camera = VoxelCameraState.Pixel2To1();
        CameraSummary = "Pixel Preview · Pixel 2:1";
        _manualZoomScale = null;
        ZoomSummary = "Fit";
        OnPropertyChanged(nameof(CurrentCameraState));
        OnPropertyChanged(nameof(ManualZoomScale));
        SaveSettings();
        RenderCurrentScene();
        StatusText = "Camera, pan, zoom, rotation, and animation reset";
    }

    /// <summary>Restores the complete initial viewport state.</summary>
    public void ResetView()
    {
        ResetCamera();
        StatusText = "View reset to initial Pixel 2:1 state";
    }

    /// <summary>Commits a visible face snap as the starting point for the next orbit drag.</summary>
    public void CommitCameraSnap()
    {
        if (!CameraFaceSnapEnabled || _camera.Mode != VoxelViewMode.FreeView) return;
        VoxelCameraFaceSnap snap = VoxelCameraMotion.SnapToSheetFace(
            _rawYawDegrees,
            _rawPitchDegrees,
            _modelRotation.Orientation,
            CameraFaceSnapAngle);
        if (!snap.IsSnapped) return;
        _rawYawDegrees = snap.YawDegrees;
        _rawPitchDegrees = snap.PitchDegrees;
        _camera = _camera with { YawDegrees = snap.YawDegrees, PitchDegrees = snap.PitchDegrees };
        CameraSummary = $"Free View · Yaw {_camera.YawDegrees:0}° · Pitch {_camera.PitchDegrees:0}° · Face Aligned";
        RenderCurrentScene();
    }

    public void AdvanceAnimations(double elapsedSeconds)
    {
        AdvanceTimeline(elapsedSeconds);
        if (!IsAnimationActive || elapsedSeconds <= 0d) return;
        _animationElapsedSeconds += Math.Min(elapsedSeconds, 0.1d);
        float angle = VoxelCameraMotion.WrapAngle(_animationSpeed * (float)_animationElapsedSeconds);
        _modelRotation = _animationBaseRotation.RotateFixedLocal(
            YawAnimationEnabled ? angle : 0f,
            PitchAnimationEnabled ? angle : 0f,
            RollAnimationEnabled ? angle : 0f);

        SyncModelRotationDisplay();
    }

    public async Task ExportTrimmedAtlasAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_mesh is null || _renderLayout is null) { StatusText = "Load and reconstruct a model before exporting."; return; }
        try
        {
            StatusText = "Rendering and packing trimmed MaxRects atlas...";
            SpriteFrame[] frames = HasMultipleFrames || !HasAnimationAxis
                ? await _spriteExportCoordinator.RenderTimelineFramesAsync(
                    GetTimelineRenderFrames(), _camera, _modelRotation, ResolveTimelineExportLayout(),
                    _renderStyle, transparentBackground: true, cancellationToken)
                : await _spriteExportCoordinator.RenderRotationFramesAsync(
                    _animationFramesPerSecond, _animationSpeed, YawAnimationEnabled, PitchAnimationEnabled,
                    RollAnimationEnabled, _camera, _modelRotation, _mesh, _renderLayout, _renderStyle,
                    transparentBackground: true, cancellationToken);
            SpriteExportResult result = await new TrimmedAtlasExporter(new PngPixelWriter()).ExportAsync(
                new TrimmedAtlasExportRequest(path, frames, new TrimmedAtlasOptions()), cancellationToken);
            StatusText = $"Exported {result.FrameCount} trimmed frames in {result.Width}x{result.Height} atlas + JSON";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusText = $"Atlas export failed: {exception.Message}";
        }
    }

    private TimelineRenderFrame[] GetTimelineRenderFrames() => _projectFrames.Select(frame =>
        new TimelineRenderFrame(
            frame.Name,
            frame.DurationMilliseconds,
            frame.Mesh ?? throw new InvalidOperationException(
                $"Timeline frame '{frame.Name}' is still rebuilding. Wait for editing to finish and export again.")))
        .ToArray();

    private PixelRenderLayout ResolveTimelineExportLayout()
    {
        int width = _projectFrames.Max(frame => frame.Document.Storage.Dimensions.Width);
        int height = _projectFrames.Max(frame => frame.Document.Storage.Dimensions.Height);
        int depth = _projectFrames.Max(frame => frame.Document.Storage.Dimensions.Depth);
        int sourceWidth = Math.Max(1, _projectFrames
            .SelectMany(frame => frame.SourceViews?.Views ?? [])
            .Select(pair => pair.Value.Width)
            .DefaultIfEmpty(_renderLayout?.Width ?? 1)
            .Max());
        int sourceHeight = Math.Max(1, _projectFrames
            .SelectMany(frame => frame.SourceViews?.Views ?? [])
            .Select(pair => pair.Value.Height)
            .DefaultIfEmpty(_renderLayout?.Height ?? 1)
            .Max());
        return _layoutResolver.Resolve(new VoxelDimensions(width, height, depth), sourceWidth, sourceHeight);
    }

    private void AdvanceTimeline(double elapsedSeconds)
    {
        if (!_timelinePlaying || !HasMultipleFrames || elapsedSeconds <= 0d) return;
        _timelineElapsedMilliseconds += elapsedSeconds * 1000d;
        int guard = _projectFrames.Count;
        while (_timelineElapsedMilliseconds >= _projectFrames[_currentFrameIndex].DurationMilliseconds && guard-- > 0)
        {
            _timelineElapsedMilliseconds -= _projectFrames[_currentFrameIndex].DurationMilliseconds;
            SelectTimelineFrame((_currentFrameIndex + 1) % _projectFrames.Count, preservePlayback: true);
        }
    }

    private void StopRotationAnimations()
    {
        bool changed = _horizontalAnimationEnabled || _verticalAnimationEnabled || _rollAnimationEnabled ||
                       _animationPreviewPlaying;
        _horizontalAnimationEnabled = false;
        _verticalAnimationEnabled = false;
        _rollAnimationEnabled = false;
        _animationPreviewPlaying = false;
        _animationBaseRotation = _modelRotation;
        _animationElapsedSeconds = 0d;
        if (!changed) return;
        OnPropertyChanged(nameof(YawAnimationEnabled));
        OnPropertyChanged(nameof(PitchAnimationEnabled));
        OnPropertyChanged(nameof(RollAnimationEnabled));
        OnPropertyChanged(nameof(HasAnimationAxis));
        OnPropertyChanged(nameof(CanExportAnimation));
        OnPropertyChanged(nameof(CanPrimaryExport));
        NotifyAnimationPlaybackChanged();
    }

    private void AnimationAxisSelectionChanged()
    {
        if (_animationPreviewPlaying)
        {
            _animationBaseRotation = _modelRotation;
            _animationElapsedSeconds = 0d;
        }
        OnPropertyChanged(nameof(HasAnimationAxis));
        OnPropertyChanged(nameof(CanExportAnimation));
        OnPropertyChanged(nameof(CanPrimaryExport));
        if (!HasAnimationAxis && _animationPreviewPlaying)
        {
            _animationPreviewPlaying = false;
        }
        NotifyAnimationPlaybackChanged();
    }

    private void NotifyAnimationPlaybackChanged()
    {
        OnPropertyChanged(nameof(IsAnimationActive));
        OnPropertyChanged(nameof(AnimationPreviewPlaying));
        OnPropertyChanged(nameof(AnimationPlayPauseLabel));
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
        _openGlUnavailable = true;
        UpdateRenderBackendForMesh();
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
        ReplaceProjectPalette([]);
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
            StatusText = "Saving Pixel2Voxel project...";
            PixelVoxelProjectFrame[] frames = _projectFrames.Count == 0
                ? [new PixelVoxelProjectFrame("frame_0000", 100, new VoxelDocument(_document.Storage), _sourceViews)]
                : _projectFrames.Select(frame => new PixelVoxelProjectFrame(
                    frame.Name,
                    frame.DurationMilliseconds,
                    new VoxelDocument(frame.Document.Storage),
                    frame.SourceViews)).ToArray();
            PixelVoxelProject project = new(
                frames,
                Math.Clamp(_currentFrameIndex, 0, frames.Length - 1),
                CaptureProjectSettings(),
                ProjectPalette.Select(ToRgba));
            await _projectSerializer.SaveAsync(destination, project, cancellationToken);
            _projectPath = Path.GetFullPath(destination);
            _editHistory.MarkClean();
            _paletteDirty = false;
            _projectSettingsDirty = false;
            OnPropertyChanged(nameof(IsProjectDirty));
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
            StatusText = "Loading Pixel2Voxel project...";
            PixelVoxelProject project = await _projectSerializer.LoadAsync(path, cancellationToken);
            VoxelMeshBuildResult[] meshResults = await Task.Run(
                () => project.Frames.Select(frame => _mesher.Build(frame.Document, cancellationToken)).ToArray(),
                cancellationToken);
            ApplyProjectSettings(project.Settings);
            ReplaceProjectPalette(project.Palette);
            ApplyLoadedFrames(project, meshResults, Path.GetFullPath(path));
            _editHistory.MarkClean();
            _paletteDirty = false;
            _projectSettingsDirty = false;
            OnPropertyChanged(nameof(IsProjectDirty));
            OnPropertyChanged(nameof(WindowTitle));
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
        if (SelectedEditTool == VoxelEditTool.Eyedropper)
        {
            if (_document?.Storage.TryGetCell(pick.Coordinate, out VoxelCell? cell) == true &&
                cell!.TryGetColor(pick.Face, out Rgba32Color sampled))
            {
                EditColor = new AvaloniaColor(sampled.Alpha, sampled.Red, sampled.Green, sampled.Blue);
                StatusText = $"Sampled {sampled.Red:X2}{sampled.Green:X2}{sampled.Blue:X2} from {pick.Coordinate} {pick.Face}";
            }
            return false;
        }
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
        if (_document is null || !_editHistory.Undo(ResolveFrameDocument, out int frameIndex)) return;
        SelectTimelineFrame(frameIndex);
        StatusText = "Undo";
        ScheduleMeshRebuild();
    }

    public void Redo()
    {
        if (_document is null || !_editHistory.Redo(ResolveFrameDocument, out int frameIndex)) return;
        SelectTimelineFrame(frameIndex);
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
        if (!_editHistory.Execute(_document, command, _currentFrameIndex)) return;
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
            new VoxelChangeSet("Paint voxel stroke", dimensions, dimensions, changes),
            _currentFrameIndex))
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
            if (_projectFrames.Count > 0 && ReferenceEquals(_projectFrames[_currentFrameIndex].Document, source))
            {
                _projectFrames[_currentFrameIndex].Mesh = result.Mesh;
            }
            UpdateRenderBackendForMesh();
            RefreshEditorOverlay(render: false);
            if (!result.IsSuccess)
            {
                _renderLayout = null;
                NotifyExportAvailabilityChanged();
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
            NotifyExportAvailabilityChanged();
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
        string summary,
        bool resetTimeline = true)
    {
        _meshCancellation?.Cancel();
        _document = document;
        _sourceViews = sourceViews;
        _mesh = meshResult.Mesh;
        UpdateRenderBackendForMesh();
        if (resetTimeline)
        {
            _projectFrames.Clear();
            _projectFrames.Add(new ProjectFrameState("frame_0000", 100, document, sourceViews, meshResult.Mesh));
            _currentFrameIndex = 0;
            _timelinePlaying = false;
            _timelineElapsedMilliseconds = 0d;
            NotifyTimelineChanged();
        }
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
        NotifyExportAvailabilityChanged();
        OnPropertyChanged(nameof(ProjectPath));
        OnPropertyChanged(nameof(WindowTitle));
        ActiveWorkspace = WorkspaceMode.Edit;

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

    private VoxelDocument ResolveFrameDocument(int frameIndex)
    {
        if ((uint)frameIndex >= (uint)_projectFrames.Count)
            throw new InvalidOperationException($"Timeline frame {frameIndex} is unavailable.");
        return _projectFrames[frameIndex].Document;
    }

    private void ApplyLoadedFrames(
        PixelVoxelProject project,
        IReadOnlyList<VoxelMeshBuildResult> meshResults,
        string? projectPath)
    {
        if (project.Frames.Count != meshResults.Count)
            throw new InvalidOperationException("Loaded animation frame and mesh counts differ.");
        _projectFrames.Clear();
        for (int index = 0; index < project.Frames.Count; index++)
        {
            PixelVoxelProjectFrame frame = project.Frames[index];
            _projectFrames.Add(new ProjectFrameState(
                frame.Name, frame.DurationMilliseconds, frame.Document, frame.SourceViews, meshResults[index].Mesh));
        }
        _currentFrameIndex = project.CurrentFrameIndex;
        _timelinePlaying = false;
        _timelineElapsedMilliseconds = 0d;
        PixelVoxelProjectFrame current = project.Frames[_currentFrameIndex];
        ApplyLoadedDocument(
            current.Document,
            current.SourceViews,
            meshResults[_currentFrameIndex],
            projectPath,
            projectPath is null
                ? $"Animation import · {project.Frames.Count} frame(s)"
                : $"Project: {Path.GetFileName(projectPath)} · {project.Frames.Count} frame(s)",
            resetTimeline: false);
        NotifyTimelineChanged();
    }

    private void SelectTimelineFrame(int index, bool preservePlayback = false)
    {
        if (_projectFrames.Count == 0 || (uint)index >= (uint)_projectFrames.Count || index == _currentFrameIndex) return;
        _meshCancellation?.Cancel();
        _currentFrameIndex = index;
        ProjectFrameState frame = _projectFrames[index];
        _document = frame.Document;
        _sourceViews = frame.SourceViews;
        _mesh = frame.Mesh;
        _sourcePixelWidth = frame.SourceViews?.Views.Max(pair => pair.Value.Width) ?? frame.Document.Storage.Dimensions.Width;
        _sourcePixelHeight = frame.SourceViews?.Views.Max(pair => pair.Value.Height) ?? frame.Document.Storage.Dimensions.Height;
        VoxelDimensions dimensions = frame.Document.Storage.Dimensions;
        ResizeWidth = dimensions.Width;
        ResizeHeight = dimensions.Height;
        ResizeDepth = dimensions.Depth;
        _renderLayout = _layoutResolver.Resolve(dimensions, Math.Max(1, _sourcePixelWidth), Math.Max(1, _sourcePixelHeight));
        ClearSelection();
        UpdateRenderBackendForMesh();
        RefreshEditorOverlay(render: false);
        if (!preservePlayback) _timelineElapsedMilliseconds = 0d;
        NotifyTimelineChanged();
        OnPropertyChanged(nameof(CurrentDocument));
        OnPropertyChanged(nameof(CurrentMesh));
        NotifyExportAvailabilityChanged();
        RenderCurrentScene();
        StatusText = $"Selected {CurrentFrameSummary}";
    }

    private void NotifyTimelineChanged()
    {
        OnPropertyChanged(nameof(FrameCount));
        OnPropertyChanged(nameof(FrameLastIndex));
        OnPropertyChanged(nameof(CurrentFrameIndex));
        OnPropertyChanged(nameof(CurrentFrameNumber));
        OnPropertyChanged(nameof(HasMultipleFrames));
        OnPropertyChanged(nameof(CanExportAnimation));
        OnPropertyChanged(nameof(CanPrimaryExport));
        OnPropertyChanged(nameof(TimelinePlaying));
        OnPropertyChanged(nameof(TimelinePlayPauseLabel));
        OnPropertyChanged(nameof(CurrentFrameSummary));
    }

    private void NotifyExportAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanExportAnimation));
        OnPropertyChanged(nameof(CanPrimaryExport));
    }

    private void ReplaceProjectPalette(IEnumerable<Rgba32Color> colors)
    {
        ProjectPalette.Clear();
        foreach (Rgba32Color color in colors.Take(32))
            ProjectPalette.Add(new AvaloniaColor(color.Alpha, color.Red, color.Green, color.Blue));
        _paletteDirty = false;
        OnPropertyChanged(nameof(ProjectPalette));
        OnPropertyChanged(nameof(IsProjectDirty));
        OnPropertyChanged(nameof(WindowTitle));
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
            _modelRotation.RollDegrees,
            _modelRotation.Orientation.X,
            _modelRotation.Orientation.Y,
            _modelRotation.Orientation.Z,
            _modelRotation.Orientation.W);

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
        _modelRotation = TryGetPersistedOrientation(settings, out System.Numerics.Quaternion orientation)
            ? VoxelModelRotationState.FromOrientation(
                _rawModelYawDegrees, _rawModelPitchDegrees, _rawModelRollDegrees, orientation)
            : new VoxelModelRotationState(
                _rawModelYawDegrees, _rawModelPitchDegrees, _rawModelRollDegrees);
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
            $"Object · Yaw {_rawModelYawDegrees:0}° · Pitch {_rawModelPitchDegrees:0}° · Roll {_rawModelRollDegrees:0}°";
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

    private static bool TryGetPersistedOrientation(
        PixelVoxelProjectSettings settings,
        out System.Numerics.Quaternion orientation)
    {
        orientation = default;
        if (settings.ModelOrientationX is not float x ||
            settings.ModelOrientationY is not float y ||
            settings.ModelOrientationZ is not float z ||
            settings.ModelOrientationW is not float w ||
            !float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || !float.IsFinite(w))
        {
            return false;
        }

        orientation = new System.Numerics.Quaternion(x, y, z, w);
        if (orientation.LengthSquared() < 0.000001f) return false;
        orientation = System.Numerics.Quaternion.Normalize(orientation);
        return true;
    }

    private void MarkProjectSettingsDirty()
    {
        if (_document is null || _projectSettingsDirty) return;
        _projectSettingsDirty = true;
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
        _sourceMaskEdits.Clear();
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
        SelectedImportFaceSlot = ImportFaceSlots.FirstOrDefault();
        ActiveWorkspace = WorkspaceMode.Import;
        ActiveImportStep = ImportWorkflowStep.MapAndAlign;
        RefreshAlignmentPreview();
        StatusText = CanApplyImport
            ? "PNG inspection complete. Review alignment and choose Apply / Reconstruct."
            : "PNG inspection complete with blocking diagnostics.";
    }

    private void RefreshAlignmentPreview()
    {
        if (_suppressImportPreviewRefresh || _importDraft is null) return;
        SixViewAlignmentPreview generated = _importer.PreviewAlignment(_importDraft, BuildCurrentAlignment());
        Dictionary<VoxelFace, OrthographicImage> editedViews = generated.Views.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (((VoxelFace face, int x, int y), Rgba32Color color) in _sourceMaskEdits)
        {
            if (editedViews.TryGetValue(face, out OrthographicImage? image) &&
                (uint)x < (uint)image.Width && (uint)y < (uint)image.Height)
            {
                editedViews[face] = image.WithPixel(x, y, color);
            }
        }
        List<ImportDiagnostic> editedDiagnostics = generated.Diagnostics.ToList();
        SixViewSlotInfo[] editedSlots = generated.Slots.Select(slot =>
        {
            OrthographicImage image = editedViews[slot.Face];
            int visible = CountVisiblePixels(image);
            if (visible == 0)
            {
                ImportFaceSlotViewModel card = ImportFaceSlots.Single(item => item.TargetFace == slot.Face);
                editedDiagnostics.Add(new ImportDiagnostic(
                    "empty-edited-mask", ImportDiagnosticSeverity.Error, card.SourceSlotIndex,
                    slot.Face, null, null, $"The edited {slot.Face} mask contains no visible pixels."));
            }
            return slot with { OpaquePixelCount = visible };
        }).ToArray();
        _alignmentPreview = new SixViewAlignmentPreview(editedViews, editedSlots, editedDiagnostics);
        CanApplyImport = _alignmentPreview.CanApply;

        VoxelReprojectionAnalysis? conflicts = null;
        if (CanApplyImport)
        {
            try
            {
                OrthographicViewSet previewViews = new(_alignmentPreview.Views);
                VoxelDocument provisional = _reconstructor.Reconstruct(previewViews, CurrentReconstructionOptions());
                conflicts = new VoxelReprojectionAnalyzer().Analyze(
                    previewViews, provisional, CurrentReconstructionOptions());
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
            {
                editedDiagnostics.Add(new ImportDiagnostic(
                    "reprojection-failed", ImportDiagnosticSeverity.Error, -1, null, null, null,
                    $"Reprojection validation failed: {exception.Message}"));
                _alignmentPreview = new SixViewAlignmentPreview(editedViews, editedSlots, editedDiagnostics);
                CanApplyImport = false;
            }
        }

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
            int conflictCount = conflicts?.GetConflictCount(card.TargetFace) ?? 0;
            string text = diagnostic?.Message ??
                (statistics is null
                    ? "No transformed view"
                    : conflictCount > 0
                        ? $"{conflictCount:N0} silhouette conflict(s) · edit source mask"
                        : $"Ready · {statistics.OpaquePixelCount:N0} visible");
            card.SetPreview(
                image is null ? null : TryCreateImportPreviewBitmap(
                    image,
                    conflicts?.Conflicts.Where(item => item.Face == card.TargetFace)),
                text,
                diagnostic?.Severity ?? (conflictCount > 0 ? ImportDiagnosticSeverity.Warning : null));
        }

        string diagnosticLines = _alignmentPreview.Diagnostics.Count == 0
            ? "No import diagnostics."
            : string.Join(
                Environment.NewLine,
                _alignmentPreview.Diagnostics.Take(16).Select(item =>
                    $"[{item.Severity}] {item.Message}"));
        ImportSummary =
            $"Draft: {Path.GetFileName(_importDraft.SourcePath)}{Environment.NewLine}" +
            $"Sources: {_importDraft.Slots.Count} selected face(s){Environment.NewLine}" +
            $"Apply ready: {CanApplyImport}{Environment.NewLine}{Environment.NewLine}" +
            $"Reprojection conflicts: {conflicts?.ConflictCount ?? 0:N0}{Environment.NewLine}{Environment.NewLine}" +
            diagnosticLines;
    }

    private VoxelReconstructionOptions CurrentReconstructionOptions() =>
        new(UnobservedXLength, UnobservedYLength, UnobservedZLength);

    private static int CountVisiblePixels(OrthographicImage image)
    {
        int count = 0;
        foreach (Rgba32Color pixel in image.Pixels.Span)
        {
            if (pixel.Alpha > 0) count++;
        }
        return count;
    }

    private static Rgba32Color ResolveSourceMaskPaintColor(OrthographicImage image, int x, int y)
    {
        if ((uint)x >= (uint)image.Width || (uint)y >= (uint)image.Height) return new Rgba32Color(255, 255, 255, 255);
        Rgba32Color existing = image.GetPixel(x, y);
        if (existing.Alpha > 0) return existing;
        for (int radius = 1; radius <= 2; radius++)
        {
            for (int sampleY = Math.Max(0, y - radius); sampleY <= Math.Min(image.Height - 1, y + radius); sampleY++)
            for (int sampleX = Math.Max(0, x - radius); sampleX <= Math.Min(image.Width - 1, x + radius); sampleX++)
            {
                Rgba32Color sample = image.GetPixel(sampleX, sampleY);
                if (sample.Alpha > 0) return sample;
            }
        }
        return new Rgba32Color(255, 255, 255, 255);
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
        ViewportMessage = "Reconstructing selected orthographic views...";
        VoxelReconstructionOptions reconstructionOptions = new(
            UnobservedXLength,
            UnobservedYLength,
            UnobservedZLength);

        try
        {
            ImportWorkResult result = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                SixViewImportResult imported = import(cancellationToken);
                VoxelDocument document = _reconstructor.Reconstruct(imported.Views, reconstructionOptions);
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
        ReplaceProjectPalette([]);
        _sourceViews = result.Import.Views;
        _mesh = result.MeshResult.Mesh;
        UpdateRenderBackendForMesh();
        _projectFrames.Clear();
        _projectFrames.Add(new ProjectFrameState(
            "frame_0000", 100, result.Document, result.Import.Views, result.MeshResult.Mesh));
        _currentFrameIndex = 0;
        _timelinePlaying = false;
        _timelineElapsedMilliseconds = 0d;
        NotifyTimelineChanged();
        _projectPath = null;
        _editHistory.Clear();
        ClearSelection();
        RefreshEditorOverlay(render: false);
        OnPropertyChanged(nameof(CurrentDocument));
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(ProjectPath));
        OnPropertyChanged(nameof(WindowTitle));
        _rawModelYawDegrees = 0f;
        _rawModelPitchDegrees = 0f;
        _rawModelRollDegrees = 0f;
        _modelRotation = VoxelModelRotationState.Identity;
        ObjectRotationSummary = "Object · Yaw 0° · Pitch 0° · Roll 0°";
        OnPropertyChanged(nameof(CurrentModelRotation));
        SixViewSlotInfo sourceSlot = result.Import.Slots[0];
        _sourcePixelWidth = sourceSlot.Width;
        _sourcePixelHeight = sourceSlot.Height;
        VoxelDimensions dimensions = result.Document.Storage.Dimensions;
        ResizeWidth = dimensions.Width;
        ResizeHeight = dimensions.Height;
        ResizeDepth = dimensions.Depth;
        _renderLayout = _layoutResolver.Resolve(dimensions, _sourcePixelWidth, _sourcePixelHeight);
        NotifyExportAvailabilityChanged();
        ActiveWorkspace = WorkspaceMode.Edit;
        AddCurrentImportToRecent();
        string slotLines = string.Join(
            Environment.NewLine,
            result.Import.Slots.Select(slot =>
                $"{slot.Face}: {slot.Width}×{slot.Height}, {slot.OpaquePixelCount:N0} visible"));
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

    private void UpdateRenderBackendForMesh()
    {
        IsCpuFallbackActive = _openGlUnavailable || _mesh?.HasTranslucentFaces == true;
    }

    private void ApplyFreeCamera()
    {
        float yaw = VoxelCameraMotion.Snap(_rawYawDegrees);
        float pitch = VoxelCameraMotion.Snap(_rawPitchDegrees);
        if (CameraFaceSnapEnabled)
        {
            VoxelCameraFaceSnap faceSnap = VoxelCameraMotion.SnapToSheetFace(
                _rawYawDegrees,
                _rawPitchDegrees,
                _modelRotation.Orientation,
                CameraFaceSnapAngle);
            if (faceSnap.IsSnapped)
            {
                yaw = faceSnap.YawDegrees;
                pitch = faceSnap.PitchDegrees;
            }
        }

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
        CameraSummary = $"Free View · Yaw {yaw:0}° · Pitch {pitch:0}°";
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
        MarkProjectSettingsDirty();
        ObjectRotationSummary = $"Object · Yaw {yaw:0}° · Pitch {pitch:0}° · Roll {roll:0}°";
        OnPropertyChanged(nameof(CurrentModelRotation));
        OnPropertyChanged(nameof(ModelRollDegrees));
        RenderCurrentScene();
    }

    private VoxelModelRotationState RotateAroundViewAxis(VoxelModelRotationState state, float degrees)
    {
        Matrix4x4.Invert(_renderTransform?.ViewRotation ?? Matrix4x4.Identity, out Matrix4x4 inverseView);
        Vector3 worldAxis = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, inverseView));
        return state.RotateWorld(worldAxis, degrees);
    }

    private void SyncModelRotationDisplay()
    {
        _rawModelYawDegrees = _modelRotation.YawDegrees;
        _rawModelPitchDegrees = _modelRotation.PitchDegrees;
        _rawModelRollDegrees = _modelRotation.RollDegrees;
        ObjectRotationSummary = $"Object · Yaw {_rawModelYawDegrees:0}° · Pitch {_rawModelPitchDegrees:0}° · Roll {_rawModelRollDegrees:0}°";
        MarkProjectSettingsDirty();
        OnPropertyChanged(nameof(CurrentModelRotation));
        OnPropertyChanged(nameof(ModelRollDegrees));
        RenderCurrentScene();
    }

    private void SetCamera(float yaw, float pitch, VoxelViewMode mode, VoxelCameraPreset preset)
    {
        _rawYawDegrees = yaw;
        _rawPitchDegrees = pitch;
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
            LeftPanelVisible = _leftPanelVisible,
            RightPanelVisible = _rightPanelVisible,
            LeftPanelWidth = _leftPanelWidth,
            RightPanelWidth = _rightPanelWidth,
            EditCategoryExpanded = _editCategoryExpanded,
            CameraCategoryExpanded = _cameraCategoryExpanded,
            AnimationCategoryExpanded = _animationCategoryExpanded,
            RenderingCategoryExpanded = _renderingCategoryExpanded,
            ExportCategoryExpanded = _exportCategoryExpanded,
            DefaultYaw = _savedDefaultYaw,
            DefaultPitch = _savedDefaultPitch,
            CameraFaceSnapEnabled = _cameraFaceSnapEnabled,
            CameraFaceSnapAngle = _cameraFaceSnapAngle,
            ZoomIsFit = !_manualZoomScale.HasValue,
            ManualZoomScale = _manualZoomScale ?? 6,
            AnimationSpeed = _animationSpeed,
            AnimationFramesPerSecond = _animationFramesPerSecond,
            GifExportResizePercent = _gifExportResizePercent,
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
        new(color.R, color.G, color.B, color.A);

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
            !float.IsFinite(settings.CameraFaceSnapAngle) || settings.CameraFaceSnapAngle is < 1f or > 30f ||
            settings.ManualZoomScale is < 1 or > 16 ||
            !float.IsFinite(settings.AnimationSpeed) || settings.AnimationSpeed is < 5f or > 180f ||
            settings.AnimationFramesPerSecond is < 1 or > 60 ||
            settings.GifExportResizePercent is < 25 or > 1000 ||
            !double.IsFinite(settings.LeftPanelWidth) || settings.LeftPanelWidth is < 260 or > 600 ||
            !double.IsFinite(settings.RightPanelWidth) || settings.RightPanelWidth is < 280 or > 600 ||
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

    private static WriteableBitmap CreateImportPreviewBitmap(
        OrthographicImage image,
        IEnumerable<VoxelReprojectionConflict>? conflicts = null)
    {
        Rgba32Color[] pixels = image.Pixels.ToArray();
        foreach (VoxelReprojectionConflict conflict in conflicts ?? [])
        {
            int index = (conflict.Y * image.Width) + conflict.X;
            pixels[index] = conflict.SourceOccupied
                ? new Rgba32Color(255, 32, 64, 255)
                : new Rgba32Color(255, 220, 0, 255);
        }
        return CreateBitmap(image.Width, image.Height, pixels);
    }

    private static WriteableBitmap? TryCreateImportPreviewBitmap(
        OrthographicImage image,
        IEnumerable<VoxelReprojectionConflict>? conflicts = null)
    {
        try
        {
            return CreateImportPreviewBitmap(image, conflicts);
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("IPlatformRenderInterface", StringComparison.Ordinal))
        {
            return null;
        }
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

    private sealed class ProjectFrameState
    {
        public ProjectFrameState(
            string name,
            int durationMilliseconds,
            VoxelDocument document,
            OrthographicViewSet? sourceViews,
            VoxelMeshData? mesh)
        {
            Name = name;
            DurationMilliseconds = durationMilliseconds;
            Document = document;
            SourceViews = sourceViews;
            Mesh = mesh;
        }

        public string Name { get; }
        public int DurationMilliseconds { get; }
        public VoxelDocument Document { get; }
        public OrthographicViewSet? SourceViews { get; }
        public VoxelMeshData? Mesh { get; set; }
    }
}
