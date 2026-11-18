#!/usr/bin/env python3
"""양산 그림 검사(크기·개수·팔레트·투명도). 표준 라이브러리만 쓴다(PIL 없음).

  python3 -B Tools/check_sprites.py --palette docs/art/아트-기준서.md [--size WxH | --max-size WxH]
                                    [--count N] [--binary-alpha] <PNG 또는 폴더>...
  python3 -B Tools/check_sprites.py --selftest

- --palette: 기준서 md의 "### 3.5" 절 안 첫 ```json 블록(문자열 "#rrggbb" 배열)을 팔레트로 읽는다.
- 폴더를 주면 바로 아래 *.png만 본다(하위 폴더는 보지 않는다).
- 판정: 8비트 RGBA PNG(색 형식 6, 인터레이스 없음), 크기(--size 정확히 / --max-size 이하), 파일 수(--count 정확히),
  불투명 픽셀(알파 255)의 RGB가 모두 팔레트 값과 정확히 같음, --binary-alpha면 알파가 0·255 말고는 없음.
- 출력: 실패한 파일마다 "FAIL <파일>: <이유>" 한 줄, 개수 실패는 "FAIL count: ..." 한 줄, 마지막에 요약 한 줄.
- 종료 코드: 0 통과, 1 실패, 2 실행할 수 없음(팔레트 블록을 못 읽음, 인자 틀림, 검사할 파일 없음).
- --selftest: 통과 1건과 반례 4종(팔레트 밖 색 1픽셀, 크기 다름, 반투명 픽셀, 개수 모자람)을 임시 폴더에 만들어
  이 도구를 실제로 실행하고, 기대 결과와 다르면 1.
"""
import argparse
import json
import re
import struct
import subprocess
import sys
import tempfile
import zlib
from pathlib import Path

PNG_SIG = b"\x89PNG\r\n\x1a\n"


class PaletteError(Exception):
    pass


def load_palette(md_path):
    try:
        text = Path(md_path).read_text(encoding="utf-8")
    except OSError as e:
        raise PaletteError(f"기준서를 열 수 없음: {e}")
    section = re.search(r"^### 3\.5[^\n]*\n(.*?)(?=^#{1,3} )", text + "\n### END\n", re.S | re.M)
    if section is None:
        raise PaletteError("3.5절을 찾지 못함")
    block = re.search(r"^```json[ \t]*\n(.*?)^```", section.group(1), re.S | re.M)
    if block is None:
        raise PaletteError("3.5절에 ```json 블록이 없음")
    try:
        values = json.loads(block.group(1))
    except ValueError as e:
        raise PaletteError(f"팔레트 JSON을 읽지 못함: {e}")
    if not isinstance(values, list) or not values:
        raise PaletteError("팔레트가 비었거나 배열이 아님")
    colorSet = set()
    for v in values:
        if not isinstance(v, str) or not re.fullmatch(r"#[0-9a-fA-F]{6}", v):
            raise PaletteError(f"색 형식 틀림: {v!r}")
        colorSet.add(tuple(int(v[i:i + 2], 16) for i in (1, 3, 5)))
    return colorSet


def read_png(path):
    """(w, h, rows) 또는 ValueError. rows는 RGBA 바이트열의 줄 목록."""
    data = Path(path).read_bytes()
    if not data.startswith(PNG_SIG):
        raise ValueError("PNG가 아님")
    pos = 8
    ihdr = None
    idat = bytearray()
    while pos + 8 <= len(data):
        length, ctype = struct.unpack(">I4s", data[pos:pos + 8])
        chunk = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if ctype == b"IHDR":
            ihdr = struct.unpack(">IIBBBBB", chunk)
        elif ctype == b"IDAT":
            idat += chunk
        elif ctype == b"IEND":
            break
    if ihdr is None:
        raise ValueError("IHDR 없음")
    w, h, depth, colorType, _, _, interlace = ihdr
    if colorType != 6 or depth != 8:
        raise ValueError(f"RGBA 8비트가 아님(색 형식 {colorType}, 비트 {depth})")
    if interlace != 0:
        raise ValueError("인터레이스 PNG는 읽지 않음")
    try:
        raw = zlib.decompress(bytes(idat))
    except zlib.error as e:
        raise ValueError(f"압축 해제 실패: {e}")
    stride = w * 4
    if len(raw) < h * (stride + 1):
        raise ValueError("픽셀 데이터가 모자람")
    rows = []
    prev = bytearray(stride)
    for y in range(h):
        base = y * (stride + 1)
        ftype = raw[base]
        line = bytearray(raw[base + 1:base + 1 + stride])
        if ftype == 1:
            for i in range(4, stride):
                line[i] = (line[i] + line[i - 4]) & 255
        elif ftype == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 255
        elif ftype == 3:
            for i in range(stride):
                left = line[i - 4] if i >= 4 else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 255
        elif ftype == 4:
            for i in range(stride):
                a = line[i - 4] if i >= 4 else 0
                b = prev[i]
                c = prev[i - 4] if i >= 4 else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                if pa <= pb and pa <= pc:
                    pred = a
                elif pb <= pc:
                    pred = b
                else:
                    pred = c
                line[i] = (line[i] + pred) & 255
        elif ftype != 0:
            raise ValueError(f"모르는 필터 {ftype}")
        rows.append(line)
        prev = line
    return w, h, rows


