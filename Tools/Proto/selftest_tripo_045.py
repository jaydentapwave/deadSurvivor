#!/usr/bin/env python3
"""Superseded (050): the offline counterexamples now test Tools/Art/asset_tripo.py. Runs Tools/Art/selftest_asset_tripo.py."""
import runpy
from pathlib import Path
runpy.run_path(str(Path(__file__).resolve().parents[1] / 'Art/selftest_asset_tripo.py'), run_name='__main__')
