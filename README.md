# Pixel2Voxel

<img width="315" height="315" alt="pixel-voxel-rotation" src="https://github.com/user-attachments/assets/3e083337-730b-4402-990e-5b5e2671ecb4" />

**Pixel2Voxel (P2V)** is a Windows-first desktop editor that reconstructs an editable voxel volume from six orthographic pixel-art views.

Front, Right, Back, Left, Top, Bottom 이미지를 교차 투영하여 3D 점유 격자를 만들고, 복셀 편집·카메라 조정·회전 애니메이션·스프라이트 및 OBJ 출력까지 한 작업 흐름에서 처리합니다.

## 다운로드

[최신 Windows 버전 다운로드](https://github.com/dev-doragi/IsometricPixel/releases/latest/download/Pixel2Voxel-win-x64.zip)

모든 버전과 변경 사항은 [GitHub Releases](https://github.com/dev-doragi/IsometricPixel/releases)에서 확인할 수 있습니다.

> 아직 Release가 게시되지 않았다면 위 직접 다운로드 링크는 404를 반환합니다. 아래의 **Release 게시** 절차로 첫 버전을 올리면 활성화됩니다.

## 핵심 기술

Pixel2Voxel은 AI 이미지 생성기가 아니라 **6방향 정사영 실루엣 기반 Visual Hull 복셀 재구성기**입니다.

```text
Front volume ∩ Right volume ∩ Back volume
∩ Left volume ∩ Top volume ∩ Bottom volume
= reconstructed voxel volume
```

각 후보 복셀을 여섯 입력 이미지에 투영하고, 모든 시점의 불투명 픽셀을 만족하는 셀만 남깁니다. 노출된 복셀 면에는 해당 원본 시트의 RGBA 색상을 보존합니다. 실루엣에 나타나지 않는 내부 공간이나 숨겨진 오목 구조는 복원할 수 없습니다.

## 주요 기능

- `6×1` PNG 시트 또는 개별 6면 PNG Import
- 고정 입력 순서: Front, Right, Back, Left, Top, Bottom
- 면 교환, Horizontal/Vertical Flip, 정수 Offset과 입력 진단
- 단계형 `Source → Map & Align → Validate → Reconstruct` 흐름
- Add, Erase, Paint, Eyedropper, Box Select 복셀 편집
- 편집 스트로크 단위 Undo/Redo와 볼륨 Resize
- Pixel 2:1, True Isometric, Front, Right, Top, Free View 카메라
- 회전된 원본 6면을 기준으로 하는 카메라 스냅
- X/Y/Z 기즈모, 회전 미리보기, 속도 및 FPS 설정
- Lighting, Outline, Background 조정
- 현재 뷰 PNG, 4·8·16방향 시트와 Aseprite JSON 출력
- 회전 GIF 및 애니메이션 PNG 시트 출력
- Unity용 OBJ/MTL/팔레트 텍스처 패키지 출력
- 휴대용 `.pxv` 프로젝트 저장·불러오기
- OpenGL 뷰포트와 CPU fallback 렌더러

## 입력 시트

모든 슬롯은 동일한 캔버스 크기여야 하며 알파는 완전 투명 `0` 또는 완전 불투명 `255`를 사용합니다.

```text
┌───────┬───────┬──────┬──────┬─────┬────────┐
│ Front │ Right │ Back │ Left │ Top │ Bottom │
└───────┴───────┴──────┴──────┴─────┴────────┘
```

Import 후 각 면의 매핑과 정렬을 확인하고 Validation을 통과한 뒤 Reconstruct를 실행합니다.

## 요구 환경

- Windows 10/11
- 소스 빌드 시 .NET SDK 10

GitHub Release의 `Pixel2Voxel-win-x64.zip`은 self-contained 빌드이므로 별도 .NET 설치 없이 실행할 수 있습니다.

## 소스에서 실행

처음 실행하거나 코드가 변경된 경우:

```powershell
dotnet run --project src\PixelVoxel.App\PixelVoxel.App.csproj -c Debug
```

이미 Debug 빌드가 존재하는 경우:

```powershell
dotnet run --project src\PixelVoxel.App\PixelVoxel.App.csproj -c Debug --no-build
```

빌드된 실행 파일:

```text
src\PixelVoxel.App\bin\Debug\net10.0\Pixel2Voxel.exe
```

제품명은 Pixel2Voxel이지만 기존 코드와 프로젝트 호환성을 위해 소스 디렉터리 및 네임스페이스는 아직 `PixelVoxel.*`을 유지합니다.

## 빌드와 테스트

```powershell
dotnet restore
dotnet build
dotnet test
```

Windows 배포본을 직접 만들려면:

```powershell
dotnet publish src\PixelVoxel.App\PixelVoxel.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
  -o artifacts\Pixel2Voxel-win-x64
```

## Release 게시

`.github/workflows/release.yml`은 `v*` 태그가 GitHub에 push되면 다음 작업을 자동으로 수행합니다.

1. 전체 테스트 실행
2. Windows x64 self-contained 앱 publish
3. `Pixel2Voxel-win-x64.zip` 생성
4. GitHub Release 생성 및 ZIP 첨부

예를 들어 첫 버전을 게시하려면:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

Actions가 완료되면 README의 최신 Windows 다운로드 링크가 자동으로 해당 ZIP을 가리킵니다.

## 프로젝트 구조

- `src/PixelVoxel.App`: Avalonia UI와 데스크톱 실행 진입점
- `src/PixelVoxel.Core`: 좌표, 복셀 문서, Visual Hull 재구성
- `src/PixelVoxel.Imaging`: PNG 입력과 검증
- `src/PixelVoxel.Rendering`: 표면 메시, 카메라, CPU/OpenGL 렌더링
- `src/PixelVoxel.Export`: `.pxv`, PNG/GIF/OBJ 출력
- `src/PixelVoxel.Cli`: 비대화형 진입점
- `tests`: Unit, rendering, application, golden tests
- `docs`: 좌표계, 저장 형식, 재구성 규칙

## 저장 형식 호환성

브랜드는 Pixel2Voxel로 변경됐지만 기존 프로젝트와 도구 호환성을 위해 다음 식별자는 유지합니다.

- 프로젝트 확장자: `.pxv`
- manifest format: `PixelVoxel`
- Unity marker: `.pixelvoxel.json`
- 사용자 설정 경로: `%LOCALAPPDATA%\PixelVoxel\settings.json`

자세한 내용은 [FileFormat.md](docs/FileFormat.md)와 [CoordinateSystem.md](docs/CoordinateSystem.md)를 참고하세요.
