#!/usr/bin/env python3
"""Superseded (050, then 051): the offline Tripo counterexamples now test 3dkit/tools/kitlib/tripo.py. Runs 3dkit/tools/selftest_tripo.py."""
import runpy
from pathlib import Path
runpy.run_path(str(Path(__file__).resolve().parents[2] / '3dkit/tools/selftest_tripo.py'), run_name='__main__')
