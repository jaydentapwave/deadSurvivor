#!/usr/bin/env python3
"""Work time and tokens per user request from a Claude Code session log (047 H9; AGENTS: record time and tokens).
Ported from Deadsurvivor Tools/Harness/session_time.py (047 H9).

  /usr/bin/python3 3dkit/tools/session_time.py [--session <id or .jsonl> ...] [--markdown] [--preview N]
    default session: the most recently modified log of this project
    --markdown   print the per-request table (paste into the stage record) instead of JSON
    --preview N  add the first N characters of each request (default 0: no message text is copied)
    --selftest
Writes output/session-time/<session>.json.

Per request (= one user prompt up to the next): span (prompt -> last event), active time (tool intervals + gaps under
120 s; the rest is waiting), tool calls, images viewed, background jobs, Claude tokens (output, cache read, cache
creation), context size at the first and last reply, sub-agent tokens (task notifications).
Codex: logs under output/ that contain "tokens used", modified inside the session window, assigned to the request in
which they were written (not every Codex log of a folder, 047-E7)."""
import argparse, json, os, re, sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]   # <project>/3dkit/tools/session_time.py -> <project>
LOGDIR = Path.home() / '.claude/projects' / ('-' + str(ROOT).strip('/').replace('/', '-'))
GAP = 120


def ts(s):
    return datetime.fromisoformat(s.replace('Z', '+00:00')).timestamp()


def is_prompt(d):
    if d.get('type') != 'user' or d.get('isMeta') or d.get('isSidechain'):
        return None
    c = (d.get('message') or {}).get('content')
    if isinstance(c, list):
        if any(isinstance(b, dict) and b.get('type') == 'tool_result' for b in c):
            return None
        c = ' '.join(b.get('text', '') for b in c if isinstance(b, dict) and b.get('type') == 'text')
    if not isinstance(c, str) or not c.strip():
        return None
    t = c.lstrip()
    if t.startswith('<') or '<task-notification>' in c:     # slash-command echoes, notifications, caveats
        return None
    return c


def load(files):
    ev, prompts, uses, results, usage, bg_start, bg_end, sub = [], [], {}, {}, {}, {}, {}, {}
    for f in files:
        for line in open(f, errors='ignore'):
            try:
                d = json.loads(line)
            except Exception:
                continue
            if not d.get('timestamp'):
                continue
            t = ts(d['timestamp']); ev.append(t); m = d.get('message') or {}
            p = is_prompt(d)
            if p is not None:
                prompts.append((t, p))
            if d.get('type') == 'assistant':
                mid = m.get('id')
                if mid and m.get('usage'):
                    u = m['usage']; old = usage.get(mid, {})
                    usage[mid] = dict(t=old.get('t', t), input=u.get('input_tokens', 0), cache_read=u.get('cache_read_input_tokens', 0),
                                      cache_create=u.get('cache_creation_input_tokens', 0), output=max(old.get('output', 0), u.get('output_tokens', 0)))
                for b in m.get('content') or []:
                    if isinstance(b, dict) and b.get('type') == 'tool_use':
                        uses[b['id']] = dict(t=t, name=b.get('name'), input=b.get('input') or {})
            elif d.get('type') == 'user':
                c = m.get('content')
                for b in c if isinstance(c, list) else []:
                    if isinstance(b, dict) and b.get('type') == 'tool_result':
                        body = b.get('content'); txt = body if isinstance(body, str) else json.dumps(body, ensure_ascii=False)
                        results[b.get('tool_use_id')] = dict(t=t, image='"type": "image"' in txt)
                        mm = re.search(r'running in background with ID: (\w+)', txt) or re.search(r'agentId: (\w+)', txt)
                        if mm:
                            bg_start[mm.group(1)] = t
                txt = c if isinstance(c, str) else ' '.join(b.get('text', '') for b in (c or []) if isinstance(b, dict) and b.get('type') == 'text')
                for mm in re.finditer(r'<task-id>(\w+)</task-id>.*?<status>(\w+)</status>', txt or '', re.S):
                    bg_end.setdefault(mm.group(1), t)
                for mm in re.finditer(r'<task-id>(\w+)</task-id>.*?<subagent_tokens>(\d+)</subagent_tokens>', txt or '', re.S):
                    sub[mm.group(1)] = (t, int(mm.group(2)))
    ev.sort(); prompts.sort()
    return ev, prompts, uses, results, usage, bg_start, bg_end, sub


def union(iv):
    out = []
    for a, b in sorted(iv):
        if out and a <= out[-1][1]:
            out[-1][1] = max(out[-1][1], b)
        else:
            out.append([a, b])
    return out