def write_png(path, w, h, pixels):
    """pixels: (r, g, b, a) 목록, 줄 순서. 시험용."""
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            raw += bytes(pixels[y * w + x])

    def chunk(ctype, body):
        return struct.pack(">I", len(body)) + ctype + body + struct.pack(">I", zlib.crc32(ctype + body) & 0xffffffff)

    Path(path).write_bytes(PNG_SIG + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                           + chunk(b"IDAT", zlib.compress(bytes(raw))) + chunk(b"IEND", b""))


def parse_size(text):
    m = re.fullmatch(r"(\d+)[xX](\d+)", text)
    if m is None:
        raise argparse.ArgumentTypeError(f"크기 형식은 WxH: {text}")
    return int(m.group(1)), int(m.group(2))


def check_file(path, colorSet, size, maxSize, isBinaryAlpha):
    try:
        w, h, rows = read_png(path)
    except (OSError, ValueError) as e:
        return [str(e)]
    reasonList = []
    if size is not None and (w, h) != size:
        reasonList.append(f"크기 {w}x{h} ≠ {size[0]}x{size[1]}")
    if maxSize is not None and (w > maxSize[0] or h > maxSize[1]):
        reasonList.append(f"크기 {w}x{h} > 최대 {maxSize[0]}x{maxSize[1]}")
    offCount = 0
    offFirst = None
    offColorSet = set()
    semiCount = 0
    semiFirst = None
    for y, line in enumerate(rows):
        for x in range(w):
            i = x * 4
            a = line[i + 3]
            if a == 255:
                rgb = (line[i], line[i + 1], line[i + 2])
                if rgb not in colorSet:
                    offCount += 1
                    offColorSet.add(rgb)
                    if offFirst is None:
                        offFirst = (x, y, rgb)
            elif a != 0:
                semiCount += 1
                if semiFirst is None:
                    semiFirst = (x, y, a)
    if offCount:
        x, y, rgb = offFirst
        reasonList.append(f"팔레트 밖 불투명 픽셀 {offCount}개({len(offColorSet)}색, 첫 ({x},{y}) #{rgb[0]:02x}{rgb[1]:02x}{rgb[2]:02x})")
    if isBinaryAlpha and semiCount:
        x, y, a = semiFirst
        reasonList.append(f"반투명 픽셀 {semiCount}개(첫 ({x},{y}) 알파 {a})")
    return reasonList


def collect(pathList):
    fileList = []
    for p in pathList:
        p = Path(p)
        if p.is_dir():
            fileList.extend(sorted(c for c in p.iterdir() if c.is_file() and c.suffix.lower() == ".png"))
        else:
            fileList.append(p)
    return fileList


def run(args):
    try:
        colorSet = load_palette(args.palette)
    except PaletteError as e:
        print(f"ERROR palette: {e}")
        return 2
    fileList = collect(args.paths)
    missingList = [f for f in fileList if not f.is_file()]
    if missingList:
        print(f"ERROR 파일 없음: {', '.join(str(f) for f in missingList)}")
        return 2
    failed = 0
    if args.count is not None and len(fileList) != args.count:
        print(f"FAIL count: 파일 {len(fileList)}개 ≠ {args.count}개")
        failed += 1
    if not fileList:
        print("ERROR 검사할 PNG가 없음")
        return 2 if args.count is None else 1
    for f in fileList:
        reasonList = check_file(f, colorSet, args.size, args.max_size, args.binary_alpha)
        if reasonList:
            print(f"FAIL {f}: {'; '.join(reasonList)}")
            failed += 1
    print(f"{'PASS' if failed == 0 else 'FAIL'}: 파일 {len(fileList)}개, 팔레트 {len(colorSet)}색, 실패 {failed}건")
    return 0 if failed == 0 else 1


