#!/usr/bin/env python3
"""Hand a review agent's job to Codex and return Codex's verdict (user 2026-11-15: "리뷰 담당을 codex로 변경을해줘").

The review agents in .claude/agents/ (design-reviewer, spec-reviewer, validator-client, algorithm-tester) are relays:
they pass the briefing they received to this tool and answer with what it prints. The role instructions Codex follows
live in .ai/reviewers/<role>.md. The SubagentStop hook still records the relayed VERDICT line, so director gates work.

  codex_review.py start --role <role> [--brief-file <file>]   briefing from the file or stdin. Prints "JOB <id>"
  codex_review.py wait <id> [--max 540]                       waits up to --max seconds (one Bash call stays < 10 min).
                                                              exit 0: prints Codex's answer, VERDICT line first
                                                              exit 3: still running ("RUNNING <s>s"), call wait again
  codex_review.py selftest                                    prompt building and verdict handling, no Codex call

Codex: model gpt-6.2-astra, reasoning medium (user 2026-11-15). Reviews are read-only on the repo; algorithm-tester gets a
writable scratch folder outside the repo so it can run extracted code. Total limit 40 minutes per job.
"""
import argparse
import json
import os
import re
import shlex
import subprocess
import sys
import time
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ROLES_DIR = ROOT / '.ai' / 'reviewers'
JOBS = Path(os.environ.get('TMPDIR', '/tmp')) / 'deadsurvivor-codex-review'
MODEL = 'gpt-6.2-astra'
EFFORT = 'medium'
LIMIT_SECONDS = 2400
SCRATCH_ROLES = {'algorithm-tester'}
VERDICT = re.compile(r'^[\s`*>#-]*VERDICT:\s*(PASS|FAIL|NOT ASSESSED)\b.*$', re.M)


def build_prompt(role, brief, scratch):
    rules = (ROLES_DIR / (role + '.md')).read_text(encoding='utf-8')
    where = ('- 쓰기는 지금 작업 폴더(레포 밖 시험용 폴더)에서만 한다. 레포 파일은 절대 경로로 읽기만 한다.'
             if scratch else '- 이 실행은 읽기 전용이다. 파일을 고치지 않는다.')
    return '\n'.join([
        f'너는 이 Unity 프로젝트의 `{role}` 리뷰 담당이다. 아래 "역할 지침"을 따르고 "이번 리뷰 브리핑"을 검토한다.',
        f'- 레포 루트: {ROOT}',
        where,
        '- 리뷰는 새 맥락에서 한다. 브리핑이 가리키는 파일·diff·기준만 보고 판단한다. 레포의 CLAUDE.md·AGENTS.md·.ai/rules/는 필요하면 읽는다.',
        '- 답의 첫 줄은 반드시 다음 중 하나다: `VERDICT: PASS`, `VERDICT: FAIL`, `VERDICT: NOT ASSESSED — <이유>`.',
        '  그 아래에 지적을 "필수 수정"과 "후속 제안"으로 나눠 파일:줄과 근거를 적는다. 실행하지 않은 확인을 통과로 적지 않는다.',
        '',
        '## 역할 지침',
        '',
        rules.strip(),
        '',
        '## 이번 리뷰 브리핑',
        '',
        brief.strip(),
        '',
    ])


def normalize(answer, why_missing):
    """Put the VERDICT line first. No VERDICT at all -> NOT ASSESSED with the reason."""
    m = VERDICT.search(answer or '')
    if not m:
        return 'VERDICT: NOT ASSESSED — Codex 답에 VERDICT 줄이 없음(' + why_missing + ')\n\n' + (answer or '').strip() + '\n'
    line = m.group(0).replace('**', '').replace('`', '').strip().lstrip('>#- ')
    rest = (answer[:m.start()] + answer[m.end():]).strip()
    return line + '\n\n' + rest + '\n'


