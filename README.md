<h1 align="center">Pixel2Voxel</h1>

<p align="center">
  <img width="315" height="315" alt="Pixel2Voxel rotation preview" src="https://github.com/user-attachments/assets/3e083337-730b-4402-990e-5b5e2671ecb4" />
</p>

<p align="center">
  English · <a href="README.ko.md">한국어</a>
</p>

**Pixel2Voxel (P2V)** is a Windows desktop editor that reconstructs editable voxel models from six orthographic pixel-art views.

Import Front, Right, Back, Left, Top, and Bottom images, reconstruct them into a voxel volume, edit the result, preview it from different angles, and export sprites or 3D assets in a single workflow.

## Download

[Download the latest Windows version](https://github.com/dev-doragi/Pixel2Voxel/releases/latest/download/Pixel2Voxel-win-x64.zip)

All versions and release notes are available on [GitHub Releases](https://github.com/dev-doragi/Pixel2Voxel/releases).

### Requirements

- Windows 10 / 11
- No separate .NET installation required

Download `Pixel2Voxel-win-x64.zip`, extract it, and run `Pixel2Voxel.exe`.

## How It Works

Pixel2Voxel is not an AI image-to-3D generator. It reconstructs a voxel volume by intersecting the silhouettes of six orthographic views.

```text
Front ∩ Right ∩ Back
∩ Left ∩ Top ∩ Bottom
= reconstructed voxel volume
```

Each candidate voxel is projected onto the selected input images. Voxels that satisfy the visible regions of every input view are kept, while RGBA colors from the source images are preserved on exposed surfaces.

Because reconstruction is based on silhouettes, hidden cavities and concave structures that are not visible from the selected input views cannot be recovered automatically.

## Workflow

```text
Import Images
      ↓
Map & Align
      ↓
Validate
      ↓
Reconstruct
      ↓
Edit Voxels
      ↓
Preview & Export
```

Pixel2Voxel supports both individual PNG images and a single `6×1` sprite sheet. The expected input order is:

```text
┌───────┬───────┬──────┬──────┬─────┬────────┐
│ Front │ Right │ Back │ Left │ Top │ Bottom │
└───────┴───────┴──────┴──────┴─────┴────────┘
```

After importing, you can adjust view mapping, flips, and offsets before validating and reconstructing the voxel model.

## Features

### Reconstruction

- One-to-six-view orthographic voxel reconstruction with explicit unobserved-axis lengths
- Individual PNG or `6×1` sprite sheet import
- Front / Right / Back / Left / Top / Bottom mapping
- Horizontal and vertical flip
- Integer image offsets
- Input validation and diagnostics

### Voxel Editing

- Add, erase, paint, eyedropper, and box select tools
- Undo / Redo
- Volume resize

### Camera & Preview

- Pixel 2:1, True Isometric, Front, Right, Top, and Free View cameras
- Camera snapping based on source views
- X / Y / Z rotation controls
- Rotation preview with adjustable speed and FPS
- Aseprite PNG+JSON face-animation import with duration-based timeline playback
- Per-face reprojected silhouette diagnostics and source-mask correction
- Independent per-frame voxel editing with project-wide undo/redo
- Lighting, outline, and background settings

### Export

- Current-view PNG
- 4 / 8 / 16-direction sprite sheets
- Animated PNG sprite sheets and rotation GIF
- Aseprite JSON
- Trimmed deterministic MaxRects atlas (2 px padding, 1 px extrusion)
- OBJ / MTL / palette texture package for Unity

### Projects

- Save and load portable `.pxv` project files
- OpenGL viewport with a CPU fallback renderer

## Input Guidelines

- Use the same canvas size for every selected view.
- Use orthographic views.
- Fully transparent pixels (`alpha = 0`) are empty; pixels with `alpha = 1-255` become voxel faces with preserved transparency.
- Keep the object aligned consistently across all views.
- Check mapping and alignment before reconstruction.

Pixel2Voxel can compensate for small alignment differences using flips and integer offsets, but consistently aligned source images will produce better results.

## Project Files

Pixel2Voxel project files use the `.pxv` extension. For compatibility with earlier versions, some internal identifiers still use the `PixelVoxel` name:

- Project extension: `.pxv`
- Manifest format: `PixelVoxel`
- Unity marker: `.pixelvoxel.json`
- Settings path: `%LOCALAPPDATA%\PixelVoxel\settings.json`

For technical details, see [FileFormat.md](docs/FileFormat.md) and [CoordinateSystem.md](docs/CoordinateSystem.md).

## Building from Source

Building Pixel2Voxel from source requires the .NET 10 SDK.

```powershell
dotnet restore
dotnet build
dotnet test
```

Run the desktop application:

```powershell
dotnet run --project src\PixelVoxel.App\PixelVoxel.App.csproj -c Debug
```

Inspect or validate a project container in automation:

```powershell
dotnet run --project src\PixelVoxel.Cli -- inspect model.pxv
dotnet run --project src\PixelVoxel.Cli -- validate model.pxv
```

Create a Windows x64 self-contained build:

```powershell
dotnet publish src\PixelVoxel.App\PixelVoxel.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
  -o artifacts\Pixel2Voxel-win-x64
```

The product name is **Pixel2Voxel**, while existing source directories and namespaces currently retain the `PixelVoxel.*` naming for compatibility.
