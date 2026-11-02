#!/usr/bin/env python3
"""045: work-time and token report from the local Claude Code session logs (user request 2026-11-02: record total and
detailed work time, find slow / inefficient / token-heavy parts for the workflow update).
Usage: /usr/bin/python3 Tools/Proto/session_time_045.py [session.jsonl ...]
Default input: the newest session log of this project. Writes output/keyart-target-045/time/time-report.json and prints a
short summary. Message text is never copied; only timestamps, tool names, categories, sizes and token counts.

Time model (UTC timestamps in the log):
- foreground tool time = tool_use -> tool_result of the same id
- background job time = launch result ('running in background with ID' / async agent) -> task-notification of that id
- agent activity = foreground tool intervals + gaps < 120 s between consecutive log events
- idle = wall time minus (activity U background)
Tokens: Claude assistant usage per message id (input, cache creation, cache read, output); sub-agent tokens from the
task-notification usage block; Codex 'tokens used' from the codex logs under output/keyart-target-045.
"""
import glob, json, os, re, sys
from collections import defaultdict
from datetime import datetime
from pathlib import Path

ROOT = Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor')
LOGDIR = Path('/Users/hyeongwoo/.claude/projects/-Users-hyeongwoo-Desktop-Workspace-Deadsurvivor')
files = [Path(x) for x in sys.argv[1:]] or [max(LOGDIR.glob('*.jsonl'), key=os.path.getsize)]


def ts(s): return datetime.fromisoformat(s.replace('Z', '+00:00')).timestamp()


events, uses, results, bg_start, bg_end, sub_tokens, usage = [], {}, {}, {}, {}, {}, {}
prompts, v2_start = [], None
for f in files:
    with open(f) as fh:
        for line in fh:
            try: d = json.loads(line)
            except Exception: continue
            t = d.get('timestamp')
            if not t: continue
            t = ts(t); events.append(t); m = d.get('message') or {}
            if d.get('type') == 'assistant':
                mid = m.get('id')
                if mid and m.get('usage'):
                    u = m['usage']; old = usage.get(mid, {})
                    usage[mid] = dict(t=old.get('t', t), input=u.get('input_tokens', 0), cache_create=u.get('cache_creation_input_tokens', 0),
                                      cache_read=u.get('cache_read_input_tokens', 0), output=max(old.get('output', 0), u.get('output_tokens', 0)), tools=old.get('tools', []))
                for b in m.get('content') or []:
                    if isinstance(b, dict) and b.get('type') == 'tool_use':
                        uses[b['id']] = dict(t=t, name=b.get('name'), input=b.get('input') or {}, mid=mid)
                        if mid in usage: usage[mid]['tools'].append(b['id'])
            elif d.get('type') == 'user':
                c = m.get('content')
                if isinstance(c, list):
                    for b in c:
                        if isinstance(b, dict) and b.get('type') == 'tool_result':
                            body = b.get('content'); txt = body if isinstance(body, str) else json.dumps(body, ensure_ascii=False)
                            results[b.get('tool_use_id')] = dict(t=t, size=len(txt), image='"type": "image"' in txt)
                            mm = re.search(r'running in background with ID: (\w+)', txt) or re.search(r'agentId: (\w+)', txt)
                            if mm: bg_start[mm.group(1)] = (t, b.get('tool_use_id'))
                            if v2_start is None and '보완 기본형 1명 + 색칠' in txt: v2_start = t
                txt = c if isinstance(c, str) else ' '.join(b.get('text', '') for b in (c or []) if isinstance(b, dict) and b.get('type') == 'text')
                for mm in re.finditer(r'<task-id>(\w+)</task-id>.*?<status>(\w+)</status>', txt, re.S):
                    bg_end.setdefault(mm.group(1), t)
                for mm in re.finditer(r'<task-id>(\w+)</task-id>.*?<subagent_tokens>(\d+)</subagent_tokens>', txt, re.S):
                    sub_tokens[mm.group(1)] = int(mm.group(2))
                if txt and not d.get('isMeta') and '<task-notification>' not in txt and not txt.lstrip().startswith('<') and len(txt) < 400 \
                        and not any(isinstance(b, dict) and b.get('type') == 'tool_result' for b in (c if isinstance(c, list) else [])):
                    prompts.append(t)
events.sort()

# ---- phases (UTC) ---------------------------------------------------------------------------------------------------
P = [('042~044·논의', events[0]), ('045 계획(0단계)', ts('2026-11-02T01:33:27Z')), ('1단계', ts('2026-11-02T02:15:31Z')),
     ('2단계 + 3단계 v1(병렬)', ts('2026-11-02T05:10:23Z')), ('3단계 v2', v2_start or ts('2026-11-02T08:00:00Z'))]
def phase(t):
    name = P[0][0]
    for n, s in P:
        if t >= s: name = n
    return name

