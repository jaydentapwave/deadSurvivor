#!/usr/bin/env python3
"""3dkit Claude Code hooks: efficiency guards (art pack core rules, .ai/rules/art-core-rules.md). Policy: 3dkit.config.json "harness"
(full_runners, full_override_env, review, context; defaults below). Ported from an earlier project's claude_guard.py (048).
Connect in the project's .claude/settings.json (the 3d-art setup does it): PreToolUse(Bash) -> `python3 -B "$CLAUDE_PROJECT_DIR/<tools>/guard.py" pretool`,
UserPromptSubmit -> `... guard.py prompt`. The project root is $CLAUDE_PROJECT_DIR (hooks) or the folder with 3dkit.config.json
above the current folder; the tool's own location does not matter.

This is an efficiency guard, not a permission boundary: it fails open (an internal error allows the action) and only sees Bash.
Permissions belong to the Claude Code permission settings and to the tools themselves (e.g. the paid-generation gate).

  pretool  (PreToolUse, Bash)
    1. a full Play run (policy full_runners) without <full_override_env>=... in the command -> deny with a one-line reason
    2. a Codex review (`codex exec ... -o <dir>/<topic>-rNNN.md`) when <topic> already has max_rounds_per_topic results
       in that folder, without <review.override_env>=... -> deny (ask the user first)
    3. `git commit --no-verify` -> deny
    4. `codex exec` without closed input (`< /dev/null`, another redirect, a heredoc or a pipe into it) -> deny. Otherwise it
       waits for end of input forever and no completion notice ever comes (problem log W12, W36: 20 minutes, 1 hour)
    5. `assets.py gen fetch --wait S` above tripo_fetch_wait_max (900) -> deny. The bounded wait is what ends the command and sends the notice
  prompt   (UserPromptSubmit)
    when the conversation context passes warn_tokens, tell the user once (no new-session proposal below new_session_tokens,
    core rule 4); past new_session_tokens (then every step_tokens), ask the agent to write a handoff and propose a new
    session before a new big task. Without a 3dkit.config.json it says once that the defaults are in use.
    Also: a Tripo task with no result past tripo_overdue_seconds (900; 58 past jobs: median 3.1, max 5.4 minutes) -> tell the agent to check.
  stop     (Stop)
    the same overdue Tripo task -> block the stop once per overdue step, so the agent checks before it goes idle. A hook runs only on an event
    and cannot wake an idle agent: the bounded `gen fetch --wait` ending is what wakes it. It only ever asks for `gen status` and
    `gen fetch`, never a new submit (double charge). An unresolved state older than a day is said once per session and does not block.

Fail-open: any internal error allows the action (errors go to output/hook-state/errors.log). Never re-runs anything,
never loops: one short message per decision.
Selftest: python3 .ai/3d-art/tools/guard.py selftest (fixture policy, no project files)"""
import json, os, re, sys, time
from datetime import datetime
from pathlib import Path

def find_root():
    """$CLAUDE_PROJECT_DIR if it holds the config, else the first folder upward from cwd that does, else $CLAUDE_PROJECT_DIR or cwd"""
    env = os.environ.get('CLAUDE_PROJECT_DIR')
    if env and (Path(env) / '3dkit.config.json').is_file():
        return Path(env).resolve()
    d = Path.cwd().resolve()
    for p in (d, *d.parents):
        if (p / '3dkit.config.json').is_file():
            return p
    return Path(env).resolve() if env else d


ROOT = find_root()
STATE = ROOT / 'output/hook-state'
RULE_PATHS = ('.ai/rules/art-core-rules.md', '3dkit/skills/art-setup/template/root/.ai/rules/art-core-rules.md')


def rules(root=None):
    """where the core rules live in this project (installed path first, then the kit's development path)"""
    r = Path(root or ROOT)
    return next((x for x in RULE_PATHS if (r / x).is_file()), RULE_PATHS[0])


DEFAULTS = {'tripo_overdue_seconds': 900, 'tripo_fetch_wait_max': 900, 'full_runners': [], 'full_override_env': 'FULL_REASON', 'review': {'max_rounds_per_topic': 2, 'override_env': 'REVIEW_OK'},
            'context': {'warn_tokens': 250000, 'new_session_tokens': 300000, 'step_tokens': 100000}}
# selftest fixture: the runner patterns an earlier project used (048), so the 40+ command cases keep their meaning
FIXTURE = {'full_runners': [{'match': 'Tools/Proto/keyart_045.py', 'full_if': "KEYART045_STAGE=['\"]?(full|load|baseline044)\\b"},
                            {'match': 'Tools/Proto/proto_main_044.py', 'full_if': '.*'}, {'match': 'Tools/Verify/run.py', 'full_if': "run\\.py['\"]?\\s+all\\b"}],
           'full_override_env': 'DS_FULL_REASON', 'review': {'max_rounds_per_topic': 2, 'override_env': 'DS_REVIEW_OK'},
           'context': DEFAULTS['context'], 'tripo_overdue_seconds': 900, 'tripo_fetch_wait_max': 900}


