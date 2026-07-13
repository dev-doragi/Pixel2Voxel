# Benchmark Log

| Date | Input | Implementation | Result | Observation |
|---|---|---|---|---|
| 2026-07-13 | `ZZZ_Sheet.png` 192×32 | Strict six-view import, union normalization, visual hull | 20×22×12, 5,280 candidates, 5,124 occupied | Front/Back validates horizontal orientation. Other opposite pairs require asymmetric fixture. |

세부 입력 통계와 CPU framebuffer SHA-256은 `benchmark/Expected/ZZZ_Sheet.metrics.json`에 기록한다.