# ---- categories ------------------------------------------------------------------------------------------------------
def category(u):
    n, i = u['name'], u['input']; cmd = (i.get('command') or '') + ' ' + (i.get('description') or ''); path = i.get('file_path') or ''
    if n == 'Bash':
        if re.search(r'keyart_045\.py|proto_main_044\.py|windowed-batch|final-batch|capture_motion', cmd): return 'Unity Play 검증'
        if re.search(r'run\.py all|gate\.py', cmd): return '회귀·gate'
        if 'codex exec' in cmd: return 'Codex 이미지 생성' if (' -i ' in cmd or 'concept' in cmd) else 'Codex 검토'
        if re.search(r'tripo_045\.py \w* ?\w*.*(submit|fetch|anim)', cmd): return 'Tripo 생성·뼈대'
        if 'tripo' in cmd.lower(): return 'Tripo 구동기·조회'
        if 'Blender' in cmd: return 'Blender 처리'
        if re.search(r'unity (command|recompile|projects)', cmd): return 'Unity 빌드·조사'
        if 'docs/' in cmd: return '문서 작성'
        return 'Bash 기타'
    if n == 'Read': return '이미지 보기' if path.lower().endswith(('.png', '.jpg', '.gif', '.webp')) else '파일 읽기'
    if n in ('Edit', 'Write'): return '문서 작성' if '/docs/' in path or path.endswith('.md') else '코드 작성'
    if n == 'Agent': return '하위 에이전트(독립 검토)'
    if n == 'AskUserQuestion': return '사용자 질문'
    if n in ('WebFetch', 'WebSearch'): return '웹 조회'
    return '기타'

def stream(u):
    i = u['input']; s = ' '.join(str(i.get(k, '')) for k in ('command', 'description', 'file_path', 'prompt'))[:4000]
    if re.search(r'hero_base045v2|concept-v2|recolor|fix_rig_facing|s3b|unity-v2|heroes_045|six', s): return '3단계 v2'
    if re.search(r'hero_bruno045|Art_s3\b|build_hero_045_s3|fix_hero_back_eyes|_s3\.unity|r003/unity|s3-r0|impl-review-s3', s): return '3단계 v1'
    if re.search(r'Main_s2|keyart_045_s2|render_check|stone045|boulder045|tree045|Water045|r002/|s2-r0|impl-review-s2|final-batch-s23', s): return '2단계'
    return None

# ---- intervals -------------------------------------------------------------------------------------------------------
def union(iv):
    iv = sorted(iv); out = []
    for a, b in iv:
        if out and a <= out[-1][1]: out[-1][1] = max(out[-1][1], b)
        else: out.append([a, b])
    return out
def total(iv): return sum(b - a for a, b in union(iv))

fg = []; per_call = []
for tid, u in uses.items():
    r = results.get(tid)
    if not r: continue
    dur = max(0.0, r['t'] - u['t']); fg.append((u['t'], r['t']))
    per_call.append(dict(t=u['t'], dur=dur, cat=category(u), phase=stream(u) or phase(u['t']), size=r['size'], image=r['image'], name=u['name'],
                         desc=(u['input'].get('description') or u['input'].get('file_path') or '')[:80], tid=tid))
gaps = [(a, b) for a, b in zip(events, events[1:]) if b - a < 120]
active = union(fg + gaps)
bg = []; bg_jobs = []
for k, (t0, tid) in bg_start.items():
    t1 = bg_end.get(k)
    if t1 and t1 > t0:
        bg.append((t0, t1)); u = uses.get(tid, {'name': '?', 'input': {}})
        bg_jobs.append(dict(t=t0, dur=t1 - t0, cat=category(u) if u['name'] != '?' else '기타', phase=(stream(u) if u['name'] != '?' else None) or phase(t0),
                            desc=(u['input'].get('description') or '')[:80], sub_tokens=sub_tokens.get(k)))
wall = events[-1] - events[0]; busy = union(active + [list(x) for x in bg])

def hours(x): return round(x / 3600, 2)
rep = dict(files=[str(f) for f in files], start_utc=datetime.utcfromtimestamp(events[0]).isoformat(), end_utc=datetime.utcfromtimestamp(events[-1]).isoformat(),
           wall_h=hours(wall), agent_active_h=hours(total(active)), background_h=hours(total(bg)), busy_h=hours(total(busy)), idle_h=hours(wall - total(busy)),
           user_prompts=len(prompts), tool_calls=len(per_call), background_jobs=len(bg_jobs))

# per phase / category
ph = defaultdict(lambda: dict(fg_s=0.0, calls=0, bg_s=0.0, bg_jobs=0)); cat = defaultdict(lambda: dict(fg_s=0.0, calls=0, bg_s=0.0, bg_jobs=0, result_kb=0.0))
for c in per_call:
    ph[c['phase']]['fg_s'] += c['dur']; ph[c['phase']]['calls'] += 1; cat[c['cat']]['fg_s'] += c['dur']; cat[c['cat']]['calls'] += 1; cat[c['cat']]['result_kb'] += c['size'] / 1024
