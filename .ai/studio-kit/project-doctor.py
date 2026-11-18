#!/usr/bin/env python3
"""Read-only project readiness audit. Exit 0: configured, 1: incomplete, 2: audit error.

This checks a real project, not the kit templates or actual hook execution.
Standard library only. YAML and schema checking are bundled: ./lib when installed in a project
(.ai/studio-kit/lib), director-handoff/lib inside the kit.
"""
import argparse
import json
import shutil
import sys
from pathlib import Path

_LIB = Path(__file__).resolve().with_name('lib')
if not _LIB.is_dir():
    _LIB = Path(__file__).resolve().parents[2] / 'director-handoff' / 'lib'
sys.path.insert(0, str(_LIB))  # bundled yaml, minischema
import minischema  # noqa: E402
import yaml  # noqa: E402

SKIP = {'work', 'Library', 'Temp', 'Logs', 'Builds', 'node_modules', 'tools', 'vendor', 'Assets', 'Packages', 'ProjectSettings', 'UserSettings', 'versions'}
BASE_HOOKS = {
    'PreToolUse': [('Bash', 'block-dangerous-git.py'), ('Bash', 'guard-worktree-remove.py'),
                   ('Write|Edit|MultiEdit|NotebookEdit', 'guard-reviewer-writes.py')],
    'UserPromptSubmit': [('', 'inject-context-rules.py'), ('', 'inject-knowledge-notes.py')],
    'Stop': [('', 'require-director-handoff.py')],
    'SubagentStop': [('', 'log-agent-verdict.py')],
}