def policy(root=ROOT):
    f = root / '3dkit.config.json'
    h = (json.loads(f.read_text()).get('harness') or {}) if f.exists() else {}
    return {**DEFAULTS, **h, 'review': {**DEFAULTS['review'], **(h.get('review') or {})}, 'context': {**DEFAULTS['context'], **(h.get('context') or {})}}


def _has_env(cmd, name):
    m = re.search(r'(?:^|[\s;&|(])' + re.escape(name) + r'=("([^"]+)"|\'([^\']+)\'|([^\s;&|\'"][^\s;&|]*))', cmd)
    return bool(m and (m.group(2) or m.group(3) or m.group(4) or '').strip())


_SHELL = r'(?:^|[\s;&|(`])(?:/\S*/)?(?:ba|z|da)?sh'
_SHELL_C = re.compile(_SHELL + r'\s+(?:-\w+\s+)*-\w*c\w*\s*$')          # bash -c, bash -lc, sh -ec ...
_SHELL_STDIN = re.compile(_SHELL + r'\b[^;&|\n]*$')                        # heredoc whose command is a shell: body runs
_SUBST = re.compile(r'\$\((?:[^()]|\([^()]*\))*\)|`[^`]*`')


def executed_text(cmd, depth=0):
    """The parts of a Bash command that the shell runs as commands (047-E0 / H2, review 048 r001 #1). Removed: heredoc
    bodies and the content of quoted strings that contain whitespace (echo/commit messages/prompts). Kept: assignment
    values (VAR="..."), quoted single words (a quoted runner path still counts), and everything that runs: `$(...)` /
    backticks inside double quotes, `bash|sh|zsh -c '...'` scripts (combined flags like -lc; analysed again, so prose
    inside them is still prose) and heredoc bodies fed to a shell (`bash <<EOF`)."""
    if depth > 4:
        return cmd
    out, i, n, pending = [], 0, len(cmd), []
    while i < n:
        c = cmd[i]
        if c == '\\' and i + 1 < n:
            out.append(cmd[i:i + 2]); i += 2; continue
        if c in '"\'':
            j = i + 1
            while j < n and cmd[j] != c:
                j += 2 if c == '"' and cmd[j] == '\\' else 1
            body, prefix = cmd[i + 1:j], ''.join(out)
            if _SHELL_C.search(prefix):
                kept = executed_text(body, depth + 1)
            elif prefix.endswith('=') or not re.search(r'\s', body):
                kept = body
            else:   # prose, but command substitutions inside double quotes still run
                kept = ' '.join(executed_text(m.group(0), depth + 1) for m in _SUBST.finditer(body)) if c == '"' else ''
            out.append(c + kept + c); i = j + 1; continue
        if cmd.startswith('<<', i) and not cmd.startswith('<<<', i):
            m = re.match(r'<<(-?)\s*(["\']?)([A-Za-z_][\w.-]*)\2', cmd[i:])
            if m:
                line_start = ''.join(out).rsplit('\n', 1)[-1]
                pending.append((m.group(3), m.group(1) == '-', bool(_SHELL_STDIN.search(line_start))))
                out.append(m.group(0)); i += m.end(); continue
        if c == '\n' and pending:
            out.append('\n'); i += 1
            for delim, tabs, runs in pending:    # bodies follow the line in order; each ends at its terminator
                body = []
                while i < n:
                    e = cmd.find('\n', i); e = n if e < 0 else e
                    line, i = cmd[i:e], e + 1
                    if (line.lstrip('\t') if tabs else line) == delim:
                        break
                    body.append(line)
                if runs:
                    out.append(executed_text('\n'.join(body), depth + 1) + '\n')
            pending = []; continue
        out.append(c); i += 1
    return ''.join(out)


def check_full(cmd, pol):
    env = pol['full_override_env']
    cmd = executed_text(cmd)
    for r in pol['full_runners']:
        # only a python invocation of the runner counts (a mention in echo/grep/docs does not)
        invoked = re.search(r'python[\d.]*\s+(?:-\S+\s+)*["\']?[^\s;&|"\']*' + re.escape(r['match']), cmd)
        if invoked and re.search(r['full_if'], cmd) and not _has_env(cmd, env):
            return (f"전체 Play 실행({r['match']})은 단계 마감·커밋 전에만 한다. 작업 중에는 짧은 단계(기본)를 쓴다. "
                    f"마감 실행이면 명령 앞에 {env}=\"<이유>\"를 붙인다 ({rules()} 1절).")
    return None