def codex_logs(t0, t1):
    rows = []
    for p in (ROOT / 'output').rglob('*'):
        if p.suffix not in ('.log', '.out', '.txt') or not p.is_file():
            continue
        try:
            mt = p.stat().st_mtime
            if not t0 <= mt <= t1 + 600:
                continue
            txt = p.read_text(errors='ignore')
        except OSError:
            continue
        mm = re.search(r'tokens used\s*\n?\s*([\d,]+)', txt)
        if mm and 'codex' in txt[:2000].lower():
            rows.append(dict(log=str(p.relative_to(ROOT)), t=mt, tokens=int(mm.group(1).replace(',', ''))))
    return rows


def report(files, preview=0):
    ev, prompts, uses, results, usage, bg_start, bg_end, sub = load(files)
    if not ev:
        raise SystemExit('empty session log')
    t_end = ev[-1]
    bounds = [p[0] for p in prompts] + [t_end + 1]
    codex = codex_logs(ev[0], t_end)
    rows = []
    for i, (t0, text) in enumerate(prompts):
        t1 = bounds[i + 1]
        tools = [(u, results.get(k)) for k, u in uses.items() if t0 <= u['t'] < t1]
        # the agent's own events only (replies, tool calls/results); meta entries and echoes do not extend the request
        inside = sorted([t0] + [u['t'] for u in usage.values() if t0 <= u['t'] < t1] + [u['t'] for u, _ in tools] + [r['t'] for _, r in tools if r and r['t'] < t1])
        last = inside[-1]
        fg = [(u['t'], r['t']) for u, r in tools if r]
        gaps = [(a, b) for a, b in zip(inside, inside[1:]) if b - a < GAP]
        active = sum(b - a for a, b in union(fg + gaps))
        msgs = sorted((u for u in usage.values() if t0 <= u['t'] < t1), key=lambda u: u['t'])
        tok = {k: sum(u[k] for u in msgs) for k in ('output', 'cache_read', 'cache_create', 'input')}
        ctx = lambda u: u['input'] + u['cache_read'] + u['cache_create']
        jobs = [k for k, s in bg_start.items() if t0 <= s < t1]
        row = dict(n=i + 1, start=datetime.fromtimestamp(t0).strftime('%H:%M'), span_min=round((last - t0) / 60, 1), active_min=round(active / 60, 1),
                   tool_calls=len(tools), images=sum(1 for _, r in tools if r and r['image']),
                   background_jobs=len(jobs), background_min=round(sum(max(0, bg_end.get(k, s) - s) for k, s in bg_start.items() if k in jobs) / 60, 1),
                   **tok, context_first=ctx(msgs[0]) if msgs else 0, context_last=ctx(msgs[-1]) if msgs else 0,
                   subagent_tokens=sum(v for t, v in sub.values() if t0 <= t < t1),
                   codex_runs=sum(1 for c in codex if t0 <= c['t'] < t1), codex_tokens=sum(c['tokens'] for c in codex if t0 <= c['t'] < t1))
        if preview:
            row['request'] = ' '.join(text.split())[:preview]
        rows.append(row)
    total = {k: sum(r[k] for r in rows) for k in ('active_min', 'tool_calls', 'images', 'background_jobs', 'output', 'cache_read', 'cache_create', 'subagent_tokens', 'codex_runs', 'codex_tokens')}
    total['active_min'] = round(total['active_min'], 1)
    total['wall_min'] = round((t_end - ev[0]) / 60, 1)
    return dict(files=[str(f) for f in files], start=datetime.fromtimestamp(ev[0]).isoformat(timespec='minutes'),
                end=datetime.fromtimestamp(t_end).isoformat(timespec='minutes'), requests=rows, total=total, codex_logs=[c['log'] for c in codex])


def fmt_m(n):
    return f'{n / 1e6:.1f}M' if n >= 1e6 else f'{round(n / 1000)}k'


def markdown(rep):
    head = '| # | 시작 | 소요(분) | 활동(분) | 도구 호출 | 출력 토큰 | 캐시 읽기 | 캐시 생성 | 맥락(끝) | 하위 에이전트 | Codex |'
    lines = [head, '|' + '|'.join(['---'] * (head.count('|') - 1)) + '|']
    for r in rep['requests']:
        lines.append(f"| {r['n']} | {r['start']} | {r['span_min']} | {r['active_min']} | {r['tool_calls']} | {r['output']:,} | {fmt_m(r['cache_read'])} | "
                     f"{fmt_m(r['cache_create'])} | {fmt_m(r['context_last'])} | {r['subagent_tokens'] or '-'} | "
                     f"{(str(r['codex_runs']) + '회 ' + fmt_m(r['codex_tokens'])) if r['codex_runs'] else '-'} |" + (f" {r['request']}" if 'request' in r else ''))
    t = rep['total']
    lines.append(f"\n합계: 벽시계 {t['wall_min']}분, 활동 {t['active_min']}분, 도구 호출 {t['tool_calls']}회, 이미지 {t['images']}회, 백그라운드 {t['background_jobs']}건, "
                 f"출력 {t['output']:,}, 캐시 읽기 {fmt_m(t['cache_read'])}, 캐시 생성 {fmt_m(t['cache_create'])}, 하위 에이전트 {t['subagent_tokens']:,}, Codex {t['codex_runs']}회 {t['codex_tokens']:,}")
    return '\n'.join(lines)


