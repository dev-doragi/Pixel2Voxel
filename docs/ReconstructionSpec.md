# Reconstruction Specification

## v1 입력

- 정적 6×1 PNG: Front, Right, Back, Left, Top, Bottom 고정 순서
- 사용자가 각 면을 지정하는 개별 PNG 여섯 장
- 여섯 이미지는 같은 캔버스 크기를 사용한다.
- Alpha 0은 빈 공간, Alpha 255는 점유 픽셀이다.
- Alpha 1~254와 빈 면은 입력 오류다.

## 정규화

재구성 전에 원본 PNG는 비파괴 draft로 검사한다. Target Face별 변환은 `Horizontal/Vertical Flip → 정수 Offset → 원본 크기 캔버스 클리핑` 순서이며 정렬 변경만으로 기존 `VoxelDocument`를 교체하지 않는다. 차단 오류가 없고 사용자가 Apply를 선택했을 때만 아래 정규화와 재구성을 실행한다.

1. 각 면의 불투명 픽셀을 `FaceCoordinateTransforms`로 모델 축에 투영한다.
2. X, Y, Z별로 관측된 범위의 합집합을 구한다.
3. 각 축 최소값을 0으로 이동하고 `max - min + 1`로 크기를 구한다.
4. 관측되지 않은 축은 길이 1로 둔다.

## Visual Hull

`IVoxelReconstructor.Reconstruct(OrthographicViewSet)`은 정확히 여섯 면을 가정하지 않는다. 각 후보 셀을 제공된 모든 뷰로 투영하고 모든 알파가 255일 때만 점유시킨다.

- 각 점유 셀은 제공된 방향별 원본 RGBA를 별도로 보존한다.
- 다른 방향 색상을 임의 fallback으로 사용하지 않는다.
- 후보 공간 한도: 1,048,576셀
- 노출 표면 캐시 한도: 500,000면
- 표면 한도 초과 시 `VoxelDocument`는 유지하고 `VoxelMeshData`만 생성하지 않는다.

## 파생 렌더 데이터

`VoxelMeshData`는 이웃이 비어 있는 면만 포함한다. 면마다 정점 4개와 인덱스 6개를 독립적으로 가지며 모델 원본이나 프로젝트 저장 데이터가 아니다.

## 이후 구현

- 1·2·3면 입력의 사용자 지정 깊이/보정 정책
- 충돌 진단 UI와 해결 정책
- 불완전 실루엣 보정
- 충돌 진단을 원본 픽셀 수정과 연결하는 작업 흐름
- 애니메이션 프레임 정렬