def check_no_verify(cmd):
    """the commit gate has one recorded way around it (GATE_SKIP); --no-verify leaves no trace"""
    cmd = executed_text(cmd)
    if re.search(r'(?:^|[\s;&|(])git\s+(?:-\S+\s+(?:\S+\s+)?)*commit\b[^;&|\n]*\s(?:--no-verify|-n)\b', cmd):
        return '커밋 전 검사를 건너뛰는 --no-verify는 쓰지 않는다. 꼭 건너뛰어야 하면 사용자에게 묻고 GATE_SKIP="<이유>" git commit으로 기록을 남긴다 (gate.py).'
    return None


_CODEX = re.compile(r'(?:^|[\s;&|(/"\'])codex\s+(?:\S+\s+)*?exec\b')
_NOT_RUN = re.compile(r'\s*(?:echo|printf|grep|rg|which|man)\b')          # these only name the command


def _codex_without_input(cmd, depth=0):
    """True when a `codex exec` runs with stdin left open. Works on executed_text, so prose in quotes and heredoc bodies do
    not count. Substitutions are checked on their own: `< p.md` inside "$(cat < p.md)" does not close codex's input."""
    if depth > 4:
        return False
    if any(_codex_without_input(m[2:-1] if m[0] == '$' else m[1:-1], depth + 1) for m in _SUBST.findall(cmd)):
        return True
    text = re.sub(r'\d*>&\d*|&>', ' ', _SUBST.sub(' ', cmd))                   # 2>&1 is not a command separator
    parts = re.split(r'(\|\||&&|\||&|;|\n)', text)
    for i in range(0, len(parts), 2):
        seg = parts[i]
        if not _CODEX.search(seg) or _NOT_RUN.match(seg) or re.search(r'\s(?:--help|-h)\b', seg):
            continue
        piped = i > 0 and parts[i - 1] == '|'
        if not piped and not re.search(r'(?:^|[\s\d"\'])<(?!\()', seg):
            return True
    return False


def check_codex_input(cmd):
    if _codex_without_input(executed_text(cmd)):
        return ('codex exec는 입력을 닫아야 한다. 안 닫으면 입력 끝을 기다리며 영원히 멈추고 완료 알림도 오지 않는다(문제 기록 W36). '
                '명령에 < /dev/null을 붙인다. 백그라운드로 돌릴 때는 시간 상한과 주기 확인도 함께 건다 (.ai/rules/art-tools.md "오래 걸리는 Codex 실행").')
    return None


def _review_out(cmd):
    if not re.search(r'(?:^|[\s;&|(/])codex\s+(?:.*\s)?exec\b', cmd):
        return None
    m = re.search(r'(?:\s-o|\s--output-last-message)(?:\s+|=)("([^"]+)"|\'([^\']+)\'|(\S+))', cmd)
    return m and (m.group(2) or m.group(3) or m.group(4))


def check_review(cmd, cwd, pol):
    cmd = executed_text(cmd)
    out = _review_out(cmd)
    if not out:
        return None
    p = Path(out) if os.path.isabs(out) else Path(cwd or ROOT) / out
    name = p.name
    # a review result is named <topic>-rNNN.md (core-rules 3); older names that say "review" without a round count too
    if not name.endswith('.md') or name.endswith('.prompt.md') or not (re.search(r'-r\d+\.md$', name) or 'review' in name):
        return None
    topic = re.sub(r'(-r\d+)?\.md$', '', name)
    pat = re.compile(re.escape(topic) + r'(-r\d+)?\.md$')
    done = [f.name for f in p.parent.glob('*.md') if pat.fullmatch(f.name) and f.name != name] if p.parent.exists() else []
    cap = pol['review']['max_rounds_per_topic']; env = pol['review']['override_env']
    if len(done) >= cap and not _has_env(cmd, env):
        return (f"같은 주제 검토 '{topic}'가 이미 {len(done)}회다(상한 {cap}: 설계 1 + 마감 1). 다시 검토하기 전에 사용자에게 묻는다. "
                f"승인되면 명령 앞에 {env}=\"<사용자 승인 내용>\"를 붙인다 ({rules()} 3절).")
    return None


TRIPO_OPEN = ('SUBMITTING', 'SUBMITTED', 'SUBMISSION_UNKNOWN')
DAY = 86400


