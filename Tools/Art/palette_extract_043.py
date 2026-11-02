"""043 palette extraction: day.png -> 8x4 palette (64x32 PNG), JSON record, swatch board.

Usage (project root, system Python with numpy/Pillow):
  /usr/bin/python3 -I Tools/Art/palette_extract_043.py <day.png> <out-dir>

Deterministic: fixed seed, no network. Writes only inside <out-dir>, which must not
already contain palette outputs.
"""
import hashlib
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

# numpy 2.x with macOS Accelerate emits spurious matmul FP warnings on valid input;
# non-finite results are rejected explicitly in main() instead.
np.seterr(all="ignore")

COLS, ROWS, CELL = 8, 4, 8
SEED = 43001
ROW_NAMES = ["grass_foliage", "soil_path", "stone", "water_accent"]


def srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def rgb_to_oklab(rgb):
    lin = srgb_to_linear(rgb)
    m1 = np.array([[0.4122214708, 0.5363325363, 0.0514459929],
                   [0.2119034982, 0.6806995451, 0.1073969566],
                   [0.0883024619, 0.2817188376, 0.6299787005]])
    lms = np.cbrt(lin @ m1.T)
    m2 = np.array([[0.2104542553, 0.7936177850, -0.0040720468],
                   [1.9779984951, -2.4285922050, 0.4505937099],
                   [0.0259040371, 0.7827717662, -0.8086757660]])
    return lms @ m2.T


def oklab_to_rgb(lab):
    m2i = np.array([[1.0, 0.3963377774, 0.2158037573],
                    [1.0, -0.1055613458, -0.0638541728],
                    [1.0, -0.0894841775, -1.2914855480]])
    lms = (lab @ m2i.T) ** 3
    m1i = np.array([[4.0767416621, -3.3077115913, 0.2309699292],
                    [-1.2684380046, 2.6097574011, -0.3413193965],
                    [-0.0041960863, -0.7034186147, 1.7076147010]])
    return linear_to_srgb(lms @ m1i.T)


def kmeans(x, k, rng, iters=40):
    if len(x) <= k:
        return x.copy(), np.arange(len(x))
    # k-means++ init
    centers = [x[rng.integers(len(x))]]
    for _ in range(1, k):
        d = np.min(((x[:, None, :] - np.array(centers)[None]) ** 2).sum(-1), axis=1)
        centers.append(x[rng.choice(len(x), p=d / d.sum())])
    c = np.array(centers)
    for _ in range(iters):
        lab = np.argmin(((x[:, None, :] - c[None]) ** 2).sum(-1), axis=1)
        nc = np.array([x[lab == i].mean(0) if np.any(lab == i) else c[i] for i in range(k)])
        if np.allclose(nc, c, atol=1e-6):
            break
        c = nc
    lab = np.argmin(((x[:, None, :] - c[None]) ** 2).sum(-1), axis=1)
    return c, lab


def classify(lab):
    L, a, b = lab[:, 0], lab[:, 1], lab[:, 2]
    C = np.hypot(a, b)
    h = (np.degrees(np.arctan2(b, a)) + 360) % 360
    grp = np.full(len(lab), -1)
    stone = C < 0.035
    green = (~stone) & (h >= 100) & (h < 175)
    soil = (~stone) & (h >= 45) & (h < 100) & (C < 0.14)
    water = (~stone) & (h >= 175) & (h < 280)
    accent = (~stone) & ~green & ~soil & ~water
    grp[green], grp[soil], grp[stone], grp[water], grp[accent] = 0, 1, 2, 3, 4
    return grp


