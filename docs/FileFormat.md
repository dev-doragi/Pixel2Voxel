# File Format

## Pixel2Voxel project v2 additions

Version 2 adds an optional project-owned `palette` array to `manifest.json`. It contains at most 32 opaque RGBA colors and does not change `document.bin` voxel semantics. The loader remains backward-compatible with version 1 projects, which open with an empty palette. Unknown future versions remain rejected.

## Animation exports

Rotation animation sheets use one fixed logical canvas and pivot. Frames are written left-to-right to PNG, while Aseprite JSON records each frame rectangle, its duration in milliseconds, the shared pivot slice, and a `rotation` frame tag. GIF exports use the same sampled frames, loop indefinitely, and use GIF palette quantization only when more than 256 colors are present.

GIF output can apply an Aseprite-style 25% to 1000% nearest-neighbor resize without changing the Aseprite sheet's logical frame size. For example, a 32x32 logical frame exports at 32x32 at 100% and 320x320 at 1000%. Rotation GIFs and animation sheets snapshot the current editor camera mode, preset, yaw, and pitch at export time. Pan is reset to zero and zoom to 1 so every frame remains centered on the fixed logical canvas. Rotation frames start at the captured model yaw, pitch, and roll; selected axes complete one loop without a duplicate 360-degree terminal frame.

## Unity OBJ package

Unity packages contain `.obj`, `.mtl`, `_palette.png`, `.pixelvoxel.json`, and `Editor/PixelVoxelAssetPostprocessor.cs`. Only exposed voxel faces are emitted. Palette UVs address texel centers, and the generated Unity postprocessor applies Point filtering, disables mipmaps and compression, and clamps the palette texture.

## Pixel2Voxel 프로젝트 v1

프로젝트 확장자는 `.pxv`이며 하나의 ZIP 컨테이너로 저장한다. 저장은 같은 디렉터리의 임시 파일에 모두 기록한 뒤 성공 시 최종 파일로 교체한다.

- `manifest.json`: `PixelVoxel` 형식명, version 1, `XYZ-RightUpFront-v1` 좌표계, 모델 크기, 포함된 뷰, 카메라·렌더·출력 설정
- `document.bin`: `PXVD` magic, version, 점유 셀 수, 좌표와 면 색상 mask 및 RGBA
- `views/{face}.png`: Import 정렬이 적용된 직교 이미지

`document.bin`의 셀은 X, Y, Z 순으로 정렬한다. 중복 좌표, 모델 범위 밖 좌표, 잘못된 면 mask, trailing byte 또는 1,048,576 후보 셀 한도 초과는 전체 로드를 거부한다. 지원하지 않는 형식·좌표계 버전도 현재 문서를 변경하지 않고 거부한다.

`VoxelMeshData`, OpenGL 버퍼, 텍스처, framebuffer와 Undo/Redo 기록은 재생성 가능한 세션 데이터이므로 포함하지 않는다. 프로젝트를 다시 열면 현재 문서가 clean 상태가 되고 편집 이력은 비운다.

## Sprite 출력 형식

현재 구현의 결과물 출력은 프로젝트 저장 형식과 독립적이다.

- 현재 뷰: 논리 크기 RGBA PNG 한 장
- 방향 시트: 동일 크기 프레임 4·8·16개를 왼쪽에서 오른쪽으로 배치한 RGBA PNG
- 메타데이터: PNG와 같은 basename의 Aseprite 호환 JSON array
- 프레임 0은 모델 yaw 0°이며 이후 프레임은 위에서 보았을 때 시계방향이다.
- 모든 프레임은 trim·padding 없이 같은 캔버스와 바닥 중앙 pivot을 사용한다.
- PNG와 JSON은 임시 파일 작성이 모두 성공한 뒤 교체한다.

이 출력 계약은 `.aseprite` 프로젝트 파일이나 향후 Pixel2Voxel 프로젝트 저장 형식을 정의하지 않는다.
