# Coordinate System

## Unity OBJ export

- Model +Y maps to Unity up and model +Z maps to Unity forward.
- One voxel edge equals one Unity unit.
- X and Z are translated so the model bottom center is the exported origin; Y=0 remains the floor.
- Exported faces retain the surface mesher's outward winding and normals.

## Camera face snap

- Free View snaps to the six canonical sheet faces while orbiting.
- Canonical sheet-face normals follow the current object rotation; snapping never targets an unrelated world-axis face.
- Releasing pan or the rotation gizmo does not commit a camera snap. Only an orbit release commits the detent.
- The snap range is user-configurable from 1 to 30 degrees and defaults to 10 degrees.
- The unsnapped orbit path is retained so dragging beyond the range exits the detent smoothly.

## Rotation gizmo

- Gizmo drags are evaluated from the quaternion captured at pointer-down, so the ring geometry and axis do not drift during a drag or collapse back to Euler composition.
- Gizmo rotation consumes relative pointer movement only: right/down are positive and left/up are negative.
- During a Windows gizmo drag the pointer is recentered after each movement, allowing an unbounded drag without producing rotation while the mouse is stationary.
- X edits Pitch, Y edits Yaw, and Z edits Roll as deterministic Euler controls.
- Pointer motion is sign-corrected from downward-positive screen coordinates to the gizmo's visible positive rotation direction.
- Reset Camera + Rotation restores both the Pixel 2:1 camera and the model rotation, and stops rotation animation.
- Reset Rotation stops every rotation animation axis before restoring the model identity, so the next animation tick cannot rotate it again.
- A full view reset also clears camera pan and returns viewport zoom to Fit.

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
- 카메라와 오브젝트의 권위 회전 상태는 정규화된 quaternion이며 Euler 값은 입력·표시·기존 저장 형식 호환에 사용한다.
- 카메라 Yaw는 월드 +Y, Pitch는 yaw가 적용된 로컬 +X를 기준으로 하며 Roll은 사용하지 않는다.
- 뷰포트 조작은 우클릭 Orbit, 중클릭 Pan, 휠 Zoom을 사용한다.
- 오브젝트 X/Y/Z Gizmo는 현재 오브젝트의 로컬 축을 사용하고 바깥 View Roll 링은 현재 카메라 시선축을 사용한다.
- Yaw/Pitch/Roll Animation은 볼륨 경계의 기하 중심을 피벗으로 사용한다. 재생 시작 quaternion과 시트가 정의한 고정 모델 +X/+Y/+Z 축에 절대 시간 각도를 합성하므로 프레임 누적 오차나 축 표류가 없다.
- 모델 회전과 카메라 회전은 렌더 프레임에서 합성하며 서로의 각도 상태를 덮어쓰지 않는다.
- Free View 카메라는 축 정렬 면에서 2° 이내일 때 해당 면의 정면 각도로 스냅할 수 있다.
- 면 정렬은 카메라 시선과 면 노멀이 평행한 상태이며 `|dot|`이 1에 가까운 조건이다.
- 스냅 중에는 해당 면의 표준 평면도 방향을 사용한다. Top과 Bottom은 현재 yaw에 가장 가까운 90° 단위로 임시 정렬하여 화면상 직교 방향을 유지한다.
- 우클릭 드래그 중에는 스냅 결과가 원본 Free View 각도를 변경하지 않아 계속 드래그하면 자연스럽게 판정 범위를 벗어날 수 있다.
- 스냅된 상태에서 우클릭을 놓으면 표시 중인 스냅 yaw/pitch를 다음 드래그의 시작 회전값으로 확정한다.

## 아직 미확정

- 출력 스프라이트 방향 인덱스와 회전 순서
- 외부 3D 형식과의 좌표 변환 규칙

`.pxv` version 1은 위 좌표 규칙을 `XYZ-RightUpFront-v1`로 manifest에 기록한다.