def main():
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    src, out = Path(sys.argv[1]), Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    for name in ("palette.png", "palette.json", "board.png"):
        if (out / name).exists():
            raise SystemExit(f"OUTPUT_EXISTS: {out / name}")
    raw = src.read_bytes()
    img = Image.open(src).convert("RGB")
    W, H = img.size
    f = 3
    small = img.resize((W // f, H // f), Image.Resampling.BOX)
    px = np.asarray(small, dtype=np.float64).reshape(-1, 3) / 255.0
    lab = rgb_to_oklab(px)
    finite = np.isfinite(lab).all(1)
    if not finite.all():
        raise SystemExit("NON_FINITE_OKLAB: %d" % int((~finite).sum()))
    keep = lab[:, 0] < 0.97  # drop specular sparkles
    sw, sh = small.size
    coords = np.stack(np.meshgrid(np.arange(sw), np.arange(sh)), -1).reshape(-1, 2) * f + f // 2
    lab, px, coords = lab[keep], px[keep], coords[keep]
    grp = classify(lab)
    rng = np.random.default_rng(SEED)

    # row plan: 0 green 8, 1 soil 8, 2 stone 8, 3 water 4 + accents 4
    plan = [(0, [0], 8), (1, [1], 8), (2, [2], 8), (3, [3], 4), (3, [4], 4)]
    rows = {r: [] for r in range(ROWS)}
    total = len(lab)
    for row, groups, k in plan:
        mask = np.isin(grp, groups)
        x = lab[mask]
        if len(x) == 0:
            continue
        sub = x[rng.choice(len(x), size=min(len(x), 6000), replace=False)]
        centers, _ = kmeans(sub, k, rng)
        full = np.argmin(((x[:, None, :] - centers[None]) ** 2).sum(-1), axis=1)
        cx = coords[mask]
        for i, cen in enumerate(centers):
            members = full == i
            share = float(members.sum()) / total
            if share == 0:
                continue
            j = np.argmin(((x - cen) ** 2).sum(-1))
            rows[row].append({"lab": cen, "share": share, "sample_px": [int(cx[j][0]), int(cx[j][1])],
                              "group": ["grass_foliage", "soil_path", "stone", "water", "accent"][groups[0]]})
    cells = []
    pal = np.zeros((ROWS * CELL, COLS * CELL, 4), dtype=np.uint8)
    for r in range(ROWS):
        if r == 3:
            ordered = sorted([c for c in rows[r] if c["group"] == "water"], key=lambda c: c["lab"][0]) + \
                      sorted([c for c in rows[r] if c["group"] == "accent"], key=lambda c: -c["share"])
        else:
            ordered = sorted(rows[r], key=lambda c: c["lab"][0])
        ordered = ordered[:COLS]
        for col, c in enumerate(ordered):
            rgb = np.clip(oklab_to_rgb(c["lab"][None])[0], 0, 1)
            rgb8 = [int(round(v * 255)) for v in rgb]
            # palette texture rows: row 0 at the TOP of the PNG; Unity UV v=0 is the bottom,
            # so the UV for row r is v=(ROWS-1-r+.5)/ROWS. Recorded per cell below.
            y0 = r * CELL
            pal[y0:y0 + CELL, col * CELL:(col + 1) * CELL, :3] = rgb8
            pal[y0:y0 + CELL, col * CELL:(col + 1) * CELL, 3] = 255
            L, a, b = c["lab"]
            cells.append({
                "index": r * COLS + col, "row": r, "col": col, "row_name": ROW_NAMES[r], "group": c["group"],
                "hex": "#%02x%02x%02x" % tuple(rgb8), "oklab": [round(float(L), 4), round(float(a), 4), round(float(b), 4)],
                "uv": [round((col + 0.5) / COLS, 6), round((ROWS - 1 - r + 0.5) / ROWS, 6)],
                "pixel_share": round(c["share"], 5), "source_sample_px": c["sample_px"], "emissive": 0.0})
    Image.fromarray(pal).save(out / "palette.png")
    # self-check: every cell interior is a single color
    chk = np.asarray(Image.open(out / "palette.png"))
    uniform = all(len(np.unique(chk[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL].reshape(-1, 4), axis=0)) == 1
                  for r in range(ROWS) for c in range(COLS))
    record = {
        "status": "PROPOSED_PENDING_USER_APPROVAL",
        "source": str(src), "source_sha256": hashlib.sha256(raw).hexdigest(), "source_size": [W, H],
        "method": "BOX downsample x3, drop OkL>=0.97, OKLCh groups (stone C<.035; green 100-175deg; soil 45-100deg C<.14; water 175-280deg; accent rest), per-group k-means seed %d" % SEED,
        "layout": {"cols": COLS, "rows": ROWS, "cell_px": CELL, "row_names": ROW_NAMES, "png_row0": "top", "alpha": "1=non-emissive; emission=(1-A)"},
        "cells_uniform": uniform, "filled_cells": len(cells),
        "palette_sha256": hashlib.sha256((out / "palette.png").read_bytes()).hexdigest(),
        "note": "day.png is a lit key-art image; colors are 'sunlit top face as seen', not measured albedo.",
        "cells": cells,
    }
    (out / "palette.json").write_text(json.dumps(record, indent=2, ensure_ascii=False))

    # board: key art with sample markers + swatches with hex
    thumb_w = 1000
    thumb = img.resize((thumb_w, int(H * thumb_w / W)), Image.Resampling.LANCZOS)
    sx = thumb_w / W
    sw_w, sw_h = 110, 70
    board = Image.new("RGB", (thumb_w + COLS * sw_w + 40, max(thumb.size[1], ROWS * (sw_h + 24)) + 20), (24, 24, 24))
    board.paste(thumb, (10, 10))
    d = ImageDraw.Draw(board)
    for c in cells:
        x, y = c["source_sample_px"]
        X, Y = 10 + x * sx, 10 + y * sx
        d.ellipse([X - 6, Y - 6, X + 6, Y + 6], outline=(255, 255, 255), width=2)
        d.text((X + 7, Y - 6), str(c["index"]), fill=(255, 255, 255))
        bx = thumb_w + 30 + c["col"] * sw_w
        by = 10 + c["row"] * (sw_h + 24)
        d.rectangle([bx, by, bx + sw_w - 8, by + sw_h], fill=c["hex"])
        d.text((bx, by + sw_h + 3), "%d %s" % (c["index"], c["hex"]), fill=(230, 230, 230))
    board.save(out / "board.png")
    print(json.dumps({"status": record["status"], "filled": len(cells), "uniform": uniform,
                      "palette_sha256": record["palette_sha256"], "rows": [[c["hex"] for c in cells if c["row"] == r] for r in range(ROWS)]}))


if __name__ == "__main__":
    main()
