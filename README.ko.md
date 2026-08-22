<h1 align="center">Pixel2Voxel</h1>

<p align="center">
  <img width="315" height="315" alt="Pixel2Voxel 회전 미리보기" src="https://github.com/user-attachments/assets/3e083337-730b-4402-990e-5b5e2671ecb4" />
</p>

<p align="center">
  <a href="README.md">English</a> · 한국어
</p>

**Pixel2Voxel(P2V)**은 여섯 방향의 정사영 픽셀 아트 이미지로부터 편집 가능한 복셀 모델을 재구성하는 Windows 데스크톱 편집기입니다.

Front, Right, Back, Left, Top, Bottom 이미지를 가져와 복셀 볼륨으로 재구성하고, 결과를 편집하고, 여러 각도에서 미리 본 뒤 스프라이트 또는 3D 에셋으로 내보낼 수 있습니다.

## 다운로드

[최신 Windows 버전 다운로드](https://github.com/dev-doragi/Pixel2Voxel/releases/latest/download/Pixel2Voxel-win-x64.zip)

모든 버전과 변경 사항은 [GitHub Releases](https://github.com/dev-doragi/Pixel2Voxel/releases)에서 확인할 수 있습니다.

### 요구 환경

- Windows 10 / 11
- 별도의 .NET 설치 불필요

`Pixel2Voxel-win-x64.zip`을 내려받아 압축을 푼 뒤 `Pixel2Voxel.exe`를 실행하세요.

## 작동 방식

Pixel2Voxel은 AI 이미지-3D 생성기가 아닙니다. 여섯 방향 정사영 이미지의 실루엣 교집합으로 복셀 볼륨을 재구성합니다.

```text
Front ∩ Right ∩ Back
∩ Left ∩ Top ∩ Bottom
= reconstructed voxel volume
```

각 후보 복셀을 선택한 입력 이미지에 투영하여 모든 시점의 가시 영역을 만족하는 복셀만 남깁니다. 노출된 표면에는 원본 이미지의 RGBA 색상을 보존합니다.

실루엣 기반 재구성이므로 선택한 입력 화면에 나타나지 않는 내부 공간이나 숨겨진 오목 구조는 자동으로 복원할 수 없습니다.

## 작업 흐름

```text
이미지 가져오기
      ↓
매핑 및 정렬
      ↓
검증
      ↓
재구성
      ↓
복셀 편집
      ↓
미리보기 및 내보내기
```

개별 PNG 이미지와 `6×1` 스프라이트 시트를 모두 지원합니다. 입력 순서는 다음과 같습니다.

```text
┌───────┬───────┬──────┬──────┬─────┬────────┐
│ Front │ Right │ Back │ Left │ Top │ Bottom │
└───────┴───────┴──────┴──────┴─────┴────────┘
```

이미지를 가져온 뒤 시점 매핑, 좌우·상하 반전, 정수 오프셋을 조정하고 검증을 거쳐 복셀 모델을 재구성할 수 있습니다.

## 주요 기능

### 재구성

- 1~6방향 정사영 복셀 재구성 및 미관측 축 길이 지정
- 개별 PNG 또는 `6×1` 스프라이트 시트 가져오기
- Front / Right / Back / Left / Top / Bottom 매핑
- 좌우 및 상하 반전
- 정수 이미지 오프셋
- 입력 검증과 진단

### 복셀 편집

- 추가, 삭제, 페인트, 스포이트, 박스 선택 도구
- 실행 취소 / 다시 실행
- 볼륨 크기 변경

### 카메라 및 미리보기

- Pixel 2:1, True Isometric, Front, Right, Top, Free View 카메라
- 원본 시점 기준 카메라 스냅
- X / Y / Z 회전 조작
- 속도와 FPS를 조절할 수 있는 회전 미리보기
- Aseprite PNG+JSON 면 애니메이션 가져오기와 duration 기반 타임라인 재생
- face별 재투영 실루엣 충돌 진단 및 원본 마스크 수정
- 프레임별 독립 복셀 편집 및 프로젝트 전체 Undo/Redo
- 조명, 외곽선, 배경 설정

### 내보내기

- 현재 화면 PNG
- 4 / 8 / 16방향 스프라이트 시트
- 애니메이션 PNG 시트 및 회전 GIF
- Aseprite JSON
- trim + MaxRects 단일 페이지 아틀라스(2px padding, 1px extrusion)
- Unity용 OBJ / MTL / 팔레트 텍스처 패키지

### 프로젝트

- 휴대용 `.pxv` 프로젝트 저장 및 불러오기
- OpenGL 뷰포트와 CPU 대체 렌더러

## 입력 지침

- 선택한 모든 시점에 같은 캔버스 크기를 사용하세요.
- 정사영 이미지를 사용하세요.
- 완전 투명(`alpha = 0`) 픽셀은 빈 공간이며, `alpha = 1~255` 픽셀은 투명도를 보존한 복셀 면으로 가져옵니다.
- 모든 시점에서 오브젝트를 일관되게 정렬하세요.
- 재구성 전에 매핑과 정렬을 확인하세요.

Pixel2Voxel은 반전과 정수 오프셋으로 작은 정렬 차이를 보정할 수 있지만, 원본 이미지를 일관되게 정렬할수록 더 좋은 결과를 얻을 수 있습니다.

## 프로젝트 파일

Pixel2Voxel 프로젝트 파일은 `.pxv` 확장자를 사용합니다. 이전 버전과의 호환성을 위해 일부 내부 식별자는 여전히 `PixelVoxel` 이름을 사용합니다.

- 프로젝트 확장자: `.pxv`
- Manifest 형식: `PixelVoxel`
- Unity marker: `.pixelvoxel.json`
- 설정 경로: `%LOCALAPPDATA%\PixelVoxel\settings.json`

기술적인 세부 내용은 [FileFormat.md](docs/FileFormat.md)와 [CoordinateSystem.md](docs/CoordinateSystem.md)를 참고하세요.

## 소스에서 빌드

소스에서 빌드하려면 .NET 10 SDK가 필요합니다.

```powershell
dotnet restore
dotnet build
dotnet test
```

데스크톱 앱 실행:

```powershell
dotnet run --project src\PixelVoxel.App\PixelVoxel.App.csproj -c Debug
```

프로젝트 컨테이너 검사 및 CI 검증:

```powershell
dotnet run --project src\PixelVoxel.Cli -- inspect model.pxv
dotnet run --project src\PixelVoxel.Cli -- validate model.pxv
```

Windows x64 self-contained 빌드 생성:

```powershell
dotnet publish src\PixelVoxel.App\PixelVoxel.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
  -o artifacts\Pixel2Voxel-win-x64
```

제품명은 **Pixel2Voxel**이지만 기존 코드와의 호환성을 위해 소스 디렉터리와 네임스페이스는 현재 `PixelVoxel.*` 이름을 유지합니다.
