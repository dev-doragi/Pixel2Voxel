# Pixel2Voxel for Aseprite — experimental prototype

Pure Lua Aseprite extension; no external runtime. Target Aseprite 1.3.17+ (actual GUI installation not yet verified). RGB, transparent/opaque alpha only. Apache-2.0, original desktop code preserved separately.

## Install / 설치
1. Double-click the `Pixel2Voxel-<version>.aseprite-extension` package, or use Edit > Preferences > Extensions > Add Extension.
2. Open an RGB sprite; Edit > Pixel2Voxel... . If the menu does not appear, reload/restart Aseprite and check Extensions.
3. `Use active` pins the current sprite and frame. `Refresh` recaptures it. Original document is not intentionally modified. The first open uses the active sprite.

## Use / 사용
- **Extrude**: current frame composite (optionally selection), Depth 1–64. Transparent pixels are empty. Side/back colors reuse front colors.
- **Layer stack**: visible flat RGB layers in bottom-to-top order, each layer = one Y slice. Cel positions are preserved on a common XZ canvas; empty visible layers count. No groups, tilemaps, reference layers, or non-normal/partial opacity layers. Selection is ignored.
- **Six views**: 6 equal horizontal slots F/R/B/L/T/D, no guide pixels. Align views swaps slots, flips, then offsets/clips. Apply rebuilds; Cancel discards draft. The original sheet stays untouched. No hidden concavities can be inferred.
- **Six views (6 frames)**: exactly six RGB timeline frames in Front, Right, Back, Left, Top, Bottom order. Uses the full canvas of each frame and shares the same Align views controls as the horizontal sheet.
- Drag the preview to orbit, middle or Shift-drag to pan, and use the wheel to zoom. Double-click or click Fit to reset. Use the six face presets or ISO to jump to a view. Snap to sheet faces uses the adjustable Snap angle during orbit. Render pixels/voxel 1/2/4 changes output resolution independently of display zoom.
- Shading adds directional lighting to both preview and baked output. Light settings controls azimuth, elevation, and color. Turn off Shading to preserve sampled RGB colors.
- Bake sprite: current view, 4/8/16 directions (start front yaw, keep pitch), or turntable (start current yaw). Shared frame dimensions and centered model anchor; no duplicate last angle. New RGB document, tag and FPS. Save/export with Aseprite itself.
- Editable stack: create a separate layer document for Extrude/Stack, not Six views (directional colors cannot round-trip). Open that document with Use active + Layer stack to edit and rebuild.
- Live is experimental and off by default; source/frame are pinned, not automatically switched. Use Refresh after edits or Use active after changing the frame. Cancel job discards pending work. Closing the source disables baking.

## Limits and verification
Candidate voxels <= 1,048,576; exposed faces <= 500,000; individual render <= 1,024 each dimension and <= 262,144 pixels; total multi-frame <= 4,194,304 pixels; editable stack <= 128 layers. Intermediate alpha is rejected rather than silently thresholded. RGB only (convert a copy of Indexed/Gray manually). No actual Aseprite UI, install, mouse capture, raw RGBA order, events or output transaction tested yet. Lua 5.4 standalone tests are available in the developer source archive. The renderer is newly written; no exact C# legacy hash guarantee. No P1/P2 features, full localization, alignment thumbnails, saved project metadata, or separate 3D model editing. Aseprite APIs may differ by host version. Report version, OS, steps and exact error if a failure occurs.

## Build and release

From the repository root, run `python scripts/package-extension.py` to build the installable package. Run `lua extension-tests/run.lua` with Lua 5.4 to check the standalone functionality. See the [prototype verification record](https://github.com/dev-doragi/Pixel2Voxel/blob/main/docs/AsepritePrototypeVerification.md) for host checks and recorded results.

Push an `aseprite-v<version>` tag matching the manifest version to publish a GitHub prerelease. Windows desktop releases use the existing `v*` tags.
