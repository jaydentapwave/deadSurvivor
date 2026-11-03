"""3dkit -> Unity: write <work_root>/<Entry>-request.json, call the package's Kit3D.AssetPipeline.Run through the Unity
CLI (`unity command script eval`), read <Entry>-result.json. The Editor must be open on this project. The C# side refuses
another project or Play mode (WRONG_TARGET_OR_PLAYING)."""
import json
import re
import subprocess


def run(kit, entry, request, timeout=900):
    kit.work.mkdir(parents=True, exist_ok=True)
    req_path, res_path = kit.work / f'{entry}-request.json', kit.work / f'{entry}-result.json'
    request = dict(request, project_root=str(kit.root), out_dir=str(kit.work))
    req_path.write_text(json.dumps(request, ensure_ascii=False, indent=1) + '\n')
    res_path.unlink(missing_ok=True)
    code = f'return Kit3D.AssetPipeline.Run({json.dumps(entry)}, {json.dumps(str(req_path))});'
    p = subprocess.run(['unity', 'command', 'script', 'eval', '--project-path', str(kit.root), '--format', 'json',
                        '--timeout', str(timeout), '--', code], cwd=kit.root, capture_output=True, text=True, timeout=timeout + 60)
    if not res_path.exists():
        text = p.stdout + p.stderr
        try:
            d = json.loads(p.stdout)
            r = (d.get('data') or {}).get('result') or {}
            msg = json.dumps(r.get('diagnostics') or r.get('output') or r.get('result') or d.get('errors'), ensure_ascii=False)[:800]
        except (json.JSONDecodeError, AttributeError):
            m = re.search(r'(Exception|error)[^\n]{0,400}', text)
            msg = m.group(0) if m else text[-600:]
        raise SystemExit(f'UNITY_{entry.upper()}_FAILED: {msg}')
    return json.loads(res_path.read_text())
