from PIL import Image
from pathlib import Path
root=Path(__file__).resolve().parents[1]
(root/'extension-tests/benchmark.rgba').write_bytes(Image.open(root/'benchmark/Inputs/ZZZ_Sheet.png').convert('RGBA').tobytes())
