# File Format

Pixel Voxel 프로젝트 저장 형식은 아직 확정되지 않았다. `IProjectSerializer`는 계약 위치만 예약하며 현재 버전에는 직렬화 구현이나 파일 확장자가 없다.

형식을 결정하기 전에 다음 항목을 정의해야 한다.

- 형식 버전과 호환성 정책
- 좌표계 및 모델 기준점 표현
- 복셀 점유와 색상 데이터 표현
- 원본 직교 이미지의 포함 또는 외부 참조 여부
- 메타데이터와 향후 확장 필드 처리

`VoxelMeshData`, OpenGL 버퍼, 텍스처, framebuffer는 파생 캐시이므로 저장 형식에 포함하지 않는다. 저장 가능한 원본 후보는 `OrthographicViewSet`과 `VoxelDocument`다.
