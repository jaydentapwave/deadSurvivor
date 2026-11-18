#!/usr/bin/env python3
"""Comparison sheets for the user — the display gate: only measured PASS pictures get on a sheet (6-1).

  sheet.py pairs <out.png> --ref <existing game picture> <candidate>... [--size 256] [--unverified "<reason>"]
  sheet.py grid  <out.png> <picture>... [--frames | --tiles | --props | --candidates] [--size 256] [--unverified "<reason>"]
                 [--against <reference crop>...] [--numbered]
  sheet.py anim  <out.gif|out.png> <frame>... [--fps 12] [--size 256] [--label "<text>"] [--unverified "<reason>"]
  sheet.py selftest

pairs: the reference first, then each candidate resized to the reference canvas, all drawn at the same pixel size
(--size = the game picture height in pixels, default: the reference's own height), each with its judgement table.
A candidate gets on the sheet only if its measure.py compare record (work_dir/measure/<sha256>.compare.json) is PASS and
still fresh — the spec is approved now and unchanged, the identity and its reference are the ones measured against.
grid --frames (one animation frame set): also the frames.py jitter record of exactly these frames must be PASS and fresh. Others are left out with the reason;
exit 1 if any was left out (the sheet still holds the rest), exit 1 without a sheet if none is left.
--unverified "<reason>": include unmeasured or failing pictures anyway (explaining a failure, comparing attempts): the
sheet gets a red UNVERIFIED band with the reason on top and on every such cell. There is no way to make an unmarked
sheet of unverified pictures. The --ref picture itself is not judged.
Pictures that measure.py cannot judge have their own gate instead of the red band:
grid --tiles (path/terrain tiles): each tile needs a seam record PASS from `tileprev.py sheet` (tiles are judged where
  they join, not against an identity). The sheet says so and still is not the game screen.
grid --props (props, icons with no identity in this art style): there is nothing to measure against, so the sheet gets
  an amber NOT MEASURED band (not UNVERIFIED) and the user judges shape and colour. A prop that does have a compare
  record must be PASS like any other picture.
grid --candidates (reference candidates of a new character: no identity exists yet, the user is choosing the picture that
  will become one): a grey band "기준 후보 — 측정 기준 없음", not the red one — nothing is wrong with them, there is just
  nothing to measure against yet. A picture that has a compare record must be PASS, so this is no way round a FAIL.
grid --against <crop>... puts reference crops (or adopted style samples) first, drawn at the SAME height as the pictures
  and marked "not judged": line weight and hand-drawn feel are compared at one size, not at each picture's own
  resolution (source case: three rounds of "thinner / rougher" at full size went the opposite way from the reference;
  at one height the reference line was thicker). For the enlarged look make a second sheet with a larger --size.
grid --numbered labels every picture #1, #2... by its position on the command line (a left-out picture keeps its number).
  Put EVERY candidate so far on it, not just the newest: the user picks by number and can go back (source case: nine
  rounds shown one at a time, then the user returned to the third).
Any kind: a picture whose matte.py fringe record is FAIL (key colour left on its rim) is left out until it is fixed.
anim (animation preview, GIF or — with a .png/.apng name — APNG): the same gate as grid --frames (every frame compare PASS,
  the jitter record of exactly these frames PASS; --unverified "<reason>" puts the red band on every frame instead). Every
  frame carries the header "애니메이션 미리보기 — 게임 화면 아님", the frame number and the real frame rate. A GIF gets ONE
  palette for all frames, and its delay is whole 1/100 s: --fps 18 cannot be stored (55.6 ms), the nearest delay is used
  and the real rate is printed and drawn. After writing, the file is opened again and decoded: the frame count, the
  delays and every decoded frame must match what was drawn, else the file is removed and the exit is 1 — a preview that
  does not decode to the frames is never left to be sent.
  Exit: 0 written and decoded back / 1 gate or decode check failed / 2 input problem."""
import argparse
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kit2d as K  # noqa: E402

PAD, LINE = 12, 18
RED, GREEN, GREY, AMBER = (200, 30, 40), (30, 140, 60), (90, 90, 90), (190, 120, 0)


