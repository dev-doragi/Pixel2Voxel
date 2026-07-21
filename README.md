# Pixel Voxel

Pixel Voxel은 여섯 방향의 픽셀 PNG를 공간적인 셀 점유 정보로 재구성하고, 회전 가능한 저해상도 3D 픽셀 프레임으로 표시하는 독립 데스크톱 프로그램입니다.

## 현재 상태

현재 버전은 다음 작업 흐름을 제공합니다.

```text
6면 PNG → 좌표 정규화 → Visual Hull → 면별 색상 VoxelDocument
→ 노출 표면 캐시 → CPU/OpenGL 픽셀 framebuffer → 정수 배율 표시
```

- 정적 6×1 시트와 개별 6면 PNG 검사·정렬·Apply 작업 흐름
- 고정 순서: Front, Right, Back, Left, Top, Bottom
- 면 카드 교환, H/V Flip, 정수 Offset, 클리핑·알파 진단 및 최근 Import 10개
- Alpha 0/255만 허용
- Pixel 2:1 및 True Isometric 프리셋
- 우클릭 드래그 Free View, 6면 정렬 카메라 스냅과 휠 정수 확대
- 입력 픽셀 크기에 맞춘 회전 안전 framebuffer와 nearest-neighbor 표시
- 카메라와 독립된 수평·수직 360° 모델 회전 애니메이션
- 4단계 방향광, 1px 화면 공간 외곽선, 사용자 지정 배경색
- `%LOCALAPPDATA%\PixelVoxel\settings.json`에 뷰포트 설정 저장
- OpenGL 실패 시 CPU 기준 렌더러 fallback
- 현재 논리 뷰 PNG 및 Pixel 2:1/True Isometric 4·8·16방향 가로 시트 출력
- 방향 시트와 함께 Aseprite `json-array` 메타데이터 출력
- 3D 뷰포트 Add, Erase, Paint, 박스 Select와 축 단위 이동
- 편집 스트로크 단위 Undo/Redo 및 명시적인 볼륨 Resize
- 휴대용 `.pxv` 프로젝트 저장·불러오기와 미저장 변경 보호

스프라이트 프레임 애니메이션, 충돌 픽셀 수정, trim/packing, GIF/OBJ 출력은 아직 구현하지 않았습니다.

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
3. `Import 6×1 Sheet`를 눌러 PNG를 선택하거나 Sheet Drop Zone에 드롭합니다.
4. 여섯 카드를 확인하고 필요하면 카드를 드래그해 교환하거나 Flip/Offset을 조정합니다.
5. `Apply / Reconstruct`를 눌러 현재 정렬을 모델에 적용합니다.

### 개별 PNG

1. Import 패널에서 여섯 방향 버튼으로 각 PNG를 지정합니다.
2. 여섯 파일은 동일한 캔버스 크기를 사용해야 합니다.
3. `Inspect Assigned Views`를 눌러 카드 미리보기를 확인합니다.
4. `Apply / Reconstruct`를 눌러 모델에 적용합니다.

### PNG 출력

- `Export > Export Current View PNG...`: 현재 카메라의 논리 픽셀 프레임을 1× PNG로 저장합니다.
- `Export > Export Direction Sheet...`: 4·8·16방향 프레임을 같은 캔버스의 가로 PNG로 저장하고 같은 이름의 Aseprite JSON을 생성합니다.
- 방향 출력은 현재 GL 화면과 UI 확대 배율을 사용하지 않고 결정론적인 CPU 픽셀 렌더러를 사용합니다.
- 기본 출력은 투명 배경이며 우측 Sprite Export 패널에서 현재 배경 포함, 방향 수, Pixel 2:1/True Isometric을 선택할 수 있습니다.

### 복셀 편집과 프로젝트

- 우측 Voxel Editor에서 Add, Erase, Paint, Select 도구를 선택하고 뷰포트를 좌클릭 또는 드래그합니다.
- Paint 중 Shift를 누르면 선택 복셀의 여섯 면을 같은 색으로 칠합니다.
- Select는 두 복셀을 차례로 클릭해 축 정렬 영역을 만들며 `±X/±Y/±Z` 버튼으로 이동합니다.
- `Ctrl+Z`, `Ctrl+Y`, `Delete`, `Escape`로 Undo, Redo, 선택 삭제, 선택 해제를 실행합니다.
- `File` 메뉴에서 `.pxv` 프로젝트를 저장하거나 불러옵니다. 프로젝트에는 현재 복셀 문서와 정렬된 직교 이미지가 포함됩니다.

Viewport에서 프리셋을 선택하거나 오른쪽 버튼으로 드래그해 독립된 카메라로 모델을 둘러볼 수 있습니다. 휠을 처음 움직이면 현재 Fit 배율에서 Manual 1×~16× 정수 배율로 전환됩니다. 수평·수직 Animation은 모델 자체의 회전을 제어하므로 카메라를 움직여도 회전 각도와 진행 속도가 유지됩니다. 우측 패널에서 디렉셔널 라이트 회전, 외곽선과 배경색을 조정할 수 있습니다.
