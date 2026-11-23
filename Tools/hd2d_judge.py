#!/usr/bin/env python3
"""HD-2D 아트 관문(gate.py G20)용 두 모델 판독을 눈가림으로 돌린다.

사용자 2026-11-18: "아트를 보여주기전에 너가 검증 테스트를 어느정도 설계를해놓고 그 검증 테스트를 모두 통과하지
못하면 나한테 보여줄 필요는 없을 것 같아". 관문 도구는 모델을 부르지 않으므로 디렉터가 이 도구로 판독 파일을 만든다.

  hd2d_judge.py plate <manifest.json> <plate_id> --run-id <id> --out <review.json>
      판의 main·motion·aspects·closeup 그림을 중립 이름으로 복사해 Codex와 Claude에 따로 보이고,
      gate-manifest.md의 review 형식(run_id, images 해시 전체, readers 둘)으로 쓴다.
  hd2d_judge.py anchor --img <이름>=<그림> [...] --out-dir <폴더>
      그림마다 따로 판독해(연속 프레임 없음, motion 제외) 닻 확인 표를 쓴다.
  hd2d_judge.py selftest
      모델 호출 없이 답 해석·점수 검사·review 구성을 시험한다.

판독자: Codex gpt-6.2-astra medium(읽기 전용), Claude Opus 5.5 medium(Tools/claude_medium.sh, Read만 허용).
두 판독자는 서로의 답과 그림의 출처·의도(어느 판인지, 무엇을 기대하는지)를 모른다.
"""
import argparse
import concurrent.futures
import hashlib
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / 'output' / 'hd2d-관문' / 'judge-work'
AXES = ['style', 'depth', 'readability', 'ground', 'motion']
LIMIT_SECONDS = 900
CODEX = ['codex', 'exec', '-m', 'gpt-6.2-astra', '-c', 'model_reasoning_effort="medium"', '-s', 'read-only',
         '--skip-git-repo-check']
CLAUDE = [str(ROOT / 'Tools' / 'claude_medium.sh'), '-p', '--allowedTools', 'Read', '--output-format', 'text']

PROMPT = """너는 도트(픽셀아트) 캐릭터와 3D 배경을 섞은 HD-2D 게임 화면의 판독자다. 그림의 출처·의도·다른 판독자의 답은 모른다. 그림에서 본 것만 근거로 한다.

기준 그림체:
- 캐릭터: 확대된 도트 그림. 갈색 계열 얇은 외곽선, 명암은 2~3단 띠.
- 배경(바닥·땅 턱·바위·돌담·나무·덤불·울타리·물): 도트 질감을 입힌 3D 디오라마와 세워 놓은 도트 그림. 배경 도트 한 칸이 캐릭터 도트 한 칸과 비슷한 크기, 명암은 2~4단 띠, 소품에 얇은 갈색 계열 외곽선. 매끈한 로우폴리 면·부드러운 그라데이션·사진 질감이 아니다.
- 밝은 낮 들판. 바닥 풀은 누런 올리브, 나무는 풀보다 확실히 어둡고, 물은 땅보다 어둡다.

그림 목록:
{listing}

축마다 먼저 본 것을 한 줄 적고 점수 0~4(4 = 확실히 그렇다, 0 = 확실히 아니다)를 준다.
- style: 배경과 캐릭터가 같은 그림체다(도트 크기·명암 띠·외곽선이 같은 결).
- depth: 디오라마 같은 입체감이 있다(높이 단·턱, 바닥에 떨어지는 그림자, 빛 방향, 화면 가장자리 흐림·어둡게).
- readability: 인물·몬스터가 배경에서 한눈에 갈리고 효과·소품에 가려지지 않는다.
- ground: 바닥에 네모 칸이나 같은 무늬의 반복이 보이지 않고, 풀·흙길·물의 재질이 구별되며 경계가 자연스럽다.
- motion: {motion_rule}
그리고 major_defects(그림체가 섞여 어색하거나 화면이 깨진 큰 결함의 개수)와 defects(결함마다 그림 이름과 위치를 한 줄).

그림 파일을 모두 직접 열어 본 뒤, 답은 JSON 하나만 쓴다(설명 문장, 코드 울타리 없이):
{{"seen":{{"style":"..","depth":"..","readability":"..","ground":"..","motion":".."}},"scores":{{"style":0,"depth":0,"readability":0,"ground":0,"motion":0}},"major_defects":0,"defects":[]}}
"""
MOTION_SEQ = '연속 프레임 seq-0~seq-7을 순서대로 비교해, 멈춰 있는 배경과 인물의 도트가 흔들리거나 번쩍이거나 튀지 않는다.'
MOTION_NONE = '연속 프레임이 없으므로 null을 준다.'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def extract_json(text):
    text = re.sub(r'```(?:json)?', '', text)
    start = text.find('{')
    while start != -1:
        depth = 0
        for i in range(start, len(text)):
            if text[i] == '{':
                depth += 1
            elif text[i] == '}':
                depth -= 1
                if depth == 0:
                    try:
                        return json.loads(text[start:i + 1])
                    except json.JSONDecodeError:
                        break
        start = text.find('{', start + 1)
    raise ValueError('답에서 JSON을 찾지 못함')