def check_tripo_wait(cmd, pol):
    cmd = executed_text(cmd)
    limit = pol['tripo_fetch_wait_max']
    for m in re.finditer(r'\bgen\s+fetch\b[^;&|\n]*?--wait[ =](\d+)', cmd):
        if int(m.group(1)) > limit:
            return (f"gen fetch --wait는 {limit}초까지다. 대기가 끝나야 완료 알림이 오므로 길게 잡으면 응답 없는 작업을 그만큼 늦게 안다. "
                    f"--wait {limit} 이하로 두고, FETCH_PENDING이면 gen status로 확인한 뒤 fetch를 다시 부른다(추가 과금 없음). 다시 제출하지 않는다.")
    return None


def tripo_open(root, now=None):
    """[(asset, state, age seconds)] of paid tasks with no result yet, generation and anim steps. Reads journals only, no network."""
    now = now or time.time()
    out = []
    for f in sorted(Path(root).glob('output/assets/*/tripo/journal.json')):
        d = json.loads(f.read_text())
        at = ((d.get('history') or [{}])[-1]).get('at')
        try:
            t = datetime.fromisoformat(at).timestamp() if at else f.stat().st_mtime
        except ValueError:
            t = f.stat().st_mtime
        steps = [(f.parent.parent.name, d.get('state'))]
        steps += [(f'{f.parent.parent.name} anim {k}', (v or {}).get('state')) for k, v in (d.get('anim') or {}).items()]
        out += [(n, st, now - t) for n, st in steps if st in TRIPO_OPEN]
    return out


