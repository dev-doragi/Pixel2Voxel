# Pixel Voxel

Pixel Voxel은 여섯 방향의 픽셀 PNG를 공간적인 셀 점유 정보로 재구성하고, 회전 가능한 저해상도 3D 픽셀 프레임으로 표시하는 독립 데스크톱 프로그램입니다.

## 현재 상태

현재 버전은 다음 작업 흐름을 제공합니다.

```text
6면 PNG → 좌표 정규화 → Visual Hull → 면별 색상 VoxelDocument
→ 노출 표면 캐시 → CPU/OpenGL 픽셀 framebuffer → 정수 배율 표시
```

- 정적 6×1 시트와 개별 6면 PNG 지원
- 고정 순서: Front, Right, Back, Left, Top, Bottom
- Alpha 0/255만 허용
- Pixel 2:1 및 True Isometric 프리셋
- 우클릭 드래그 Free View와 휠 정수 확대
- 입력 픽셀 크기에 맞춘 회전 안전 framebuffer와 nearest-neighbor 표시
- 카메라와 독립된 수평·수직 360° 모델 회전 애니메이션
- 4단계 방향광, 1px 화면 공간 외곽선, 사용자 지정 배경색
- `%LOCALAPPDATA%\PixelVoxel\settings.json`에 뷰포트 설정 저장
- OpenGL 실패 시 CPU 기준 렌더러 fallback

편집, 스프라이트 프레임 애니메이션, 프로젝트 저장, PNG/GIF/OBJ 출력은 아직 구현하지 않았습니다.

## 솔루션 구조

- `src/PixelVoxel.App`: Avalonia UI와 GL 컨텍스트 수명주기
- `src/PixelVoxel.Core`: 좌표, 이미지, Visual Hull, 문서 데이터
- `src/PixelVoxel.Rendering`: 표면 캐시, 공통 카메라, CPU/Silk.NET 렌더러
- `src/PixelVoxel.Imaging`: ImageSharp 기반 PNG 입력과 검증
- `src/PixelVoxel.Export`: 저장 및 출력 계약
- `src/PixelVoxel.Cli`: 비대화형 진입점
- `tests/`: Core, Imaging, Rendering 및 golden 테스트
- `benchmark/`: 기준 입력, 수치, 관찰 기록

## 요구 환경

- .NET SDK 10.0.301 이상
- Windows 우선 지원

## 빌드와 테스트

```powershell
dotnet restore
dotnet build
dotnet test
```

## 실행

```powershell
dotnet run --project src\PixelVoxel.App --no-build
```

빌드된 Windows 실행 파일은 다음 경로에 생성됩니다.

```text
src\PixelVoxel.App\bin\Debug\net10.0\PixelVoxel.App.exe
```

## 사용 방법

### 6×1 시트

1. Aseprite에서 한 행에 여섯 개의 동일 크기 슬롯으로 PNG를 내보냅니다.
2. 순서를 Front, Right, Back, Left, Top, Bottom으로 배치합니다.
3. `Import 6×1 Sheet`를 눌러 PNG를 선택합니다.

### 개별 PNG

1. Import 패널에서 여섯 방향 버튼으로 각 PNG를 지정합니다.
2. 여섯 파일은 동일한 캔버스 크기를 사용해야 합니다.
3. `Reconstruct Separate Views`를 누릅니다.

Viewport에서 프리셋을 선택하거나 오른쪽 버튼으로 드래그해 독립된 카메라로 모델을 둘러볼 수 있습니다. 휠을 처음 움직이면 현재 Fit 배율에서 Manual 1×~16× 정수 배율로 전환됩니다. 수평·수직 Animation은 모델 자체의 회전을 제어하므로 카메라를 움직여도 회전 각도와 진행 속도가 유지됩니다. 우측 패널에서 디렉셔널 라이트 회전, 외곽선과 배경색을 조정할 수 있습니다.