def check_answer(answer, axes):
    scores = answer.get('scores')
    if not isinstance(scores, dict):
        raise ValueError('scores 없음')
    for axis in axes:
        value = scores.get(axis)
        if not isinstance(value, int) or isinstance(value, bool) or not 0 <= value <= 4:
            raise ValueError('점수 형식 오류: %s=%r' % (axis, value))
    defects = answer.get('major_defects')
    if not isinstance(defects, int) or isinstance(defects, bool) or defects < 0:
        raise ValueError('major_defects 형식 오류: %r' % (defects,))
    return answer


def build_prompt(names, has_motion):
    listing = '\n'.join('- %s' % n for n in names)
    return PROMPT.format(listing=listing, motion_rule=MOTION_SEQ if has_motion else MOTION_NONE)


def run_codex(folder, names, prompt):
    out = folder / 'codex-answer.txt'
    cmd = CODEX + ['-C', str(folder), '-o', str(out), prompt, '-i'] + [str(folder / n) for n in names]
    with open(folder / 'codex.log', 'w') as log:
        subprocess.run(cmd, stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT, timeout=LIMIT_SECONDS,
                       check=False)
    return out.read_text(encoding='utf-8') if out.exists() else ''


def run_claude(folder, names, prompt):
    paths = '\n'.join('- %s: %s' % (n, folder / n) for n in names)
    full = prompt + '\n그림 파일(Read 도구로 하나씩 연다):\n' + paths + '\n'
    done = subprocess.run(CLAUDE + [full], stdin=subprocess.DEVNULL, capture_output=True, text=True,
                          timeout=LIMIT_SECONDS, cwd=str(folder), check=False)
    (folder / 'claude.log').write_text(done.stdout + '\n--- stderr ---\n' + done.stderr, encoding='utf-8')
    return done.stdout


def judge(folder, names, has_motion):
    axes = AXES if has_motion else AXES[:4]
    prompt = build_prompt(names, has_motion)
    (folder / 'prompt.md').write_text(prompt, encoding='utf-8')
    readers = {'codex-gpt-6.2-astra': run_codex, 'claude-opus-5.5': run_claude}
    result = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        futures = {name: pool.submit(fn, folder, names, prompt) for name, fn in readers.items()}
        for name, future in futures.items():
            try:
                answer = check_answer(extract_json(future.result()), axes)
                answer['name'] = name
                result[name] = answer
            except (ValueError, subprocess.TimeoutExpired) as e:
                result[name] = {'name': name, 'error': str(e)}
    return result


def plate_images(manifest, plate_id):
    plate = next((p for p in manifest['plates'] if p['id'] == plate_id), None)
    if plate is None:
        raise ValueError('판 없음: ' + plate_id)
    pairs = [(plate['main'], 'main.png')]
    pairs += [(k, 'seq-%d.png' % i) for i, k in enumerate(plate.get('motion', []))]
    pairs += [(k, 'aspect-%d.png' % i) for i, k in enumerate(plate.get('aspects', []))]
    if plate.get('closeup'):
        pairs.append((plate['closeup'], 'closeup.png'))
    return pairs


def make_review(run_id, image_hashes, answers):
    readers = []
    for name, answer in answers.items():
        if 'error' in answer:
            raise ValueError('%s 판독 실패: %s' % (name, answer['error']))
        readers.append({k: v for k, v in answer.items()})
    return {'run_id': run_id, 'images': image_hashes, 'readers': readers}