def gate(kit, path, set_kind=None):
    """(ok, reason, item lines) for one picture. Needs a compare record that is PASS and still fresh (approved spec,
    identity and reference unchanged since). set_kind (grid of frames): if the picture has any frame-set record, the
    record of exactly this set must be PASS and fresh too."""
    recs = K.read_records(kit, K.sha256(path))
    needed = ['compare'] + ([set_kind] if set_kind else [])      # --frames: this exact set must have been judged
    lines, why = [], []
    for kind in needed:
        r = recs.get(kind)
        if not r:
            why.append(f'no {"compare" if kind == "compare" else "jitter record for this frame set"}')
            continue
        stale = K.stale_reason(kit, r)
        v = r.get('verdict')
        if v != 'PASS' or stale:
            why.append(f'{kind.split("-")[0]} {v}' + (f' ({stale})' if stale else ''))
        cond = (f" changed={','.join(r.get('changed') or []) or '-'}" + (' pose' if r.get('pose') else '') + (f" box={r['changed_box']}" if r.get('changed_box') else '')
                + f" edit={r.get('edit')}") if kind == 'compare' else \
            f" moving={','.join(r.get('moving') or []) or '-'} frames={len(r.get('set') or [])}"
        lines.append((f"{kind.split('-')[0]}: {v}{cond}" + (f' STALE {stale}' if stale else ''), GREEN if v == 'PASS' and not stale else RED))
        for i in r.get('items', []):
            val = i['value']
            ref = f" (ref {i['ref']})" if i.get('ref') not in (None, 0, 0.0) else ''
            lines.append((f"  {i['item']} {val if val is not None else '-'}{ref} / {i['tol']}" + (f" {i['note']}" if i.get('note') else ''),
                          GREEN if i['verdict'] == 'PASS' else GREY if i['verdict'] == 'INFO' else AMBER if i['verdict'] == 'NOT_ASSESSED' else RED))
    return not why, ('PASS' if not why else 'not PASS: ' + '; '.join(why)), lines


def gate_tile(kit, path):
    """a tile is judged where it joins: the seam record tileprev.py sheet wrote for this file content must be PASS"""
    r = K.read_records(kit, K.sha256(path)).get('seam')
    if not r:
        return False, 'not PASS: no seam record (run tileprev.py sheet on the tile set, or tileprev.py strip on side-view terrain)', []
    v = r.get('verdict')
    line = (f"seam: {v} ({len(r.get('set') or [])} tiles, {r.get('cells')} {'seams' if r.get('mode') == 'strip' else 'path cells'}, {Path(r.get('sheet') or '').name})",
            GREEN if v == 'PASS' else RED)
    return v == 'PASS', ('PASS' if v == 'PASS' else f'not PASS: seam {v}'), [line]


def gate_prop(kit, path):
    """a prop with no identity has nothing to be measured against; one that was measured must be PASS"""
    if K.read_records(kit, K.sha256(path)).get('compare'):
        return gate(kit, path)
    return True, 'PASS', [('not measured: no identity for props in this style', AMBER), ('user judges shape and colour', GREY)]


def gate_candidate(kit, path):
    """a reference candidate has no identity to be measured against yet; one that was measured must be PASS"""
    if K.read_records(kit, K.sha256(path)).get('compare'):
        return gate(kit, path)
    return True, 'PASS', [('기준 후보 — 측정 기준 없음', GREY), ('no identity yet: the user picks the reference', GREY)]


def fringe_block(kit, path):
    """reason if matte.py fringe judged this file content FAIL (key colour left on the rim), else None"""
    r = K.read_records(kit, K.sha256(path)).get('fringe')
    if r and r.get('verdict') == 'FAIL':
        return f"fringe FAIL (spill {r.get('spill')}, edge {r.get('edge')}, stray {r.get('stray')} px -> matte.py fix)"
    return None