for j in bg_jobs:
    ph[j['phase']]['bg_s'] += j['dur']; ph[j['phase']]['bg_jobs'] += 1; cat[j['cat']]['bg_s'] += j['dur']; cat[j['cat']]['bg_jobs'] += 1
# phase wall spans
spans = {}
for i, (n, s) in enumerate(P):
    e = P[i + 1][1] if i + 1 < len(P) else events[-1]; spans[n] = dict(wall_h=hours(e - s), active_h=hours(total([[max(a, s), min(b, e)] for a, b in active if b > s and a < e])),
                                                                    busy_h=hours(total([[max(a, s), min(b, e)] for a, b in busy if b > s and a < e])))
rep['phases_time_window'] = spans
rep['by_stream'] = {k: dict(fg_h=hours(v['fg_s']), calls=v['calls'], bg_h=hours(v['bg_s']), bg_jobs=v['bg_jobs']) for k, v in ph.items()}
rep['by_category'] = {k: dict(fg_h=hours(v['fg_s']), calls=v['calls'], bg_h=hours(v['bg_s']), bg_jobs=v['bg_jobs'], result_kb=round(v['result_kb'])) for k, v in sorted(cat.items(), key=lambda x: -(x[1]['fg_s'] + x[1]['bg_s']))}

# tokens
tok = defaultdict(lambda: dict(input=0, cache_create=0, cache_read=0, output=0, msgs=0)); tok_cat = defaultdict(lambda: dict(cache_read=0, output=0, msgs=0))
cat_of_tid = {c['tid']: c['cat'] for c in per_call}
for mid, u in usage.items():
    k = phase(u['t']); [tok[k].__setitem__(x, tok[k][x] + u[x]) for x in ('input', 'cache_create', 'cache_read', 'output')]; tok[k]['msgs'] += 1
    c = cat_of_tid.get(u['tools'][0], '응답 글') if u['tools'] else '응답 글'
    tok_cat[c]['cache_read'] += u['cache_read']; tok_cat[c]['output'] += u['output']; tok_cat[c]['msgs'] += 1
rep['tokens_by_phase'] = {k: v for k, v in tok.items()}
rep['tokens_total'] = {x: sum(v[x] for v in tok.values()) for x in ('input', 'cache_create', 'cache_read', 'output', 'msgs')}
rep['tokens_by_category'] = dict(sorted(tok_cat.items(), key=lambda x: -x[1]['cache_read']))
rep['subagent_tokens'] = dict(total=sum(v for v in sub_tokens.values()), runs=len(sub_tokens))
codex = []
for lg in glob.glob(str(ROOT / 'output/keyart-target-045/**/*.log'), recursive=True) + glob.glob(str(ROOT / 'output/keyart-target-045/**/*run.log'), recursive=True):
    try: txt = Path(lg).read_text(errors='ignore')
    except Exception: continue
    mm = re.search(r'tokens used\s*\n?\s*([\d,]+)', txt)
    if mm and 'codex' in txt[:400].lower(): codex.append(dict(log=str(Path(lg).relative_to(ROOT)), tokens=int(mm.group(1).replace(',', ''))))
codex = list({c['log']: c for c in codex}.values())
rep['codex'] = dict(runs=len(codex), tokens=sum(c['tokens'] for c in codex), by_kind={k: sum(c['tokens'] for c in codex if k in c['log']) for k in ('design-review', 'impl-review', 'concept')})

# hot spots
rep['longest_foreground'] = [dict(phase=c['phase'], cat=c['cat'], min=round(c['dur'] / 60, 1), what=c['desc']) for c in sorted(per_call, key=lambda c: -c['dur'])[:15]]
rep['longest_background'] = [dict(phase=j['phase'], cat=j['cat'], min=round(j['dur'] / 60, 1), what=j['desc']) for j in sorted(bg_jobs, key=lambda j: -j['dur'])[:15]]
rep['largest_results_kb'] = [dict(phase=c['phase'], cat=c['cat'], kb=round(c['size'] / 1024, 1), what=c['desc']) for c in sorted(per_call, key=lambda c: -c['size'])[:12]]
rep['images_viewed'] = sum(1 for c in per_call if c['image'])
rep['play_runs'] = sum(1 for j in bg_jobs if j['cat'] == 'Unity Play 검증') + sum(1 for c in per_call if c['cat'] == 'Unity Play 검증' and c['dur'] > 120)
out = ROOT / 'output/keyart-target-045/time'; out.mkdir(parents=True, exist_ok=True)
(out / 'time-report.json').write_text(json.dumps(rep, ensure_ascii=False, indent=1))
print(json.dumps({k: rep[k] for k in ('start_utc', 'end_utc', 'wall_h', 'agent_active_h', 'background_h', 'busy_h', 'idle_h', 'user_prompts', 'tool_calls', 'background_jobs', 'tokens_total', 'subagent_tokens', 'codex', 'images_viewed', 'play_runs')}, ensure_ascii=False, indent=1))
