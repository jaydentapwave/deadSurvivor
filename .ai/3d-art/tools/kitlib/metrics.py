"""3dkit: how well the agent produced assets, from the files the pipeline already keeps (catalog + Tripo journals + the
project's problem log). The harness replay (tools/replay.py) measures the tools; this measures the work.

  assets.py metrics            table + totals
  assets.py metrics --record   also append one JSON line to harness.metrics (default docs/art/metrics.jsonl) to see the trend

Numbers (per family = an asset and its versions X, X_r2 ...):
  paid_attempts       generations that reached the provider (journal state SUBMITTING/SUBMITTED/UNKNOWN/DOWNLOADED)
  credits             provider credits of the family (generation + paid anim steps)
  first_try           the adopted version was the family's first paid attempt
  quality_warnings    downloads whose quality check warned (kitlib/tripo.py quality)
Totals add: adopted assets, 0-credit adopted assets (built without Tripo), first-try adoption rate, credits per adopted Tripo
asset, and - when harness.problem_log points at a table whose first column is an id like U12 - how many issues the user found
(U), and how many are still open."""
import json
import re
import subprocess
import time
from pathlib import Path

from . import tripo as TR

PAID = ('SUBMITTING', 'SUBMITTED', 'SUBMISSION_UNKNOWN', 'DOWNLOADED')


def families(kit):
    TR.setup(kit)
    fam = {}
    work = kit.root / TR.TRIPO.get('new_work_root', 'output/assets')
    seen = set()
    for a in TR.SPECS:
        seen.add(TR.adir(a).resolve())
        fam.setdefault(TR.base_name(a), []).append(TR.adir(a))
    for f in work.glob('*/tripo/journal.json'):
        if f.parent.resolve() not in seen:
            fam.setdefault(TR.base_name(f.parents[1].name), []).append(f.parent)
    return fam


def family_rows(kit):
    fam = families(kit)
    by_tripo = {a['tripo']['asset']: a for a in kit.cat['assets'] if a.get('tripo')}
    rows = []
    for base, dirs in sorted(fam.items()):
        paid, credits, qwarn, versions = 0, 0, 0, []
        for d in dirs:
            jf = Path(d) / 'journal.json'
            j = json.loads(jf.read_text()) if jf.is_file() else {}
            if j.get('state') in PAID:
                paid += 1
                versions.append(Path(d).parent.name if Path(d).name == 'tripo' else Path(d).name)
            credits += TR.cost(j) + sum(TR.cost(v) for v in (j.get('anim') or {}).values())
            qwarn += bool((j.get('quality') or {}).get('warnings'))
        adopted = [n for n, a in by_tripo.items() if TR.base_name(n) == base and a.get('status') == 'adopted']
        first = sorted(versions, key=lambda n: int((re.search(r'_r(\d+)$', n) or [0, 1])[1]))[:1]
        rows.append(dict(family=base, paid_attempts=paid, credits=credits, adopted=adopted, quality_warnings=qwarn,
                         first_try=bool(adopted) and bool(first) and adopted[0] == first[0]))
    return rows


def problem_counts(kit):
    p = (kit.data.get('harness') or {}).get('problem_log')
    if not p or not (kit.root / p).is_file():
        return None
    out = {}
    for line in (kit.root / p).read_text().splitlines():
        m = re.match(r'^\|\s*([A-Z])(\d+)\s*\|', line)
        if m:
            k = m[1]
            out.setdefault(k, dict(total=0, open=0))
            out[k]['total'] += 1
            out[k]['open'] += bool(re.search(r'\|\s*(남음|확인 필요)\s*\|?\s*$', line))
    return out


def summary(kit):
    rows = family_rows(kit)
    assets = kit.cat['assets']
    adopted = [a for a in assets if a.get('status') == 'adopted']
    tripo_adopted = [r for r in rows if r['adopted']]
    credits = sum(r['credits'] for r in rows)
    return dict(
        at=time.strftime('%F %T'),
        head=subprocess.run(['git', 'rev-parse', '--short', 'HEAD'], cwd=kit.root, capture_output=True, text=True).stdout.strip(),
        assets=len(assets), adopted=len(adopted), adopted_zero_credit=sum(1 for a in adopted if not a.get('tripo')),
        families=len(rows), paid_attempts=sum(r['paid_attempts'] for r in rows), credits=credits,
        first_try_rate=round(sum(r['first_try'] for r in tripo_adopted) / len(tripo_adopted), 3) if tripo_adopted else None,
        credits_per_adopted_tripo_asset=round(credits / len(tripo_adopted), 1) if tripo_adopted else None,
        regenerations=sum(max(0, r['paid_attempts'] - 1) for r in rows),
        quality_warnings=sum(r['quality_warnings'] for r in rows),
        problems=problem_counts(kit)), rows


def main(kit, args):
    s, rows = summary(kit)
    for r in rows:
        if r['paid_attempts'] > 1 or not r['first_try']:
            print(f"{r['family']:<28} attempts {r['paid_attempts']}  credits {r['credits']:>5}  adopted {','.join(r['adopted']) or '-'}"
                  f"{'  first try' if r['first_try'] else ''}{'  quality warnings ' + str(r['quality_warnings']) if r['quality_warnings'] else ''}")
    print('METRICS', json.dumps(s, ensure_ascii=False))
    if '--record' in args:
        out = kit.root / ((kit.data.get('harness') or {}).get('metrics') or 'docs/art/metrics.jsonl')
        out.parent.mkdir(parents=True, exist_ok=True)
        with out.open('a') as f:
            f.write(json.dumps(dict(kind='assets', **s), ensure_ascii=False) + '\n')
        print('recorded ->', out.relative_to(kit.root))