def audit(root, host='claude', required=()):
    root = Path(root).resolve()
    if not root.is_dir():
        raise ValueError(f'Project directory does not exist: {root}')
    findings = []

    def issue(code, path, message):
        findings.append({'code': code, 'path': str(path), 'message': message})

    def local(value):
        path = (root / value).resolve()
        if not path.is_relative_to(root):
            raise ValueError(f'Path escapes project root: {value}')
        return path

    def need(value):
        if not local(value).is_file():
            issue('missing-file', value, 'Required project file is missing')

    capfile = root / '.director/capabilities.yaml'
    caps = []
    if not capfile.is_file():
        issue('missing-capabilities', '.director/capabilities.yaml', 'Run the applicable setup')
    else:
        data = yaml.safe_load(capfile.read_text(encoding='utf-8'))
        schema = Path(__file__).with_name('capabilities.schema.json')
        if not schema.exists():
            schema = Path(__file__).resolve().parents[2] / 'director-handoff/schemas/capabilities.schema.json'
        minischema.validate(data, json.loads(schema.read_text(encoding='utf-8')))
        caps = data['capabilities']
    ids = [cap['id'] for cap in caps]
    if len(ids) != len(set(ids)):
        issue('duplicate-capability', str(capfile), 'Capability ids must be unique')
    for cid in required:
        if cid not in ids:
            issue('missing-capability', cid, 'Requested stage has not been configured')

    # Bounded discovery, including a non-git workspace root. Never follow symlinks
    # or crawl copied kits, caches, archives and generated analysis artifacts.
    discovered = []

    def scan(folder, depth):
        if (folder / '.claude-plugin').exists() or (folder / 'plugins/studio-kit').exists():
            return
        kind = None
        if (folder / 'ProjectSettings/ProjectVersion.txt').is_file():
            kind = 'unity-client.implement'
        elif (folder / 'firebase.json').is_file():
            kind = 'server.implement'
        elif (folder / 'rea.py').is_file() and (folder / 'game/game.json').is_file():
            kind = 'game-analysis.analyze'
        if kind:
            rel = folder.relative_to(root).as_posix()
            discovered.append({'capability': kind, 'path': rel})
            if not any(c['id'] == kind and local(c.get('path', '.')) == folder for c in caps):
                issue('unregistered-area', rel, f'{kind} exists but is not registered at this path')
            if kind == 'game-analysis.analyze':
                return
        if depth < 2:
            for child in sorted(folder.iterdir()):
                if child.is_dir() and not child.is_symlink() and not child.name.startswith('.') and child.name not in SKIP:
                    scan(child, depth + 1)
    scan(root, 0)

    settings_path = root / '.claude/settings.json'
    settings = json.loads(settings_path.read_text(encoding='utf-8')) if settings_path.exists() else {}
    if not isinstance(settings, dict) or not isinstance(settings.get('hooks', {}), dict):
        raise ValueError('settings.json and hooks must be objects')
    hooks = settings.get('hooks', {})
    if settings.get('disableAllHooks'):
        issue('hooks-disabled', '.claude/settings.json', 'Project hooks are disabled')
    for groups in hooks.values():
        if not isinstance(groups, list) or any(not isinstance(g, dict) or
                not isinstance(g.get('hooks', []), list) or
                any(not isinstance(h, dict) for h in g.get('hooks', [])) for g in groups):
            raise ValueError('Invalid hook groups in settings.json')

    def hook(event, matcher, script):
        groups = hooks.get(event, [])
        relative = script if '/' in script else '.claude/hooks/' + script
        command = f'python3 -B "$CLAUDE_PROJECT_DIR/{relative}"'
        found = any(g.get('matcher', '') == matcher and any(
            h.get('type') == 'command' and h.get('command', '') == command
            for h in g.get('hooks', [])) for g in groups)
        if not found:
            issue('missing-hook', script, f'Missing {event} registration (matcher={matcher!r})')

    for cap in caps:
        area = cap.get('path', '.')
        # 기획 능력은 문서 폴더가 아직 없어도 된다(첫 문서를 쓸 때 생긴다)
        if not local(area).is_dir() and cap['id'] not in ('design.write', 'game.direction', 'game.spec'):
            issue('missing-area', area, 'Registered area does not exist')
        for rule in cap.get('rules', []):
            need(rule)
        for gate in cap.get('gates', []):
            if gate.get('agent'):
                need(f".claude/agents/{gate['agent']}.md")
        if cap['id'] == 'unity-client.implement':
            for file in ['CLAUDE.md', '.ai/rules/test-before-claim.md', '.ai/rules/architecture.md']:
                need(str(Path(area) / file))
            for name in ['validator-client', 'algorithm-tester']:
                need(f'.claude/agents/{name}.md')
            need('.claude/hooks/verify-on-new-file.py')
            hook('PreToolUse', 'Write', 'verify-on-new-file.py')
        if cap['id'] == 'server.implement':
            need(str(Path(area) / 'CLAUDE.md'))
            need('.claude/agents/validator-server.md')
        if cap['id'] == 'game-analysis.analyze':
            for file in ['AGENTS.md', 'CLAUDE.md', 'rea.py', 'game/game.json',
                         'tools/hooks/rea_source_guard.py', 'tools/hooks/rea_source_reminder.py']:
                need(str(Path(area) / file))
            prefix = '' if area in ('', '.') else area.rstrip('/') + '/'
            hook('UserPromptSubmit', '', f'{prefix}tools/hooks/rea_source_reminder.py')
            hook('PreToolUse', 'Write|Edit|MultiEdit|mcp__Claude_Docs__update|mcp__Claude_Docs__batch|mcp__Claude_Docs__create',
                 f'{prefix}tools/hooks/rea_source_guard.py')

    base = any(cid in ids for cid in ['design.write', 'unity-client.implement', 'server.implement'])
    if base:
        if 'design.write' not in ids:
            issue('missing-capability', 'design.write', 'Implementation requires the common design stage')
        for file in ['CLAUDE.md', '.ai/rules/git-workflow.md', '.ai/inject.tsv',
                     '.claude/agents/spec-reviewer.md', '.claude/hooks/hooklib.py']:
            need(file)
        for event, entries in BASE_HOOKS.items():
            for matcher, script in entries:
                need('.claude/hooks/' + script)
                hook(event, matcher, script)

    need('.ai/studio-kit/project-doctor.py')
    need('.ai/studio-kit/capabilities.schema.json')
    entry = 'AGENTS.md' if host == 'codex' else 'CLAUDE.md'
    need(entry)
    if local(entry).is_file() and '<!-- studio-kit:project-start -->' not in local(entry).read_text(encoding='utf-8'):
        issue('missing-entry-guide', entry, 'Project preflight instructions have not been installed')
    notes = [
        'Configuration only: this does not prove skill discovery, hook execution, reviews, compilation or gameplay.',
        'Discovery covers root and two directory levels; excluded/deeper areas must be registered explicitly.',
    ]
    if host == 'codex':
        notes.append('Claude hooks are not Codex enforcement. Run preflight and stage verification explicitly; unavailable reviews remain not-run.')
    state_path = root / '.ai/studio-kit/install-state.json'
    installed = json.loads(state_path.read_text()) if state_path.exists() else None
    if installed is None:
        notes.append('No installation baseline; re-run setup to compare existing files without overwriting them.')
    return {'status': 'incomplete' if findings else 'configured', 'root': str(root), 'host': host,
            'capabilities': ids, 'discovered': discovered, 'findings': findings, 'notes': notes,
            'execution': {'automatic_runner': 'claude', 'runner_available': bool(shutil.which('claude')),
                          'native_automatic_runner_supported': host == 'claude',
                          'claude_hooks_applicable': host == 'claude', 'hook_execution_verified': False},
            'last_setup': installed}


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--root', required=True, type=Path)
    ap.add_argument('--host', choices=['claude', 'codex'], default='claude')
    ap.add_argument('--require', action='append', default=[], help='Capability required for the next stage; repeatable')
    args = ap.parse_args()
    try:
        result = audit(args.root, args.host, args.require)
    except (yaml.YAMLError, minischema.ValidationError, minischema.SchemaError) as exc:
        # YAML/schema failures must never be reported as a configured project.
        result = {'status': 'error', 'error': f'{type(exc).__name__}: {exc}'}
    except (OSError, ValueError) as exc:
        result = {'status': 'error', 'error': str(exc)}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return {'configured': 0, 'incomplete': 1, 'error': 2}[result['status']]


if __name__ == '__main__':
    raise SystemExit(main())
