# Product Requirements

## Convenience workflow additions

- Resizable, independently hideable import and inspector docks with persisted layout state.
- Searchable inspector categories for editing, camera, animation, rendering, and export.
- Project-owned 32-color palette and exact pre-lighting voxel-face eyedropper.
- Rotation capture to looping GIF or Aseprite-compatible PNG sheet plus JSON.
- Unity-oriented OBJ/MTL/palette package with a scoped pixel-texture AssetPostprocessor.

## 목적

Pixel2Voxel(P2V)은 직교 PNG 이미지의 Front, Side, Top 뷰를 입력으로 받아 편집 가능한 3D 픽셀 모델을 만들고 여러 방향의 PNG 스프라이트로 출력하는 독립 데스크톱 프로그램을 목표로 한다.

## 대상 환경

- C# 및 .NET 10
- Avalonia UI
- Windows 우선 지원
- 이후 macOS와 Linux 지원
- Silk.NET 기반 OpenGL 렌더링
- ImageSharp 기반 PNG 처리

## 현재 구현 범위

- Aseprite 정적 6×1 PNG 및 개별 6면 PNG 비파괴 검사
- Target Face 카드 교환, H/V Flip, 정수 Offset과 Apply/Reconstruct 분리
- 중간 알파, 빈 면, 크기 불일치, 클리핑 진단과 최근 Import 10개
- Alpha 0/Alpha > 0 점유 판정과 공통 좌표계 정규화
- 제공된 알파 실루엣 교집합 기반 visual-hull 복셀 재구성
- 셀마다 방향별 원본 RGBA 보존
- 노출 면 기반 비영속 렌더 캐시
- Silk.NET OpenGL 저해상도 FBO와 CPU 기준 렌더러
- Pixel 2:1, True Isometric, Front, Right, Top 및 Free View
- nearest-neighbor 정수 배율 Viewport
- 현재 논리 뷰 1× PNG 출력
- Pixel 2:1/True Isometric 카메라 기반 4·8·16방향 가로 PNG 및 Aseprite JSON 출력
- 3D 뷰포트 복셀 추가·삭제·면 색상 편집과 박스 선택 이동
- 편집 스트로크 단위 Undo/Redo와 명시적인 볼륨 크기 변경
- 선택 및 호버의 논리 1px 픽셀 외곽선
- 휴대용 `.pxv` version 3 프로젝트 저장·불러오기와 v1/v2 읽기 호환
- Alpha 1~255 면 색상 보존과 후면→전면 source-over CPU 기준 렌더링
- 1~6면 부분 입력과 미관측 축의 사용자 지정 길이
- Aseprite PNG+JSON 면 애니메이션 동기 검사, 프레임별 재구성·편집·재생·저장
- 알파 경계 trim, 결정적 MaxRects 단일 atlas packing, 2px padding, 1px edge extrusion,
  최대 4096×4096 및 원본 오프셋·피벗·duration Aseprite 호환 JSON
- 동일한 face 좌표 변환으로 복셀을 원본 뷰에 재투영한 실루엣 충돌 진단과 변환된
  원본 마스크 픽셀 오버레이·수정
- 회전 GIF 및 Unity OBJ 패키지 출력
