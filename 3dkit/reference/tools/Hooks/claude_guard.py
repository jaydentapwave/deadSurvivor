#!/usr/bin/env python3
"""Project Claude Code hooks: efficiency guards (docs/workflows/core-rules.md). Policy: Tools/Harness/tier-policy.json.

  pretool  (PreToolUse, Bash)
    1. a full Play run (policy full_runners) without DS_FULL_REASON=... in the command -> deny with a one-line reason
    2. a Codex review (`codex exec ... -o <dir>/<topic>-rNNN.md`) when <topic> already has max_rounds_per_topic results
       in that folder, without DS_REVIEW_OK=... -> deny (ask the user first)
  prompt   (UserPromptSubmit)
    when the conversation context passes warn_tokens (then every step_tokens), tell the user once and remind the agent
    to write a stage handoff and propose a new session.

Fail-open: any internal error allows the action (errors go to output/hook-state/errors.log). Never re-runs anything,
never loops: one short message per decision.
Selftest: /usr/bin/python3 Tools/Hooks/claude_guard.py selftest"""
import json, os, re, sys, time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STATE = ROOT / 'output/hook-state'


def policy():
    return json.loads((ROOT / 'Tools/Harness/tier-policy.json').read_text())


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
                    f"마감 실행이면 명령 앞에 {env}=\"<이유>\"를 붙인다 (docs/workflows/core-rules.md).")
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
                f"승인되면 명령 앞에 {env}=\"<사용자 승인 내용>\"를 붙인다 (docs/workflows/core-rules.md).")
    return None


def pretool(inp, pol):
    if inp.get('tool_name') != 'Bash':
        return None
    cmd = (inp.get('tool_input') or {}).get('command') or ''
    reason = check_full(cmd, pol) or check_review(cmd, inp.get('cwd'), pol)
    if reason:
        return {'hookSpecificOutput': {'hookEventName': 'PreToolUse', 'permissionDecision': 'deny', 'permissionDecisionReason': reason}}
    return None


def context_tokens(transcript):
    """input + cache read + cache creation of the last assistant message (= what the next request re-reads)"""
    p = Path(transcript)
    if not p.exists():
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


def prompt(inp, pol, state_dir=STATE):
    tok = context_tokens(inp.get('transcript_path') or '')
    ctx = pol['context']
    if tok is None or tok < ctx['warn_tokens']:
        return None
    level = context_level(tok, ctx)
    sf = Path(state_dir) / f"context-{re.sub(r'[^A-Za-z0-9_-]', '', inp.get('session_id') or 'none')}.json"
    last = json.loads(sf.read_text()).get('level', -1) if sf.exists() else -1
    if level <= last:
        return None
    sf.parent.mkdir(parents=True, exist_ok=True); sf.write_text(json.dumps({'level': level, 'tokens': tok, 'time': time.time()}))
    man = round(tok / 10000)
    if tok >= ctx.get('new_session_tokens', 1 << 62):
        # 047 H10: most of the cost is re-reading this context on every tool call; a big new task should start fresh
        return {'systemMessage': f"대화 맥락이 약 {man}만 토큰입니다. 새 큰 작업이면 인수인계를 쓰고 새 세션에서 시작하는 편이 쌉니다.",
                'hookSpecificOutput': {'hookEventName': 'UserPromptSubmit', 'additionalContext':
                    f"[효율 hook] 현재 대화 맥락 약 {man}만 토큰(새 세션 기준 {ctx['new_session_tokens'] // 10000}만 초과). 도구 호출마다 이 맥락을 다시 읽어 비용의 대부분이 된다. "
                    "이 요청이 새 큰 작업(새 단계, 여러 파일 구현, Play 여러 번)이면 시작하기 전에 docs/templates/handoff.md로 현재상태.md에 인수인계를 쓰고 "
                    "사용자에게 새 세션을 먼저 제안한다. 짧은 질문·마무리면 그대로 답한다 (docs/workflows/core-rules.md 4절)."}}
    return {'systemMessage': f"대화 맥락이 약 {man}만 토큰입니다. 응답마다 이만큼 다시 읽습니다. 단계가 끝났으면 인수인계 후 새 세션을 권장합니다.",
            'hookSpecificOutput': {'hookEventName': 'UserPromptSubmit', 'additionalContext':
                f"[효율 hook] 현재 대화 맥락 약 {man}만 토큰. 지금 단계가 끝났거나 끝나 가면 docs/templates/handoff.md로 현재상태.md에 인수인계를 쓰고 사용자에게 새 세션을 제안한다 (docs/workflows/core-rules.md)."}}


def selftest():
    import tempfile
    pol = policy(); c = {}
    run = lambda cmd, cwd=str(ROOT): pretool({'tool_name': 'Bash', 'tool_input': {'command': cmd}, 'cwd': cwd}, pol)
    deny = lambda r: bool(r) and r['hookSpecificOutput']['permissionDecision'] == 'deny'
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
        c['small_context_silent'] = prompt(inp, pol, sd) is None
        tr.write_text(line(100000) + '\n' + line(260000) + '\n')
        r1 = prompt(inp, pol, sd); c['big_context_warns_once'] = bool(r1 and 'systemMessage' in r1) and prompt(inp, pol, sd) is None
        tr.write_text(line(310000) + '\n'); r2 = prompt(inp, pol, sd)
        c['new_session_level_warns'] = bool(r2) and '새 세션을 먼저' in r2['hookSpecificOutput']['additionalContext']
        tr.write_text(line(370000) + '\n'); c['same_level_silent'] = prompt(inp, pol, sd) is None
        tr.write_text(line(410000) + '\n'); c['next_step_warns_again'] = prompt(inp, pol, sd) is not None
        c['levels'] = [context_level(t, pol['context']) for t in (100000, 260000, 299999, 300000, 399999, 400000, 520000)] == [-1, 0, 0, 1, 1, 2, 3]
        c['missing_transcript_silent'] = prompt({'transcript_path': str(Path(td) / 'no.jsonl')}, pol, sd) is None
    ok = all(c.values())
    print(json.dumps({'status': 'PASS' if ok else 'FAIL', 'cases': c}, ensure_ascii=False))
    return ok


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else ''
    if mode == 'selftest':
        sys.exit(0 if selftest() else 1)
    try:
        inp = json.loads(sys.stdin.read() or '{}'); pol = policy()
        out = pretool(inp, pol) if mode == 'pretool' else prompt(inp, pol) if mode == 'prompt' else None
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
