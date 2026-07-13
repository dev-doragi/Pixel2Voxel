using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelVoxel.App.Services;
using PixelVoxel.Core;
using PixelVoxel.Imaging;
using PixelVoxel.Rendering;
using AvaloniaColor = Avalonia.Media.Color;

namespace PixelVoxel.App.ViewModels;

/// <summary>Coordinates import, camera motion, visual styling, and GPU/CPU render state.</summary>
public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private const float FreeRotationSensitivity = 0.7f;
    private readonly AsepriteSpriteSheetImporter _importer;
    private readonly IVoxelReconstructor _reconstructor;
    private readonly VoxelSurfaceMesher _mesher;
    private readonly PixelArtVoxelRasterizer _pixelArtRasterizer;
    private readonly VoxelRenderTransformResolver _transformResolver;
    private readonly PixelRenderLayoutResolver _layoutResolver;
    private readonly ViewportSettingsStore _settingsStore;
    private readonly Dictionary<VoxelFace, string> _separatePaths = [];
    private CancellationTokenSource? _importCancellation;
    private VoxelDocument? _document;
    private VoxelMeshData? _mesh;
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
    private string _objectRotationSummary = "Object · Yaw 0° · Pitch 0°";
    private string _zoomSummary = "Fit";
    private int _sourcePixelWidth;
    private int _sourcePixelHeight;
    private float _rawYawDegrees;
    private float _rawPitchDegrees;
    private float _rawModelYawDegrees;
    private float _rawModelPitchDegrees;
    private float _savedDefaultYaw;
    private float _savedDefaultPitch;
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
    private long _importRevision;

    /// <summary>Initializes the application workflow and loads user viewport settings.</summary>
    public MainWindowViewModel(
        AsepriteSpriteSheetImporter importer,
        IVoxelReconstructor reconstructor,
        VoxelSurfaceMesher mesher,
        PixelArtVoxelRasterizer pixelArtRasterizer,
        VoxelRenderTransformResolver transformResolver,
        PixelRenderLayoutResolver layoutResolver,
        ViewportSettingsStore settingsStore)
    {
        _importer = importer;
        _reconstructor = reconstructor;
        _mesher = mesher;
        _pixelArtRasterizer = pixelArtRasterizer;
        _transformResolver = transformResolver;
        _layoutResolver = layoutResolver;
        _settingsStore = settingsStore;

        (ViewportUserSettings settings, string? diagnostic) = _settingsStore.Load();
        diagnostic ??= GetInvalidSettingsDiagnostic(settings);
        _savedDefaultYaw = VoxelCameraMotion.WrapAngle(FiniteOrDefault(settings.DefaultYaw, -45f));
        _savedDefaultPitch = VoxelCameraMotion.WrapAngle(FiniteOrDefault(settings.DefaultPitch, -30f));
        _rawYawDegrees = _savedDefaultYaw;
        _rawPitchDegrees = _savedDefaultPitch;
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
        _renderStyle = BuildRenderStyle();
        ZoomSummary = _manualZoomScale.HasValue ? $"{_manualZoomScale.Value}×" : "Fit";
        CameraSummary = $"Saved Default · Yaw {_camera.YawDegrees:0}° · Pitch {_camera.PitchDegrees:0}°";

        if (diagnostic is not null)
        {
            StatusText = diagnostic;
            SaveSettings();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? RenderStateChanged;

    public VoxelMeshData? CurrentMesh => _mesh;

    public VoxelRenderTransform? CurrentRenderTransform => _renderTransform;

    public VoxelCameraState CurrentCameraState => _camera;

    public VoxelModelRotationState CurrentModelRotation => _modelRotation;

    public PixelRenderLayout? CurrentRenderLayout => _renderLayout;

    public VoxelRenderStyle CurrentRenderStyle => _renderStyle;

    public int? ManualZoomScale => _manualZoomScale;

    public long ImportRevision => Interlocked.Read(ref _importRevision);

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
        RunImportAsync(token => _importer.ImportHorizontalSheet(path), default);

    public void SetSeparatePath(VoxelFace face, string path)
    {
        _separatePaths[face] = path;
        OnPropertyChanged(GetPathPropertyName(face));
        StatusText = $"Assigned {face}: {Path.GetFileName(path)}";
    }

    public Task ReconstructSeparateAsync()
    {
        Dictionary<VoxelFace, string> snapshot = new(_separatePaths);
        return RunImportAsync(token => _importer.ImportSeparate(snapshot), default);
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

    public void Dispose()
    {
        _importCancellation?.Cancel();
        _importCancellation?.Dispose();
        _viewportBitmap?.Dispose();
        _settingsStore.Dispose();
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
        _document = result.Document;
        _mesh = result.MeshResult.Mesh;
        _rawModelYawDegrees = 0f;
        _rawModelPitchDegrees = 0f;
        _modelRotation = VoxelModelRotationState.Identity;
        ObjectRotationSummary = "Object · Yaw 0° · Pitch 0°";
        OnPropertyChanged(nameof(CurrentModelRotation));
        SixViewSlotInfo sourceSlot = result.Import.Slots[0];
        _sourcePixelWidth = sourceSlot.Width;
        _sourcePixelHeight = sourceSlot.Height;
        VoxelDimensions dimensions = result.Document.Storage.Dimensions;
        _renderLayout = _layoutResolver.Resolve(dimensions, _sourcePixelWidth, _sourcePixelHeight);
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
                _mesh,
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
        if (_modelRotation.YawDegrees == yaw && _modelRotation.PitchDegrees == pitch)
        {
            return;
        }

        _modelRotation = new VoxelModelRotationState(yaw, pitch);
        ObjectRotationSummary = $"Object · Yaw {yaw:0}° · Pitch {pitch:0}°";
        OnPropertyChanged(nameof(CurrentModelRotation));
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

    private static WriteableBitmap CreateBitmap(PixelFramebuffer frame)
    {
        WriteableBitmap bitmap = new(
            new PixelSize(frame.Width, frame.Height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Unpremul);
        byte[] bytes = new byte[checked(frame.Width * frame.Height * 4)];
        ReadOnlySpan<Rgba32Color> pixels = frame.Pixels.Span;
        for (int index = 0; index < pixels.Length; index++)
        {
            int byteIndex = index * 4;
            bytes[byteIndex] = pixels[index].Red;
            bytes[byteIndex + 1] = pixels[index].Green;
            bytes[byteIndex + 2] = pixels[index].Blue;
            bytes[byteIndex + 3] = pixels[index].Alpha;
        }

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        int sourceRowBytes = frame.Width * 4;
        for (int row = 0; row < frame.Height; row++)
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
