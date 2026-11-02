#!/usr/bin/env python3
"""045: kept for references in the 045 records. The short Play check now lives in the runner as its default tier
(Tools/Proto/keyart_045.py, KEYART045_STAGE=short, docs/workflows/core-rules.md); this wrapper runs it for the s3b scene.
Usage: /usr/bin/python3 Tools/Proto/quick_squads_045.py"""
import os, subprocess, sys
from pathlib import Path

env = dict(os.environ, KEYART045_STAGE='short')
env.setdefault('KEYART045_SCENE', 'Assets/DeadSurvivor/Scenes/Proto045/Proto045_Main_s3b.unity')
sys.exit(subprocess.call([sys.executable, str(Path(__file__).resolve().parent / 'keyart_045.py')], env=env))
