#!/usr/bin/env python3
"""Legacy entry point: the open Unity project's Assets is now the only game source."""
import argparse
import json
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'Tools'))
from project_paths import ROOT, APPROVED_PROJECT, require_project

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply',action='store_true')
    parser.add_argument('--baseline',type=Path)
    args=parser.parse_args()
    if args.apply or args.baseline:
        parser.error('Legacy copying is retired. Edit Assets/DeadSurvivor in the canonical Unity/Git project.')
    require_project(ROOT,APPROVED_PROJECT)
    print(json.dumps(dict(mode='canonical-project',project=str(ROOT),source='Assets/DeadSurvivor',copying=False),ensure_ascii=False))
if __name__=='__main__':main()