def checker(w, h, cell=8):
    im = Image.new('RGB', (w, h), (235, 235, 235))
    d = ImageDraw.Draw(im)
    for y in range(0, h, cell):
        for x in range((y // cell) % 2 * cell, w, cell * 2):
            d.rectangle([x, y, x + cell - 1, y + cell - 1], fill=(250, 250, 250))
    return im


def fit(img, size, canvas=None):
    """resize to the reference canvas first (same game export size), then scale so the height is `size` px"""
    if canvas and img.size != canvas:
        img = img.resize(canvas, Image.LANCZOS)
    w, h = img.size
    s = size / h
    return img.resize((max(1, round(w * s)), size), Image.LANCZOS if s < 1 else Image.NEAREST)


def render(out, cells, size, unverified, title, notice=None):
    f, fs = K.font(14), K.font(12)
    widths = [c['img'].width for c in cells]
    text_w = max(fs.getlength(t) for c in cells for t, _ in c['lines'] + [(c['label'], None)])
    text_h = max(len(c['lines']) for c in cells) * LINE + 2 * LINE
    cols = min(len(cells), 6)
    rows = (len(cells) + cols - 1) // cols
    cw = int(max(max(widths), 220, text_w + 8)) + PAD            # the column is as wide as its longest table line
    band = 40 if unverified or notice else 0
    W, H = cols * cw + PAD, band + LINE * 2 + rows * (size + text_h + PAD) + PAD
    sheet = Image.new('RGB', (W, H), (255, 255, 255))
    d = ImageDraw.Draw(sheet)
    if unverified:
        d.rectangle([0, 0, W, band], fill=RED)
        d.text((PAD, 10), f'UNVERIFIED — {unverified}', fill=(255, 255, 255), font=f)
    elif notice:
        d.rectangle([0, 0, W, band], fill=notice[1])
        d.text((PAD, 10), notice[0], fill=(255, 255, 255), font=f)
    d.text((PAD, band + 4), title, fill=GREY, font=fs)
    for n, c in enumerate(cells):
        x = PAD + (n % cols) * cw
        y = band + LINE * 2 + (n // cols) * (size + text_h + PAD)
        bg = checker(c['img'].width, size)
        bg.paste(c['img'], (0, 0), c['img'])
        sheet.paste(bg, (x, y))
        if c['unverified']:
            d.rectangle([x, y, x + c['img'].width - 1, y + 16], fill=RED)
            d.text((x + 3, y + 1), 'UNVERIFIED', fill=(255, 255, 255), font=fs)
        d.text((x, y + size + 4), c['label'], fill=(20, 20, 20), font=f)
        for i, (t, col) in enumerate(c['lines']):
            d.text((x, y + size + 4 + LINE * (i + 1)), t, fill=col, font=fs)
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)


def build(kit, out, pics, ref=None, size=None, unverified=None, quiet=False, frame_set=False, kind=None, against=None, numbered=False):
    canvas = Image.open(ref).size if ref else None
    set_kind = None
    if frame_set:
        import frames
        set_kind = frames.set_key([K.sha256(p) for p in pics])
    size = size or (canvas[1] if canvas else 256)
    cells, left = [], []
    if ref:
        cells.append({'img': fit(Image.open(ref).convert('RGBA'), size), 'label': f'REF {Path(ref).name}',
                      'lines': [('reference (not judged)', GREY)], 'unverified': False})
    for r in against or []:
        cells.append({'img': fit(Image.open(r).convert('RGBA'), size), 'label': f'REF {Path(r).name}',
                      'lines': [('reference, same height (not judged)', GREY)], 'unverified': False})
    for n, p in enumerate(pics, 1):
        ok, why, lines = (gate_tile(kit, p) if kind == 'tiles' else gate_prop(kit, p) if kind == 'props'
                          else gate_candidate(kit, p) if kind == 'candidates' else gate(kit, p, set_kind))
        fringe = fringe_block(kit, p)
        if fringe:
            ok, why, lines = False, ('not PASS: ' + fringe if ok else f'{why}; {fringe}'), lines + [(fringe, RED)]
        if not ok and not unverified:
            left.append((p, why))
            continue
        cells.append({'img': fit(Image.open(p).convert('RGBA'), size, canvas), 'label': (f'#{n} ' if numbered else '') + Path(p).name, 'lines': lines or [(why, RED)],
                      'unverified': not ok})
    for p, why in left:
        print(f'LEFT_OUT {Path(p).name}: {why}')
    judged = [c for c in cells if not c['label'].startswith('REF ')]
    if not judged:
        print('NO_SHEET nothing passed the measure gate -> fix and re-measure, or --unverified "<reason>"')
        return 1
    notice = None
    if kind == 'props' and any(c['lines'] and c['lines'][0][1] == AMBER for c in judged):
        notice = ('NOT MEASURED — 소품·아이콘(identity 없음): 수치 판정 없음, 형태·색은 사용자 판정', AMBER)
    if kind == 'candidates' and any(c['lines'] and c['lines'][0][0].startswith('기준 후보') for c in judged):
        notice = ('기준 후보 — 측정 기준 없음: 새 캐릭터의 기준 그림을 고르는 시트. 고른 그림이 identity가 된다', GREY)
    title = f"{len(judged)} picture(s) at {size}px" + (f", reference {Path(ref).name}" if ref else '')
    if kind == 'tiles':
        title += ' — tiles: seam check only (tileprev.py). 이음 시트와 게임 화면에서 판정'
    render(out, cells, size, unverified if any(c['unverified'] for c in cells) else None, title, notice)
    if not quiet:
        print(f'SHEET {out} ({len(judged)} shown, {len(left)} left out)')
    return 1 if left else 0


# ---------- animation preview ----------

ANIM_MARK = '애니메이션 미리보기 — 게임 화면 아님'
DECODE_MEAN, DECODE_FAR, DECODE_FAR_SHARE = 6.0, 48, 0.01


def anim_frames(pics, size, fps, label='', unverified=None, flags=None):
    """RGB frames as they will be shown: header band, frame number and rate, the picture on a checker at `size` px high"""
    f, fs = K.font(14), K.font(12)
    canvas = Image.open(pics[0]).size
    imgs = [fit(Image.open(p).convert('RGBA'), size, canvas) for p in pics]
    head = [ANIM_MARK, ''] + ([f'UNVERIFIED — {unverified}'] if unverified else [])
    w = int(max(imgs[0].width, max(f.getlength(t) for t in head), fs.getlength(f'000/000 · 00.00 fps · {label}')) + 2 * PAD)
    top = LINE * (len(head) + 1)
    out = []
    for n, im in enumerate(imgs):
        fr = Image.new('RGB', (w, top + size + PAD), (255, 255, 255))
        d = ImageDraw.Draw(fr)
        d.rectangle([0, 0, w, LINE + 6], fill=(40, 40, 40))
        d.text((PAD, 4), ANIM_MARK, fill=(255, 255, 255), font=f)
        d.text((PAD, LINE + 8), f'{n + 1}/{len(imgs)} · {fps:.2f} fps' + (f' · {label}' if label else ''), fill=GREY, font=fs)
        if unverified:
            d.rectangle([0, LINE * 2 + 6, w, LINE * 3 + 6], fill=RED)
            d.text((PAD, LINE * 2 + 8), head[2], fill=(255, 255, 255), font=fs)
        bg = checker(im.width, size)
        bg.paste(im, (0, 0), im)
        x = (w - im.width) // 2
        fr.paste(bg, (x, top))
        if flags and flags[n]:
            d.rectangle([x, top, x + im.width - 1, top + 16], fill=RED)
            d.text((x + 3, top + 1), 'UNVERIFIED', fill=(255, 255, 255), font=fs)
        out.append(fr)
    return out


def write_anim(out, frames, ms):
    """GIF: every frame quantized to ONE palette built from all frames (a palette per frame is what breaks colours).
    .png/.apng: APNG, lossless."""
    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    if out.suffix.lower() in ('.png', '.apng'):
        frames[0].save(out, format='PNG', save_all=True, append_images=frames[1:], duration=ms, loop=0)
        return
    strip = Image.new('RGB', (frames[0].width, frames[0].height * len(frames)))
    for i, fr in enumerate(frames):
        strip.paste(fr, (0, fr.height * i))
    pal = strip.quantize(colors=255, method=Image.MEDIANCUT, dither=Image.Dither.NONE)
    q = [fr.quantize(palette=pal, dither=Image.Dither.NONE) for fr in frames]
    q[0].save(out, format='GIF', save_all=True, append_images=q[1:], duration=ms, loop=0, optimize=False, disposal=1)


def decode_check(path, frames, ms):
    """open the written file again and compare what a viewer will decode with what was drawn. Returns (ok, text)"""
    from PIL import ImageSequence
    try:
        im = Image.open(path)
        dec = [(fr.convert('RGB'), fr.info.get('duration')) for fr in ImageSequence.Iterator(im)]
    except (OSError, ValueError, EOFError) as e:
        return False, f'cannot be decoded: {e}'
    if len(dec) != len(frames):
        return False, f'decodes to {len(dec)} frames, {len(frames)} were drawn'
    worst_mean, worst_far, wrong_ms = 0.0, 0.0, 0
    for (d, dur), src in zip(dec, frames):
        if d.size != src.size:
            return False, f'decoded frame is {d.size}, drawn {src.size}'
        diff = np.abs(np.asarray(d, dtype=np.int16) - np.asarray(src, dtype=np.int16))
        worst_mean = max(worst_mean, float(diff.mean()))
        worst_far = max(worst_far, float((diff.max(axis=-1) > DECODE_FAR).mean()))
        wrong_ms += dur is not None and abs(float(dur) - ms) > 1
    ok = worst_mean <= DECODE_MEAN and worst_far <= DECODE_FAR_SHARE and not wrong_ms
    return ok, (f'{len(dec)} frames decoded, worst mean colour difference {worst_mean:.2f} (max {DECODE_MEAN:g}), '
                f'pixels far off {worst_far:.2%} (max {DECODE_FAR_SHARE:.0%})' + (f', {wrong_ms} frames with another delay' if wrong_ms else ''))


def anim(kit, out, pics, fps=12.0, size=None, label='', unverified=None, quiet=False):
    import frames as F
    if len(pics) < 2:
        raise K.KitError('TOO_FEW_FRAMES', 'anim needs 2 or more frames')
    if not 1 <= fps <= 50:
        raise K.KitError('BAD_FPS', '--fps between 1 and 50')
    gif = Path(out).suffix.lower() not in ('.png', '.apng')
    if gif and Path(out).suffix.lower() != '.gif':
        raise K.KitError('BAD_OUT', f'{out}: name the preview .gif, or .png/.apng for APNG')
    set_kind = F.set_key([K.sha256(p) for p in pics])
    flags, left = [], []
    for p in pics:
        ok, why, _ = gate(kit, p, set_kind)
        fringe = fringe_block(kit, p)
        if fringe:
            ok, why = False, fringe
        flags.append(not ok)
        if not ok:
            left.append((p, why))
    if left and not unverified:
        for p, why in left:
            print(f'LEFT_OUT {Path(p).name}: {why}')
        print('NO_ANIM an animation with frames missing is another animation -> fix and re-measure (measure.py compare, '
              'frames.py jitter on exactly these frames), or --unverified "<reason>"')
        return 1
    ms = max(20, round(100 / fps) * 10) if gif else max(1, round(1000 / fps))     # GIF delay is whole 1/100 s; under 20 ms viewers slow it down
    real = 1000 / ms
    size = size or min(Image.open(pics[0]).height, 512)
    frames = anim_frames(pics, size, real, label, unverified if left else None, flags if left else None)
    write_anim(out, frames, ms)
    ok, text = decode_check(out, frames, ms)
    if not ok:
        Path(out).unlink(missing_ok=True)
        print(f'DECODE_FAIL {out}: {text} -> removed, nothing to send')
        return 1
    if not quiet:
        if abs(real - fps) > 0.01:
            print(f'FPS asked {fps:g}, stored {real:.2f} (delay {ms} ms' + ('; GIF delays are whole 1/100 s — name the file .png for APNG to keep the rate)' if gif else ')'))
        print(f"ANIM {out} ({len(pics)} frames, {real:.2f} fps, {'one palette' if gif else 'APNG'}; decoded back: {text})"
              + (f' UNVERIFIED: {len(left)} frame(s)' if left else ''))
    return 0


def selftest():
    import json
    import numpy as np
    import measure as M
    import frames as F
    td = Path(tempfile.mkdtemp(prefix='sheet-selftest-'))
    root = td / 'proj'
    (root / '.ai/2d-art').mkdir(parents=True)
    (root / 'ids').mkdir()
    (root / '.ai/2d-art/config.json').write_text(json.dumps({'schema': '2d-art-config/1', 'work_dir': 'out', 'spec': 'spec.md', 'identities_dir': 'ids'}))
    spec = root / 'spec.md'

    def approve(text):
        spec.write_text(text, encoding='utf-8')
        spec.with_suffix('.approval.json').write_text(json.dumps({'schema': 'art-spec-approval/1', 'revision': '', 'sha256': K.sha256(spec)}))
    approve('- 상태: 승인\n- 승인: 2026-01-01 — "ok"\n```json 2d-measure\n{"tolerance": {}}\n```\n')
    ref, good, bad, unmeasured = (td / f'{n}.png' for n in ('ref', 'good', 'bad', 'unmeasured'))
    M._figure().save(ref)
    M._figure(shift=(0, -10)).save(good)
    M._figure(leg_w=0.7).save(bad)
    M._figure(blob=True).save(unmeasured)
    (root / 'ids/hero.json').write_text(json.dumps({'schema': K.IDENTITY_SCHEMA, 'id': 'hero', 'ref': str(ref), 'ref_sha256': K.sha256(ref),
                                                     'approved': '2026-01-01 — "ok"', 'width_part': 'hat', 'outline_exclude': ['hat'],
                                                     'parts': {'hat': {'hex': '#0047BB'}, 'leg_l': {'hex': '#F0963C', 'box_rel': [0, 0.5, 0.5, 1]},
                                                               'leg_r': {'hex': '#F0963C', 'box_rel': [0.5, 0.5, 1, 1]}}}))
    kit = K.load_kit(root)
    M.compare(kit, 'hero', [good, bad], edit=False, quiet=True)
    cases = []

    def case(label, ok):
        cases.append(bool(ok))
        print(f"{'PASS' if ok else 'FAIL'} {label}")
    s1 = td / 's1.png'
    case('only the PASS picture: exit 0, sheet made', build(kit, s1, [good], ref=ref, quiet=True) == 0 and s1.is_file())
    s2 = td / 's2.png'
    case('FAIL and unmeasured left out: exit 1, sheet still holds the PASS one',
         build(kit, s2, [good, bad, unmeasured], ref=ref, quiet=True) == 1 and s2.is_file())
    s3 = td / 's3.png'
    case('nothing passes: exit 1, no sheet', build(kit, s3, [bad, unmeasured], ref=ref, quiet=True) == 1 and not s3.is_file())
    s4 = td / 's4.png'
    rc = build(kit, s4, [bad, unmeasured], ref=ref, unverified='explaining why the legs fail', quiet=True)
    top = np.asarray(Image.open(s4))[5, 5] if s4.is_file() else None
    case('--unverified: sheet made with a red band', rc == 0 and top is not None and tuple(top[:3]) == RED)
    # frames: each frame PASS on compare, but the shown set failed jitter -> out; a consistently wrong set without compare -> out
    thin2 = td / 'thin2.png'
    M._figure(leg_w=0.7, shift=(0, -4)).save(thin2)
    F.jitter(kit, 'hero', [good, ref], quiet=True)                       # this set is fine
    F.jitter(kit, 'hero', [good, bad], quiet=True)                       # this set fails (legs jump)
    case('grid of a failing frame set keeps the frames out', build(kit, td / 's5.png', [good, bad], frame_set=True, quiet=True) == 1)
    F.jitter(kit, 'hero', [bad, thin2], quiet=True)                      # consistent but wrong; thin2 never compared
    case('frame set PASS but no compare: out', build(kit, td / 's6.png', [bad, thin2], frame_set=True, quiet=True) == 1
         and not (td / 's6.png').is_file())
    case('single PASS picture (no frame set) is shown', build(kit, td / 's7.png', [good], quiet=True) == 0)
    good2 = td / 'good2.png'
    M._figure(shift=(0, -16)).save(good2)
    M.compare(kit, 'hero', [good2], edit=False, quiet=True)
    case('--frames with compare PASS but jitter never run: out', build(kit, td / 's7b.png', [good, good2], frame_set=True, quiet=True) == 1)
    F.jitter(kit, 'hero', [good, good2], quiet=True)
    case('--frames after jitter PASS on exactly these frames: shown', build(kit, td / 's7c.png', [good, good2], frame_set=True, quiet=True) == 0)
    from PIL import Image as _I
    w = _I.open(td / 's7c.png').width
    case('sheet wide enough for its table lines', w >= 2 * 220)
    # stale: tighten the spec and re-approve -> the old PASS no longer counts
    approve('- 상태: 승인\n- 승인: 2026-01-02 — "tighter"\n```json 2d-measure\n{"tolerance": {"de76": 1}}\n```\n')
    case('spec re-approved after measuring: old PASS is stale, out', build(kit, td / 's8.png', [good], ref=ref, quiet=True) == 1)
    spec.write_text(spec.read_text(encoding='utf-8').replace('상태: 승인', '상태: 초안'), encoding='utf-8')
    case('spec no longer approved: out', build(kit, td / 's9.png', [good], ref=ref, quiet=True) == 1)
    # tiles and props: their own gate instead of the red band
    approve('- 상태: 승인\n- 승인: 2026-01-03 — "ok"\n```json 2d-measure\n{"tolerance": {}}\n```\n')
    tile, lone, prop = td / 'tile_path_h.png', td / 'tile_path_v.png', td / 'crate.png'
    Image.new('RGBA', (20, 20), (150, 110, 70, 255)).save(tile)
    Image.new('RGBA', (20, 20), (150, 110, 71, 255)).save(lone)
    Image.new('RGBA', (20, 20), (90, 60, 30, 255)).save(prop)
    K.write_record(kit, {'schema': K.MEASURE_SCHEMA, 'kind': 'seam', 'sha256': K.sha256(tile), 'verdict': 'PASS',
                         'set': [K.sha256(tile)], 'cells': 30, 'sheet': 'tiles-sheet.png'})
    s10 = td / 's10.png'
    rc = build(kit, s10, [tile], kind='tiles', quiet=True)
    top = tuple(np.asarray(Image.open(s10))[5, 5][:3]) if s10.is_file() else None
    case('--tiles with a seam PASS record: shown without a red band', rc == 0 and top not in (None, RED))
    case('--tiles without a seam record: out', build(kit, td / 's11.png', [lone], kind='tiles', quiet=True) == 1
         and not (td / 's11.png').is_file())
    K.write_record(kit, {'schema': K.MEASURE_SCHEMA, 'kind': 'seam', 'sha256': K.sha256(lone), 'verdict': 'FAIL', 'set': [], 'cells': 30})
    case('--tiles with a seam FAIL record: out', build(kit, td / 's12.png', [lone], kind='tiles', quiet=True) == 1)
    s13 = td / 's13.png'
    rc = build(kit, s13, [prop], kind='props', quiet=True)
    top = tuple(np.asarray(Image.open(s13))[5, 5][:3]) if s13.is_file() else None
    case('--props with no identity: shown with an amber NOT MEASURED band, not red', rc == 0 and top == AMBER)
    case('--props does not rescue a measured FAIL picture', build(kit, td / 's14.png', [bad], kind='props', quiet=True) == 1)
    # reference candidates: grey band, and no way round a measured FAIL
    cand = td / 'cand.png'
    M._figure(hat='#AA3355').save(cand)
    s15 = td / 's15.png'
    rc = build(kit, s15, [cand, prop], kind='candidates', quiet=True)
    top = tuple(np.asarray(Image.open(s15))[5, 5][:3]) if s15.is_file() else None
    case('--candidates with no identity yet: shown with a grey band, neither red nor amber', rc == 0 and top == GREY)
    case('--candidates does not rescue a measured FAIL picture', build(kit, td / 's16.png', [bad], kind='candidates', quiet=True) == 1
         and not (td / 's16.png').is_file())
    # --against: reference crops first at the same height, never judged; --numbered keeps command-line numbers
    crop = td / 'refcrop.png'
    M._figure().resize((64, 64)).save(crop)
    s19, s20 = td / 's19.png', td / 's20.png'
    build(kit, s19, [cand, prop], kind='candidates', quiet=True)
    rc = build(kit, s20, [cand, prop], kind='candidates', quiet=True, against=[crop], numbered=True)
    case('--against adds a reference cell at the same height (sheet grows by one column), exit unchanged',
         rc == 0 and Image.open(s20).width > Image.open(s19).width and Image.open(s20).height == Image.open(s19).height)
    case('--against alone is not a sheet: a measured FAIL still leaves nothing to show',
         build(kit, td / 's21.png', [bad], kind='candidates', quiet=True, against=[crop], numbered=True) == 1 and not (td / 's21.png').is_file())
    # a fringe FAIL record keeps a picture out whatever the kind
    K.write_record(kit, {'schema': K.MEASURE_SCHEMA, 'kind': 'fringe', 'sha256': K.sha256(prop), 'verdict': 'FAIL', 'spill': 40, 'edge': 0, 'stray': 0})
    case('a picture with a fringe FAIL record is left out (props, candidates)', build(kit, td / 's17.png', [prop], kind='props', quiet=True) == 1
         and build(kit, td / 's18.png', [prop], kind='candidates', quiet=True) == 1)
    # animation preview: same gate as --frames, one palette, decoded back
    from PIL import ImageSequence
    M.compare(kit, 'hero', [good, good2], edit=False, quiet=True)           # the spec was re-approved above: measure again
    F.jitter(kit, 'hero', [good, good2], quiet=True)
    g1 = td / 'a1.gif'
    rc = anim(kit, g1, [good, good2], fps=18, quiet=True)
    dec = list(ImageSequence.Iterator(Image.open(g1))) if g1.is_file() else []
    case('anim of a verified frame set: GIF written, decodes to the same 2 frames, --fps 18 stored as 60 ms (16.67 fps)',
         rc == 0 and len(dec) == 2 and Image.open(g1).info.get('duration') == 60)
    case('anim of frames whose set was never judged: no file', anim(kit, td / 'a2.gif', [good, bad], quiet=True) == 1 and not (td / 'a2.gif').is_file())
    a3 = td / 'a3.gif'
    rc = anim(kit, a3, [good, bad], unverified='showing why the legs fail', quiet=True)
    px = np.asarray(Image.open(a3).convert('RGB')) if a3.is_file() else None
    case('anim --unverified: written with a red band on the frames', rc == 0 and px is not None and (px.reshape(-1, 3) == RED).all(axis=1).sum() > 200)
    a4 = td / 'a4.png'
    case('anim to .png: APNG, 2 frames, exact delay', anim(kit, a4, [good, good2], fps=18, quiet=True) == 0
         and getattr(Image.open(a4), 'n_frames', 1) == 2 and abs(Image.open(a4).info.get('duration', 0) - 56) <= 1)
    # the decode check itself: a file that does not decode to the drawn frames
    drawn = anim_frames([good, good2], 256, 12.5)
    write_anim(td / 'ok.gif', drawn, 80)
    case('decode check passes the file it wrote', decode_check(td / 'ok.gif', drawn, 80)[0])
    drawn[0].save(td / 'short.gif')
    case('decode check: a file with a frame missing FAILs', not decode_check(td / 'short.gif', drawn, 80)[0])
    wrong = [fr.convert('L').convert('RGB').point(lambda v: 255 - v) for fr in drawn]
    write_anim(td / 'wrong.gif', wrong, 80)
    case('decode check: frames whose colours came out wrong FAIL', not decode_check(td / 'wrong.gif', drawn, 80)[0])
    write_anim(td / 'slow.gif', drawn, 200)
    case('decode check: another delay than asked FAILs', not decode_check(td / 'slow.gif', drawn, 80)[0])
    bad_n = cases.count(False)
    print(f"sheet selftest {'PASS' if not bad_n else 'FAIL'}: {len(cases)} cases, {bad_n} failed")
    return 1 if bad_n else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    p = sub.add_parser('pairs')
    p.add_argument('out', type=Path)
    p.add_argument('--ref', required=True)
    p.add_argument('pics', nargs='+')
    g = sub.add_parser('grid')
    g.add_argument('out', type=Path)
    g.add_argument('pics', nargs='+')
    for s in (p, g):
        s.add_argument('--size', type=int)
        s.add_argument('--unverified')
    sub.add_parser('selftest')
    g.add_argument('--frames', action='store_true', help='the pictures are one animation frame set: its jitter record must be PASS')
    g.add_argument('--tiles', action='store_true', help='path/terrain tiles: each needs a seam record PASS from tileprev.py sheet')
    g.add_argument('--props', action='store_true', help='props/icons with no identity: shown with a NOT MEASURED band')
    g.add_argument('--candidates', action='store_true', help='reference candidates of a new character (no identity yet): grey band')
    g.add_argument('--against', nargs='+', help='reference crops / style samples drawn first at the same height, not judged')
    g.add_argument('--numbered', action='store_true', help='label the pictures #1, #2... in command-line order')
    an = sub.add_parser('anim')
    an.add_argument('out', type=Path)
    an.add_argument('pics', nargs='+')
    an.add_argument('--fps', type=float, default=12.0)
    an.add_argument('--size', type=int)
    an.add_argument('--label', default='')
    an.add_argument('--unverified')
    a = ap.parse_args()
    if a.cmd == 'selftest':
        return selftest()
    if a.unverified is not None and not a.unverified.strip():
        print('UNVERIFIED_NEEDS_REASON --unverified "<why these unmeasured or failing pictures are shown>"')
        return 2
    if sum(bool(getattr(a, k, False)) for k in ('frames', 'tiles', 'props', 'candidates')) > 1:
        print('ONE_KIND --frames, --tiles, --props and --candidates are different gates: pick one')
        return 2
    try:
        kit = K.load_kit()
        if a.cmd == 'anim':
            return anim(kit, a.out, a.pics, a.fps, a.size, a.label, a.unverified)
        kind = next((k for k in ('tiles', 'props', 'candidates') if getattr(a, k, False)), None)
        against = getattr(a, 'against', None) or []
        missing = [r for r in against if not Path(r).is_file()]
        if missing:
            raise K.KitError('NO_IMAGE', ', '.join(missing))
        return build(kit, a.out, a.pics, getattr(a, 'ref', None), a.size, a.unverified, frame_set=getattr(a, 'frames', False), kind=kind,
                     against=against, numbered=getattr(a, 'numbered', False))
    except K.KitError as e:
        print(e)
        return 2


if __name__ == '__main__':
    sys.exit(main())
