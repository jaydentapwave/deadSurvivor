#!/usr/bin/env python3
"""Compare personal Unity code/data; apply only against verified baseline hashes."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

ROOT=Path(__file__).resolve().parents[1]
PROJECT=Path('/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor')
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply',action='store_true')
    parser.add_argument('--baseline',type=Path)
    args=parser.parse_args()
    destination=PROJECT/'Assets/DeadSurvivor'
    mappings=[]
    for folder in ('Runtime','Editor'):
        sources=list((ROOT/'UnityMigration'/folder).rglob('*.cs'))
        source_names={p.relative_to(ROOT/'UnityMigration'/folder) for p in sources}
        target_names={p.relative_to(destination/folder) for p in (destination/folder).rglob('*.cs')}
        if target_names-source_names:raise RuntimeError('Destination-only C# files; resolve ownership first: '+str(target_names-source_names))
        mappings += [(p,destination/folder/p.relative_to(ROOT/'UnityMigration'/folder)) for p in sources]
    mappings.append((ROOT/'UnityMigration/Data/PrototypeData.json',destination/'Resources/DeadSurvivor/PrototypeData.json'))
    changed=[(a,b) for a,b in mappings if digest(a)!=digest(b)]
    if args.apply:
        if not args.baseline:parser.error('--apply requires --baseline summary.json')
        baseline=json.loads(args.baseline.read_text())['sha256']
        # Validate the entire target set before any write, including external deletions.
        for source,target in mappings:
            key=str(source.relative_to(ROOT));expected=baseline.get(key)
            if digest(target)!=expected:raise RuntimeError('Destination changed since baseline: '+str(target))
        for source,target in changed:
            target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
        for source,target in mappings:
            if digest(source)!=digest(target):raise RuntimeError('Post-copy mismatch: '+str(target))
    print(json.dumps({'mode':'apply' if args.apply else 'compare','changed':[str(a.relative_to(ROOT)) for a,b in changed]},ensure_ascii=False,indent=2))
if __name__=='__main__':main()