def start(args):
    if not (ROLES_DIR / (args.role + '.md')).is_file():
        print(f'역할 지침이 없다: {ROLES_DIR / (args.role + ".md")}', file=sys.stderr)
        return 2
    brief = Path(args.brief_file).read_text(encoding='utf-8') if args.brief_file else sys.stdin.read()
    if not brief.strip():
        print('브리핑이 비어 있다', file=sys.stderr)
        return 2
    job = time.strftime('%Y%m%d-%H%M%S-') + uuid.uuid4().hex[:6]
    d = JOBS / job
    d.mkdir(parents=True)
    scratch = args.role in SCRATCH_ROLES
    cwd = d / 'work' if scratch else ROOT
    if scratch:
        cwd.mkdir()
    (d / 'prompt.md').write_text(build_prompt(args.role, brief, scratch), encoding='utf-8')
    sandbox = 'workspace-write' if scratch else 'read-only'
    codex = ['codex', 'exec', '-s', sandbox, '-C', str(cwd), '-m', MODEL,
             '-c', f'model_reasoning_effort="{EFFORT}"', '-o', str(d / 'out.md')]
    if scratch:
        codex.append('--skip-git-repo-check')
    q = ' '.join(shlex.quote(x) for x in codex)
    script = (f"perl -e 'alarm shift; exec @ARGV' {LIMIT_SECONDS} {q} \"$(cat {shlex.quote(str(d / 'prompt.md'))})\" "
              f"< /dev/null > {shlex.quote(str(d / 'log.txt'))} 2>&1; echo $? > {shlex.quote(str(d / 'exit.txt'))}\n")
    (d / 'run.sh').write_text(script, encoding='utf-8')
    proc = subprocess.Popen(['/bin/sh', str(d / 'run.sh')], stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                            stderr=subprocess.DEVNULL, start_new_session=True)
    (d / 'meta.json').write_text(json.dumps({'role': args.role, 'pid': proc.pid, 'started': time.time(),
                                             'model': MODEL, 'effort': EFFORT, 'sandbox': sandbox}), encoding='utf-8')
    print('JOB ' + job)
    return 0


def wait(args):
    d = JOBS / args.job
    meta = json.loads((d / 'meta.json').read_text(encoding='utf-8'))
    end = time.time() + args.max
    while not (d / 'exit.txt').exists() and time.time() < end:
        time.sleep(5)
    elapsed = int(time.time() - meta['started'])
    if not (d / 'exit.txt').exists():
        print(f'RUNNING {elapsed}s (job {args.job}) — wait를 다시 부른다')
        return 3
    code = (d / 'exit.txt').read_text().strip()
    log_tail = ''
    if (d / 'log.txt').exists():
        log_tail = ' / '.join((d / 'log.txt').read_text(errors='replace').strip().splitlines()[-3:])[:400]
    answer = (d / 'out.md').read_text(encoding='utf-8', errors='replace') if (d / 'out.md').exists() else ''
    why = f'exit {code}' + ('(시간 상한)' if code == '142' else '') + (f', 로그 끝: {log_tail}' if log_tail else '')
    print(normalize(answer, why), end='')
    print(f'\n(Codex {meta["model"]}, 추론 {meta["effort"]}, {meta["sandbox"]}, {elapsed}초, job {args.job}, 로그 {d / "log.txt"})')
    return 0


def selftest(_args):
    ok = True
    for role in ('design-reviewer', 'spec-reviewer', 'validator-client', 'algorithm-tester'):
        has = (ROLES_DIR / (role + '.md')).is_file()
        ok &= has
        if has:
            p = build_prompt(role, '브리핑 내용', role in SCRATCH_ROLES)
            ok &= '브리핑 내용' in p and 'VERDICT: PASS' in p
        print(role, 'role file', has)
    cases = {
        'VERDICT: PASS\nok': 'VERDICT: PASS',
        'intro\n**VERDICT: FAIL** — x\nmore': 'VERDICT: FAIL',
        'no verdict here': 'VERDICT: NOT ASSESSED',
        '': 'VERDICT: NOT ASSESSED',
    }
    for text, want in cases.items():
        got = normalize(text, 'test').splitlines()[0]
        good = got.startswith(want)
        ok &= good
        print('normalize', repr(text[:20]), '->', got[:40], 'OK' if good else 'BAD')
    print('SELFTEST', 'PASS' if ok else 'FAIL')
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    s = sub.add_parser('start')
    s.add_argument('--role', required=True)
    s.add_argument('--brief-file')
    w = sub.add_parser('wait')
    w.add_argument('job')
    w.add_argument('--max', type=int, default=540)
    sub.add_parser('selftest')
    args = ap.parse_args()
    return {'start': start, 'wait': wait, 'selftest': selftest}[args.cmd](args)


if __name__ == '__main__':
    sys.exit(main())
