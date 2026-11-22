"""Run unchanged project tools with task-local records. No game files are written."""
from pathlib import Path
import sys, runpy, json
root = Path.cwd()
tools = root / '.ai/2d-art/tools'
sys.path.insert(0, str(tools))
import kit2d
cfg = json.loads((root / '.ai/2d-art/config.json').read_text())
cfg['work_dir'] = 'output/hd2d-dq/codex-sprites/measure-records'
kit = kit2d.Kit(root, cfg)
kit2d.load_kit = lambda start=None: kit
name = sys.argv.pop(1)
sys.argv[0] = str(tools / name)
runpy.run_path(str(tools / name), run_name='__main__')
