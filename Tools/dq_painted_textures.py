"""Codex 이미지 생성으로 받은 이어 붙는 질감을 본 게임 색 평균에 맞춰 DQPainted 폴더에 쓴다.

색은 예전 절차 질감의 평균색(빛 값이 그 색에 맞춰져 있음)으로 옮기고, 그려진 세부(풀잎·흙알·판석·나뭇결·천 결)는 그대로 둔다.
Grass·Leaves는 밝기 세부만 쓰는 회색 지도(평균 0.5)다 — 색은 셰이더가 정한다.
사용: /usr/bin/python3 Tools/dq_painted_textures.py <생성 폴더> <출력 폴더>
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

SRC = Path(sys.argv[1])
OUT = Path(sys.argv[2])
# (출력 이름, 원본, 평균 sRGB 목표 또는 None=회색 세부)
JOBS = [
    ("Grass", "A/grass.png", None),
    ("Soil", "A/dirt.png", (.72, .64, .44)),
    ("StonePath", "D/flagstone.png", (.62, .58, .48)),
    ("Wood", "B/wood.png", (.48, .30, .13)),
    ("Cloth", "C/canvas.png", (.80, .69, .43)),
    ("Flag", "C/redcloth.png", (.56, .17, .14)),
    ("Leaves", "C/leaves.png", None),
    ("Plant", "C/leaves.png", (.22, .58, .19)),
]


def wrap_blur(values, radius):
    """Gaussian blur that wraps around the tile edges (FFT is periodic, so the texture stays tileable)."""
    size = values.shape[0]
    freq = np.fft.fftfreq(size)
    gauss = np.exp(-2 * (np.pi * radius) ** 2 * (freq[:, None] ** 2 + freq[None, :] ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(values) * gauss))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, source, target in JOBS:
        a = np.asarray(Image.open(SRC / source).convert("RGB").resize((1024, 1024), Image.LANCZOS)).astype(np.float64) / 255
        if target is None:
            luma = .299 * a[..., 0] + .587 * a[..., 1] + .114 * a[..., 2]
            ratio = luma / luma.mean()
            if name == "Grass":
                # Keep only fine blade grain: mid-size blotches read as ripples when tiled; big light comes from the shader.
                ratio = ratio - wrap_blur(ratio, 12) + 1
            out = np.clip(.5 * ratio, 0, 1)
            out = np.repeat(out[..., None], 3, axis=2)
        else:
            out = np.clip(a * (np.array(target) / a.reshape(-1, 3).mean(0)), 0, 1)
        path = OUT / f"{name}.png"
        Image.fromarray((out * 255 + .5).astype(np.uint8)).save(path, optimize=False)
        print(path, "mean", np.round(out.reshape(-1, 3).mean(0), 3))


if __name__ == "__main__":
    main()
