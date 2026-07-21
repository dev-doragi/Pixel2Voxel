# Coordinate System

## v1 모델 좌표

- +X: 모델의 오른쪽
- +Y: 모델의 위쪽
- +Z: 모델의 전방
- 이미지 U: 오른쪽
- 이미지 V: 아래쪽
- PNG 원점: 좌상단
- 모델 회전 기준점: 복셀 경계 상자의 기하학적 중심

면별 이미지 좌표 변환은 `FaceCoordinateTransforms` 한 곳에서 정의한다.

| Face | Image U | Image V |
|---|---|---|
| Front | +X | -Y |
| Back | -X | -Y |
| Right | -Z | -Y |
| Left | +Z | -Y |
| Top | +X | +Z |
| Bottom | +X | -Z |

축 범위는 관련 면의 불투명 픽셀을 위 표에 따라 변환한 뒤 합집합으로 계산한다. 전역 최소 좌표를 0으로 평행 이동하여 복셀 좌표를 만든다.

## 카메라

- Pixel 2:1: yaw -45°, pitch -30°, 화면 축 기울기 약 26.565°
- True Isometric: yaw -45°, pitch 약 -35.264°
- Pixel Preview는 투영된 모델 중심과 pan을 내부 프레임버퍼 정수 픽셀에 맞춘다.
- Free View는 연속 yaw와 pitch를 허용한다.
- 마우스 입력은 카메라의 world-to-view 회전만 변경한다.
- 수평·수직 Animation은 모델 중심점 기준 object-to-world 회전만 변경한다.
- Object Tilt는 모델 중심점 기준 로컬 Z축 roll을 변경하며 yaw/pitch와 함께 합성한다.
- View 모드에서 `Shift+좌클릭 드래그`는 카메라 대신 Object Tilt를 변경한다.
- 모델 회전과 카메라 회전은 렌더 프레임에서 합성하며 서로의 각도 상태를 덮어쓰지 않는다.
- Free View 카메라는 축 정렬 면에서 5° 이내일 때 해당 면의 정면 각도로 스냅할 수 있다.
- 면 정렬은 카메라 시선과 면 노멀이 평행한 상태이며 `|dot|`이 1에 가까운 조건이다.
- 스냅 중에는 해당 면의 표준 평면도 방향을 사용한다. Top과 Bottom은 현재 yaw에 가장 가까운 90° 단위로 임시 정렬하여 화면상 직교 방향을 유지한다.
- 우클릭 드래그 중에는 스냅 결과가 원본 Free View 각도를 변경하지 않아 계속 드래그하면 자연스럽게 판정 범위를 벗어날 수 있다.
- 스냅된 상태에서 우클릭을 놓으면 표시 중인 스냅 yaw/pitch를 다음 드래그의 시작 회전값으로 확정한다.

## 아직 미확정

- 출력 스프라이트 방향 인덱스와 회전 순서
- 외부 3D 형식과의 좌표 변환 규칙

`.pxv` version 1은 위 좌표 규칙을 `XYZ-RightUpFront-v1`로 manifest에 기록한다.
