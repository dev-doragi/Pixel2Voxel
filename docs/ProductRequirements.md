# Product Requirements

## 목적

Pixel Voxel은 직교 PNG 이미지의 Front, Side, Top 뷰를 입력으로 받아 편집 가능한 3D 픽셀 모델을 만들고 여러 방향의 PNG 스프라이트로 출력하는 독립 데스크톱 프로그램을 목표로 한다.

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
- 이진 알파 검증과 공통 좌표계 정규화
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
- 휴대용 `.pxv` version 1 프로젝트 저장·불러오기

## 아직 구현하지 않은 범위

- 애니메이션 프레임 정렬과 재생
- 반투명 픽셀 및 블렌딩
- 충돌 픽셀 시각화·수정 도구
- 스프라이트 trim 및 atlas packing
- GIF 및 OBJ 출력