def cmd_plate(args):
    manifest_path = Path(args.manifest).resolve()
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    pairs = plate_images(manifest, args.plate)
    folder = WORK / ('%s-%s' % (args.run_id, args.plate))
    if folder.exists():
        shutil.rmtree(folder)
    folder.mkdir(parents=True)
    hashes = {}
    for key, neutral in pairs:
        entry = manifest['files'][key]
        src = Path(entry['path'])
        src = src if src.is_absolute() else manifest_path.parent / src
        digest = sha(src)
        if entry.get('sha256') and entry['sha256'] != digest:
            print('INPUT_ERROR: manifest 해시와 다름 ' + key, file=sys.stderr)
            return 2
        hashes[key] = digest
        shutil.copyfile(src, folder / neutral)
    answers = judge(folder, [n for _, n in pairs], has_motion=bool(manifest_plates_motion(pairs)))
    try:
        review = make_review(args.run_id, hashes, answers)
    except ValueError as e:
        print('JUDGE_ERROR: %s (로그: %s)' % (e, folder), file=sys.stderr)
        return 1
    Path(args.out).write_text(json.dumps(review, ensure_ascii=False, indent=1), encoding='utf-8')
    review_key = next((k for k, v in manifest['files'].items() if Path(v['path']).resolve() == Path(args.out).resolve()), None)
    if review_key is None:
        print('WARN: manifest에 이 판독 파일 경로가 없음 — G03에서 BLOCKED가 된다', file=sys.stderr)
    else:
        manifest['files'][review_key]['sha256'] = sha(args.out)
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    for r in review['readers']:
        print(r['name'], r['scores'], 'major', r['major_defects'])
    return 0


def manifest_plates_motion(pairs):
    return [n for _, n in pairs if n.startswith('seq-')]


def cmd_anchor(args):
    out_dir = Path(args.out_dir).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    jobs = {}
    for item in args.img:
        name, path = item.split('=', 1)
        folder = WORK / ('anchor-' + name)
        if folder.exists():
            shutil.rmtree(folder)
        folder.mkdir(parents=True)
        shutil.copyfile(path, folder / 'main.png')
        jobs[name] = (folder, sha(path))
    results = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=len(jobs)) as pool:
        futures = {name: pool.submit(judge, folder, ['main.png'], False) for name, (folder, _) in jobs.items()}
        for name, future in futures.items():
            results[name] = {'sha256': jobs[name][1], 'readers': future.result()}
    (out_dir / 'anchor-results.json').write_text(json.dumps(results, ensure_ascii=False, indent=1), encoding='utf-8')
    for name, r in results.items():
        for reader, a in r['readers'].items():
            print(name, reader, a.get('scores', a.get('error')), 'major', a.get('major_defects'))
    return 0


def selftest():
    cases = []
    good = '설명\n```json\n{"seen":{},"scores":{"style":3,"depth":4,"readability":3,"ground":3,"motion":4},"major_defects":0,"defects":[]}\n```'
    cases.append(('fenced json', check_answer(extract_json(good), AXES)['scores']['depth'] == 4))
    for bad, label in [('{"scores":{"style":5,"depth":3,"readability":3,"ground":3,"motion":3},"major_defects":0}', 'score 5'),
                       ('{"scores":{"style":3,"depth":3,"readability":3,"ground":3},"major_defects":0}', 'missing motion'),
                       ('{"scores":{"style":3,"depth":3,"readability":3,"ground":3,"motion":3}}', 'missing defects'),
                       ('no json here', 'no json')]:
        try:
            check_answer(extract_json(bad), AXES)
            cases.append((label + ' rejected', False))
        except ValueError:
            cases.append((label + ' rejected', True))
    no_motion = '{"scores":{"style":1,"depth":2,"readability":3,"ground":0,"motion":null},"major_defects":1}'
    cases.append(('anchor null motion ok', check_answer(extract_json(no_motion), AXES[:4])['major_defects'] == 1))
    answers = {'a': {'name': 'a', 'scores': {}, 'major_defects': 0}, 'b': {'name': 'b', 'error': 'timeout'}}
    try:
        make_review('r', {}, answers)
        cases.append(('reader error blocks review', False))
    except ValueError:
        cases.append(('reader error blocks review', True))
    pairs = plate_images({'plates': [{'id': 'p', 'main': 'm', 'motion': ['a', 'b'], 'aspects': ['w'], 'closeup': 'c'}]}, 'p')
    cases.append(('neutral names', [n for _, n in pairs] == ['main.png', 'seq-0.png', 'seq-1.png', 'aspect-0.png', 'closeup.png']))
    prompt = build_prompt(['main.png'], False)
    cases.append(('prompt hides plate id', 'plate' not in prompt and 'null' in prompt))
    for label, ok in cases:
        print(('PASS ' if ok else 'FAIL ') + label)
    return 0 if all(ok for _, ok in cases) else 1


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='command', required=True)
    p = sub.add_parser('plate')
    p.add_argument('manifest')
    p.add_argument('plate')
    p.add_argument('--run-id', required=True)
    p.add_argument('--out', required=True)
    a = sub.add_parser('anchor')
    a.add_argument('--img', action='append', required=True)
    a.add_argument('--out-dir', required=True)
    sub.add_parser('selftest')
    args = ap.parse_args()
    if args.command == 'selftest':
        return selftest()
    return cmd_plate(args) if args.command == 'plate' else cmd_anchor(args)


if __name__ == '__main__':
    sys.exit(main())