def selftest():
    import tempfile
    def a(t, mid, out, cr, tools=()):
        return dict(type='assistant', timestamp=t, message=dict(id=mid, usage=dict(input_tokens=1, output_tokens=out, cache_read_input_tokens=cr, cache_creation_input_tokens=10),
                                                               content=[dict(type='tool_use', id=x, name='Bash', input={}) for x in tools]))
    def r(t, tid, txt='ok'):
        return dict(type='user', timestamp=t, message=dict(content=[dict(type='tool_result', tool_use_id=tid, content=txt)]))
    log = [dict(type='user', timestamp='2026-11-03T01:00:00Z', message=dict(content='첫 요청')),
           a('2026-11-03T01:00:05Z', 'm1', 100, 1000, ['t1']), r('2026-11-03T01:00:35Z', 't1'),
           a('2026-11-03T01:00:40Z', 'm2', 50, 2000),
           dict(type='user', timestamp='2026-11-03T01:01:00Z', message=dict(content='<command-name>/x</command-name>')),
           dict(type='user', timestamp='2026-11-03T01:01:00Z', isMeta=True, message=dict(content='meta')),
           dict(type='user', timestamp='2026-11-03T01:30:00Z', message=dict(content=[dict(type='text', text='두 번째 요청')])),
           a('2026-11-03T01:30:10Z', 'm3', 7, 5000, ['t2']), r('2026-11-03T01:30:20Z', 't2', 'Command running in background with ID: bg1. x'),
           dict(type='user', timestamp='2026-11-03T01:35:20Z', message=dict(content='<task-notification><task-id>bg1</task-id><status>completed</status></task-notification>')),
           a('2026-11-03T01:35:30Z', 'm4', 9, 6000)]
    c = {}
    with tempfile.TemporaryDirectory() as td:
        f = Path(td) / 's.jsonl'; f.write_text('\n'.join(json.dumps(x, ensure_ascii=False) for x in log) + '\n')
        rep = report([f], preview=10); q = rep['requests']
        c['two_requests'] = len(q) == 2 and q[0]['request'] == '첫 요청' and q[1]['request'] == '두 번째 요청'
        c['tokens_split'] = (q[0]['output'], q[0]['cache_read'], q[1]['output']) == (150, 3000, 16)
        c['context_last'] = q[0]['context_last'] == 2011 and q[1]['context_first'] == 5011
        c['waiting_excluded'] = q[0]['span_min'] == 0.7 and q[0]['active_min'] == 0.7
        c['background_job'] = q[1]['background_jobs'] == 1 and q[1]['background_min'] == 5.0
        c['no_text_by_default'] = 'request' not in report([f])['requests'][0]
        c['markdown'] = markdown(rep).count('\n| ') >= 2
    ok = all(c.values())
    print(json.dumps({'status': 'PASS' if ok else 'FAIL', 'cases': c}, ensure_ascii=False))
    return ok


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('--session', nargs='*', default=[])
    p.add_argument('--markdown', action='store_true')
    p.add_argument('--preview', type=int, default=0)
    p.add_argument('--selftest', action='store_true')
    a = p.parse_args()
    if a.selftest:
        sys.exit(0 if selftest() else 1)
    files = [Path(s) if s.endswith('.jsonl') else LOGDIR / (s + '.jsonl') for s in a.session] or [max(LOGDIR.glob('*.jsonl'), key=os.path.getmtime)]
    rep = report(files, a.preview)
    out = ROOT / 'output/session-time'; out.mkdir(parents=True, exist_ok=True)
    (out / (files[0].stem + '.json')).write_text(json.dumps(rep, ensure_ascii=False, indent=1))
    print(markdown(rep) if a.markdown else json.dumps(dict(start=rep['start'], end=rep['end'], total=rep['total'], requests=len(rep['requests']),
                                                            report=str((out / (files[0].stem + '.json')).relative_to(ROOT))), ensure_ascii=False))


if __name__ == '__main__':
    main()
