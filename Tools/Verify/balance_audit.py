#!/usr/bin/env python3
"""Static item-space audit and summaries of recorded Unity experiments."""
import argparse
import hashlib
import json
import statistics
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--loadouts',type=Path,required=True);args=parser.parse_args();args.loadouts=args.loadouts.resolve()
    source=ROOT/'UnityMigration/Data/PrototypeData.json';data=json.loads(source.read_text());items=data['items'];defs={i['id']:i for i in items}
    party=[data['characters'][i] for i in [0,2,4,5]]
    def eligible(d):
        if not d.get('bonusEffect'):return False
        for c in party:
            w=defs[c['weapon']];mode=w['attackMode'];target=d.get('targetMode')
            if not target or (target=='attack' and mode not in ['heal','healArea']) or target==mode:return True
        return False
    weights={w['id']:w['weight'] for w in data['settings']['weights']};early={i:w*(2 if eligible(defs[i]) else 1) for i,w in weights.items()};late={i:w*(1.5 if i in [c['weapon'] for c in party] else 1) for i,w in weights.items()}
    evidence=json.loads((args.loadouts.parent.parent/'summary.json').read_text())
    assert evidence['sha256']['UnityMigration/Data/PrototypeData.json']==hashlib.sha256(source.read_bytes()).hexdigest(), 'Data differs from experiment input'
    results=json.loads(args.loadouts.read_text());keys=['simulationTime','wave','kills','damage','healing','alive','maxSlotError','win']
    assert len(results)==96
    assert all('parallel-v1 additive loadout' in r['equipped']['policy'] for r in results), 'Expected current additive experiment policy'
    rows=[]
    for item in items:
        delta=[r['survivalDelta'] for r in results if r['equipped']['variant']==item['id']]
        rows.append({'id':item['id'],'name':item['name'],'cells':len(item['shape']),'base':item['baseEffect'],'baseValue':item['baseValue'],'basePerCell':item['baseValue']/len(item['shape']),'bonusEffect':item['bonusEffect'],'bonusValue':item['bonusValue'],'earlyProbability':early[item['id']]/sum(early.values()),'lateProbability':late[item['id']]/sum(late.values()),'survivalDeltaMean':statistics.mean(delta),'survivalDeltaMin':min(delta),'survivalDeltaMax':max(delta)})
    fields=('baseEffect','baseValue','bonusEffect','bonusValue','targetMode','requiresAdjacent','shape')
    duplicates=[[a['id'],b['id']] for n,a in enumerate(items) for b in items[n+1:] if not a.get('attackMode') and all(a.get(k)==b.get(k) for k in fields)]
    output={'dataSHA256':hashlib.sha256(source.read_bytes()).hexdigest(),'input':str(args.loadouts.relative_to(ROOT)),'policy':'parallel-v1 additive loadout; former same-weapon controls no longer apply','rows':rows,'exactEffectShapeDuplicates':duplicates,'characters':[dict(c,effectiveHP=c['hp']/(1-c['def'])) for c in data['characters']],'limits':'Initial loadout paired pilot; fixed movement; no later reward equip; not human balance or DPS ranking.'}
    out=ROOT/'output/balance-audit/parallel-v1';out.mkdir(exist_ok=True,parents=True);(out/'summary.json').write_text(json.dumps(output,ensure_ascii=False,indent=2))
    print('PASS 96 additive pairs; duplicates='+str(duplicates))
if __name__=='__main__':main()