def selftest():
    me = Path(__file__).resolve()
    ok = True
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        spec = tmp / "spec.md"
        spec.write_text("# 기준서\n\n### 3.4 다른 절\n\n```json\n[\"#000000\"]\n```\n\n### 3.5 색·광택\n\n- 설명\n\n"
                        "```json\n[\"#34140e\", \"#4f7a2e\"]\n```\n\n### 3.6 다음\n", encoding="utf-8")
        brown = (0x34, 0x14, 0x0e, 255)
        green = (0x4f, 0x7a, 0x2e, 255)
        clear = (0, 0, 0, 0)

        def make(name, w, h, edit=None):
            px = [brown if (x + y) % 2 else green for y in range(h) for x in range(w)]
            px[0] = clear
            if edit:
                edit(px)
            folder = tmp / name
            folder.mkdir(exist_ok=True)
            write_png(folder / f"{name}.png", w, h, px)
            return folder

        good = make("good", 8, 8)
        offColor = make("offcolor", 8, 8, lambda px: px.__setitem__(9, (0x4f, 0x7a, 0x2f, 255)))
        wrongSize = make("size", 8, 7)
        semi = make("semi", 8, 8, lambda px: px.__setitem__(5, (0x4f, 0x7a, 0x2e, 128)))
        write_png(good / "second.png", 8, 8, [green] * 64)
        badSpec = tmp / "bad.md"
        badSpec.write_text("### 3.5 색\n\n팔레트 없음\n", encoding="utf-8")

        common = ["--palette", str(spec), "--size", "8x8", "--binary-alpha"]
        cases = [
            ("통과", common + ["--count", "2", str(good)], 0, None),
            ("팔레트 밖 색 1픽셀", common + [str(offColor)], 1, "팔레트 밖 불투명 픽셀 1개"),
            ("크기 다름", common + [str(wrongSize)], 1, "크기 8x7"),
            ("반투명 픽셀", common + [str(semi)], 1, "반투명 픽셀 1개"),
            ("반투명 허용(--binary-alpha 없음)", ["--palette", str(spec), str(semi)], 0, None),
            ("개수 모자람", common + ["--count", "3", str(good)], 1, "FAIL count"),
            ("최대 크기 넘음", ["--palette", str(spec), "--max-size", "8x6", str(wrongSize)], 1, "> 최대"),
            ("팔레트 블록 못 읽음", ["--palette", str(badSpec), str(good)], 2, "ERROR palette"),
        ]
        for name, argv, wantCode, wantText in cases:
            res = subprocess.run([sys.executable, "-B", str(me)] + argv, capture_output=True, text=True)
            out = res.stdout + res.stderr
            passed = res.returncode == wantCode and (wantText is None or wantText in out)
            print(f"{'ok  ' if passed else 'BAD '} {name}: exit {res.returncode} (기대 {wantCode})"
                  + ("" if passed else f"\n     출력: {out.strip()}"))
            ok = ok and passed
    print("SELFTEST PASS" if ok else "SELFTEST FAIL")
    return 0 if ok else 1


def main():
    parser = argparse.ArgumentParser(description="양산 그림 검사(크기·개수·팔레트·투명도)")
    parser.add_argument("--selftest", action="store_true")
    parser.add_argument("--palette")
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--size", type=parse_size)
    group.add_argument("--max-size", type=parse_size)
    parser.add_argument("--count", type=int)
    parser.add_argument("--binary-alpha", action="store_true")
    parser.add_argument("paths", nargs="*")
    args = parser.parse_args()
    if args.selftest:
        return selftest()
    if not args.palette or not args.paths:
        parser.print_usage()
        print("ERROR --palette와 PNG 파일·폴더가 필요하다")
        return 2
    return run(args)


if __name__ == "__main__":
    sys.exit(main())