def tripo_overdue(inp, pol, state_dir, root, now=None):
    """(new overdue tasks, recent ones among them): each task is said once per step of tripo_overdue_seconds, an old one once per session"""
    limit = pol['tripo_overdue_seconds']
    sid = re.sub(r'[^A-Za-z0-9_-]', '', inp.get('session_id') or 'none')
    fresh = []
    for name, st, age in tripo_open(root, now):
        if age >= limit and _once(state_dir, f"tripo-{sid}-{re.sub(r'[^A-Za-z0-9_-]', '_', name)}.json", 0 if age >= DAY else int(age // limit)):
            fresh.append((name, st, age))
    return fresh


def tripo_message(items):
    lines = []
    for name, st, age in items:
        asset = name.split(' anim ')[0]
        if st == 'SUBMITTED':
            lines.append(f"{name}: 제출 뒤 {int(age // 60)}분째 결과 없음. `assets.py gen status {asset}`로 확인하고 `assets.py gen fetch {asset} --wait 300`을 다시 부른다(추가 과금 없음)")
        else:
            lines.append(f"{name}: {st} 상태로 {int(age // 60)}분째. 제출됐는지 모르는 상태다. 잔액·사용 내역으로 확인(`gen reconcile`)하고 사용자에게 알린다")
    return '[Tripo 확인 hook] ' + ' / '.join(lines) + '. 다시 제출하지 않는다(이중 과금). 네트워크 오류면 상태부터 본다 (.ai/rules/3d-tools.md).'


def stop(inp, pol, state_dir=STATE, root=None, now=None):
    if inp.get('stop_hook_active'):
        return None
    items = tripo_overdue(inp, pol, state_dir, root or ROOT, now)
    recent = [i for i in items if i[2] < DAY]
    return {'decision': 'block', 'reason': tripo_message(recent)} if recent else None


def prompt_hook(inp, pol, state_dir=STATE, root=None, now=None):
    out = prompt(inp, pol, state_dir, root)
    items = tripo_overdue(inp, pol, state_dir, root or ROOT, now)
    if not items:
        return out
    out = out or {}
    spec = out.setdefault('hookSpecificOutput', {'hookEventName': 'UserPromptSubmit', 'additionalContext': ''})
    spec['additionalContext'] = (spec.get('additionalContext', '') + ' ' + tripo_message(items)).strip()
    return out


def pretool(inp, pol):
    if inp.get('tool_name') != 'Bash':
        return None
    cmd = (inp.get('tool_input') or {}).get('command') or ''
    reason = check_full(cmd, pol) or check_review(cmd, inp.get('cwd'), pol) or check_no_verify(cmd) or check_codex_input(cmd) or check_tripo_wait(cmd, pol)
    if reason:
        return {'hookSpecificOutput': {'hookEventName': 'PreToolUse', 'permissionDecision': 'deny', 'permissionDecisionReason': reason}}
    return None


def context_tokens(transcript):
    """input + cache read + cache creation of the last assistant message (= what the next request re-reads)"""
    p = Path(transcript) if transcript else None
    if p is None or not p.is_file():
        return None
    with p.open('rb') as f:
        f.seek(0, 2); size = f.tell(); f.seek(max(0, size - 4_000_000)); tail = f.read().decode('utf-8', 'ignore')
    for line in reversed(tail.splitlines()):
        if '"usage"' not in line:
            continue
        try:
            d = json.loads(line)
        except Exception:
            continue
        u = (d.get('message') or {}).get('usage') if d.get('type') == 'assistant' else None
        if u:
            return u.get('input_tokens', 0) + u.get('cache_read_input_tokens', 0) + u.get('cache_creation_input_tokens', 0)
    return None


def context_level(tok, ctx):
    """thresholds: warn, new_session, then every step after new_session (250k, 300k, 400k, 500k ...); -1 below warn"""
    warn, new, stepv = ctx['warn_tokens'], ctx.get('new_session_tokens', ctx['warn_tokens']), ctx['step_tokens']
    if tok < warn:
        return -1
    if tok < new:
        return 0
    return 1 + (tok - new) // stepv


def _once(state_dir, name, level):
    sf = Path(state_dir) / name
    last = json.loads(sf.read_text()).get('level', -1) if sf.exists() else -1
    if level <= last:
        return False
    sf.parent.mkdir(parents=True, exist_ok=True); sf.write_text(json.dumps({'level': level, 'time': time.time()}))
    return True


def prompt(inp, pol, state_dir=STATE, root=None):
    sid = re.sub(r'[^A-Za-z0-9_-]', '', inp.get('session_id') or 'none')
    r = Path(root or ROOT)
    if not (r / '3dkit.config.json').is_file() and _once(state_dir, f'noconfig-{sid}.json', 0):
        return {'systemMessage': f"3dkit guard: {r}에서 3dkit.config.json을 찾지 못해 기본 정책으로 동작합니다(전체 Play 차단 없음, 검토 상한 2, 맥락 25만/30만)."}
    tok = context_tokens(inp.get('transcript_path') or '')
    ctx = pol['context']
    if tok is None or tok < ctx['warn_tokens']:
        return None
    level = context_level(tok, ctx)
    if not _once(state_dir, f'context-{sid}.json', level):
        return None
    man = round(tok / 10000)
    new = ctx.get('new_session_tokens', 1 << 62)
    core = rules(r)
    if tok >= new:
        # 047 H10: most of the cost is re-reading this context on every tool call; a big new task should start fresh
        return {'systemMessage': f"대화 맥락이 약 {man}만 토큰입니다. 새 큰 작업이면 인수인계를 쓰고 새 세션에서 시작하는 편이 쌉니다.",
                'hookSpecificOutput': {'hookEventName': 'UserPromptSubmit', 'additionalContext':
                    f"[효율 hook] 현재 대화 맥락 약 {man}만 토큰(새 세션 기준 {new // 10000}만 초과). 도구 호출마다 이 맥락을 다시 읽어 비용의 대부분이 된다. "
                    "이 요청이 새 큰 작업(새 단계, 여러 파일 구현, Play 여러 번)이면 시작하기 전에 인수인계 양식으로 현재 상태 문서(docs/현재상태.md)에 인수인계를 쓰고 "
                    f"사용자에게 새 세션을 먼저 제안한다. 짧은 질문·마무리면 그대로 답한다 ({core} 4절)."}}
    # B47: below the new-session threshold only inform; core rule 4 says not to propose a new session here
    return {'systemMessage': f"대화 맥락이 약 {man}만 토큰입니다(새 세션 기준 {new // 10000}만).",
            'hookSpecificOutput': {'hookEventName': 'UserPromptSubmit', 'additionalContext':
                f"[효율 hook] 현재 대화 맥락 약 {man}만 토큰. 새 세션 기준({new // 10000}만) 아래이므로 새 세션을 제안하지 않는다({core} 4절). "
                "단계가 끝나면 인수인계를 쓸 수 있게 결과·검증·남은 일을 정리해 둔다."}}


def selftest():
    import tempfile
    pol = FIXTURE; c = {}
    c['config_defaults'] = policy(Path(tempfile.mkdtemp()))['review']['max_rounds_per_topic'] == 2
    run = lambda cmd, cwd=str(ROOT): pretool({'tool_name': 'Bash', 'tool_input': {'command': cmd}, 'cwd': cwd}, pol)
    deny = lambda r: bool(r) and r['hookSpecificOutput']['permissionDecision'] == 'deny'
    c['no_verify_denied'] = deny(run('git commit --no-verify -m x')) and deny(run('git commit -n -m x'))
    c['no_verify_in_message_allowed'] = run('git commit -m "skip --no-verify here"') is None and run('git commit -m x') is None
    c['short_default_allowed'] = run('KEYART045_SCENE=a.unity /usr/bin/python3 Tools/Proto/keyart_045.py') is None
    c['full_no_reason_denied'] = deny(run('KEYART045_STAGE=full /usr/bin/python3 Tools/Proto/keyart_045.py'))
    c['load_no_reason_denied'] = deny(run("KEYART045_STAGE='load' /usr/bin/python3 Tools/Proto/keyart_045.py"))
    c['full_with_reason_allowed'] = run('DS_FULL_REASON="stage 3 end" KEYART045_STAGE=full /usr/bin/python3 Tools/Proto/keyart_045.py') is None
    c['empty_reason_denied'] = deny(run('DS_FULL_REASON="" KEYART045_STAGE=full /usr/bin/python3 Tools/Proto/keyart_045.py'))
    c['echo_mention_allowed'] = run('echo "KEYART045_STAGE=full Tools/Proto/keyart_045.py"') is None
    c['python_flags_full_denied'] = deny(run('KEYART045_STAGE=full /usr/bin/python3 -u /Users/x/Tools/Proto/keyart_045.py'))
    c['baseline044_no_reason_denied'] = deny(run('KEYART045_STAGE=baseline044 /usr/bin/python3 Tools/Proto/keyart_045.py'))
    c['baseline044_with_reason_allowed'] = run('DS_FULL_REASON="044 비교" KEYART045_STAGE=baseline044 /usr/bin/python3 Tools/Proto/keyart_045.py') is None
    c['old_runner_denied'] = deny(run('/usr/bin/python3 Tools/Proto/proto_main_044.py'))
    c['run_all_denied'] = deny(run('python3 Tools/Verify/run.py all --screens'))
    c['run_local_allowed'] = run('python3 Tools/Verify/run.py local') is None
    c['grep_mention_allowed'] = run('grep -n KEYART045_STAGE Tools/Harness/tier.py') is None
    c['other_bash_allowed'] = run('ls') is None and pretool({'tool_name': 'Read', 'tool_input': {}}, pol) is None
    # 047-E0 / H2: only executed positions count (heredoc bodies and quoted prose are text, not commands)
    c['heredoc_body_allowed'] = run("cat > docs/x.md <<'EOF'\nKEYART045_STAGE=full /usr/bin/python3 Tools/Proto/keyart_045.py\nEOF") is None
    c['heredoc_dash_body_allowed'] = run("cat <<-EOF > a.md\n\tpython3 Tools/Verify/run.py all\n\tEOF\necho done") is None
    c['command_after_heredoc_denied'] = deny(run("cat > a.md <<EOF\nnote\nEOF\npython3 Tools/Verify/run.py all"))
    c['quoted_prose_allowed'] = run('git commit -m "later: /usr/bin/python3 Tools/Verify/run.py all --screens"') is None
    c['quoted_path_denied'] = deny(run('KEYART045_STAGE=full /usr/bin/python3 "Tools/Proto/keyart_045.py"'))
    c['shell_c_denied'] = deny(run('bash -c "python3 Tools/Verify/run.py all"'))
    c['reason_with_spaces_allowed'] = run('DS_FULL_REASON="stage 4 end, commit" python3 Tools/Verify/run.py all --screens') is None
    # review 048 r001 #1
    c['shell_lc_denied'] = deny(run("bash -lc 'python3 Tools/Verify/run.py all'"))
    c['abs_shell_c_denied'] = deny(run("/bin/zsh -c 'python3 Tools/Verify/run.py all'"))
    c['subst_in_quotes_denied'] = deny(run('echo "$(python3 Tools/Verify/run.py all)"'))
    c['backtick_in_quotes_denied'] = deny(run('echo "result: `python3 Tools/Verify/run.py all`"'))
    c['heredoc_to_shell_denied'] = deny(run("bash <<'EOF'\npython3 Tools/Verify/run.py all\nEOF"))
    c['heredoc_to_cat_allowed'] = run("cat <<'EOF' | tee notes.md\npython3 Tools/Verify/run.py all\nEOF") is None
    c['prose_inside_shell_c_allowed'] = run("bash -c 'echo \"python3 Tools/Verify/run.py all\"'") is None
    # W36: codex exec with input left open waits forever; the first case is the exact shape that stalled for an hour
    c['codex_w36_shape_denied'] = deny(run('''cd o && perl -e 'alarm shift; exec @ARGV' 1500 codex exec -s workspace-write -C "$PWD" "$(cat p.md)" > r.log 2>&1'''))
    c['codex_plain_denied'] = deny(run('codex exec "draw a boss"'))
    c['codex_flag_before_exec_denied'] = deny(run('codex -a never exec "draw a boss"'))
    c['codex_background_denied'] = deny(run('codex exec "draw" > r.log 2>&1 &'))
    c['codex_shell_c_denied'] = deny(run('bash -c "codex exec draw"'))
    c['codex_prose_is_not_input_denied'] = deny(run('codex exec "read < /dev/null in prose"'))
    c['codex_subst_input_is_not_ours_denied'] = deny(run('codex exec "$(cat < p.md)"'))
    c['codex_inside_subst_denied'] = deny(run('x="$(codex exec draw)"'))
    c['codex_second_command_denied'] = deny(run('codex exec a < /dev/null; codex exec b'))
    c['codex_after_redirected_cat_denied'] = deny(run('cat < p.md; codex exec draw'))
    c['codex_closed_allowed'] = run('codex exec "draw" < /dev/null') is None and run('codex exec "draw" </dev/null > r.log 2>&1') is None
    c['codex_w36_fixed_allowed'] = run('''perl -e 'alarm shift; exec @ARGV' 1500 codex exec -C "$PWD" "$(cat p.md)" </dev/null > r.log 2>&1''') is None
    c['codex_pipe_allowed'] = run('cat p.md | codex exec -') is None
    c['codex_heredoc_allowed'] = run("codex exec - <<'EOF'\ndraw a boss\nEOF") is None
    c['codex_help_allowed'] = run('codex exec --help') is None
    c['codex_mentions_allowed'] = all(run(x) is None for x in ('grep -n "codex exec" a.md', 'echo codex exec', 'git commit -m "fix codex exec input"', 'which codex', 'codex --version', "cat > a.md <<'EOF'\ncodex exec draw\nEOF", 'ps -eo command | grep "[c]odex exec"'))
    c['tripo_wait_short_allowed'] = run('python3 .ai/3d-art/tools/assets.py gen fetch a --wait 600') is None and run('python3 assets.py gen fetch a') is None
    c['tripo_wait_limit_allowed'] = run('python3 assets.py gen fetch a --wait 900') is None
    c['tripo_wait_long_denied'] = deny(run('python3 assets.py gen fetch a --wait 3600')) and deny(run('python3 assets.py gen fetch a --wait=901'))
    c['tripo_wait_mention_allowed'] = run('git commit -m "gen fetch --wait 3600 was too long"') is None and run('sleep 3600') is None
    with tempfile.TemporaryDirectory() as td:
        rt = Path(td)
        def job(name, state, age, **kw):
            f = rt / 'output/assets' / name / 'tripo/journal.json'; f.parent.mkdir(parents=True, exist_ok=True)
            f.write_text(json.dumps(dict(state=state, history=[dict(at=datetime.fromtimestamp(NOW - age).astimezone().isoformat(), state=state)], **kw)))
        NOW = time.time(); sd = rt / 's'; ST = lambda sid='a', active=False, now=NOW: stop({'session_id': sid, 'stop_hook_active': active}, pol, sd, rt, now)
        c['tripo_no_journal_silent'] = ST() is None
        job('young', 'SUBMITTED', 300); c['tripo_young_silent'] = ST() is None
        job('done', 'DOWNLOADED', 9000); c['tripo_done_silent'] = ST() is None
        job('late', 'SUBMITTED', 1000); r = ST()
        c['tripo_overdue_blocks_stop'] = bool(r) and r['decision'] == 'block' and 'gen status late' in r['reason'] and 'gen fetch late --wait 300' in r['reason']
        c['tripo_never_says_submit'] = bool(r) and 'gen submit' not in r['reason'] and '다시 제출하지 않는다' in r['reason']
        c['tripo_same_step_once'] = ST() is None
        c['tripo_stop_active_no_loop'] = ST('b', True) is None
        c['tripo_next_step_again'] = ST(now=NOW + 900) is not None
        job('late', 'DOWNLOADED', 0); job('young', 'DOWNLOADED', 0)
        job('unk', 'SUBMISSION_UNKNOWN', 2000); r = ST('c')
        c['tripo_unknown_says_reconcile'] = bool(r) and 'gen reconcile' in r['reason'] and 'gen fetch' not in r['reason']
        job('unk', 'DOWNLOADED', 0)
        job('old', 'SUBMISSION_UNKNOWN', 3 * DAY)
        c['tripo_old_does_not_block'] = ST('d') is None
        pp = lambda sid: prompt_hook({'session_id': sid, 'transcript_path': ''}, pol, sd, rt, NOW)
        (rt / '3dkit.config.json').write_text('{}')
        r = pp('e'); c['tripo_old_said_once_on_prompt'] = bool(r) and 'old' in r['hookSpecificOutput']['additionalContext'] and pp('e') is None
        job('old', 'DOWNLOADED', 0)
        job('anim', 'DOWNLOADED', 5000, anim={'rig': {'state': 'SUBMITTED'}}); r = ST('f')
        c['tripo_anim_step_seen'] = bool(r) and 'anim rig' in r['reason'] and 'gen status anim' in r['reason']
    with tempfile.TemporaryDirectory() as td:
        d = Path(td) / 'r003'; d.mkdir()
        rv = lambda out, extra='': run(f'{extra}codex exec -s read-only -C . -o {out} "review" < /dev/null', td)
        c['review_first_allowed'] = rv('r003/codex-impl-review-x-r001.md') is None
        (d / 'codex-impl-review-x-r001.md').write_text('x'); (d / 'codex-impl-review-x-r001.prompt.md').write_text('x')
        c['review_second_allowed'] = rv('r003/codex-impl-review-x-r002.md') is None
        (d / 'codex-impl-review-x-r002.md').write_text('x')
        c['review_third_denied'] = deny(rv('r003/codex-impl-review-x-r003.md'))
        c['review_third_user_ok'] = rv('r003/codex-impl-review-x-r003.md', 'DS_REVIEW_OK="사용자 승인 11-02" ') is None
        c['other_topic_allowed'] = rv('r003/codex-design-review-x-r001.md') is None
        c['fix_check_allowed'] = rv('r003/codex-impl-review-x-fix-r001.md') is None      # core-rules 3: fix check is its own topic
        c['abs_path_counted'] = deny(rv(str(d / 'codex-impl-review-x-r003.md')))
        c['codex_not_review_allowed'] = rv('r003/concept-image.md') is None
        # topic name without the word "review" (core-rules 3: <topic>-rNNN.md)
        (d / 'harness-r001.md').write_text('x'); (d / 'harness-r002.md').write_text('x')
        c['plain_topic_third_denied'] = deny(rv('r003/harness-r003.md'))
        c['plain_topic_other_allowed'] = rv('r003/proto-r001.md') is None
        # prompt: context size from a fake transcript
        tr = Path(td) / 't.jsonl'; sd = Path(td) / 'state'
        line = lambda n: json.dumps({'type': 'assistant', 'message': {'usage': {'input_tokens': 10, 'cache_read_input_tokens': n, 'cache_creation_input_tokens': 0}}})
        tr.write_text(line(100000) + '\n'); inp = {'transcript_path': str(tr), 'session_id': 's1'}
        proj = Path(td) / 'proj'; proj.mkdir(); (proj / '3dkit.config.json').write_text('{}')
        P = lambda i: prompt(i, pol, sd, proj)
        c['small_context_silent'] = P(inp) is None
        tr.write_text(line(100000) + '\n' + line(260000) + '\n')
        r1 = P(inp); c['big_context_warns_once'] = bool(r1 and 'systemMessage' in r1) and P(inp) is None
        c['warn_level_does_not_propose'] = bool(r1) and '제안하지 않는다' in r1['hookSpecificOutput']['additionalContext'] and '새 세션을 먼저' not in r1['hookSpecificOutput']['additionalContext']
        tr.write_text(line(310000) + '\n'); r2 = P(inp)
        c['new_session_level_warns'] = bool(r2) and '새 세션을 먼저' in r2['hookSpecificOutput']['additionalContext']
        tr.write_text(line(370000) + '\n'); c['same_level_silent'] = P(inp) is None
        tr.write_text(line(410000) + '\n'); c['next_step_warns_again'] = P(inp) is not None
        c['levels'] = [context_level(t, pol['context']) for t in (100000, 260000, 299999, 300000, 399999, 400000, 520000)] == [-1, 0, 0, 1, 1, 2, 3]
        c['missing_transcript_silent'] = P({'transcript_path': str(Path(td) / 'no.jsonl')}) is None
        bare = Path(td) / 'bare'; bare.mkdir(); nc = {'transcript_path': '', 'session_id': 's2'}
        n1 = prompt(nc, pol, sd, bare); c['no_config_said_once'] = bool(n1 and 'config.json' in n1['systemMessage']) and prompt(nc, pol, sd, bare) is None
        c['rules_path_installed_first'] = rules(proj) == '.ai/rules/art-core-rules.md'
    ok = all(c.values())
    print(json.dumps({'status': 'PASS' if ok else 'FAIL', 'cases': c}, ensure_ascii=False))
    return ok


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else ''
    if mode == 'selftest':
        sys.exit(0 if selftest() else 1)
    try:
        inp = json.loads(sys.stdin.read() or '{}'); pol = policy()
        out = pretool(inp, pol) if mode == 'pretool' else prompt_hook(inp, pol) if mode == 'prompt' else stop(inp, pol) if mode == 'stop' else None
        if out:
            print(json.dumps(out, ensure_ascii=False))
    except Exception as e:   # fail-open: an efficiency guard must never block work by its own bug
        try:
            STATE.mkdir(parents=True, exist_ok=True)
            with (STATE / 'errors.log').open('a') as f:
                f.write(f"{time.strftime('%Y-%m-%d %H:%M:%S')} {mode} {type(e).__name__}: {str(e)[:300]}\n")
        except Exception:
            pass
    sys.exit(0)


if __name__ == '__main__':
    main()
