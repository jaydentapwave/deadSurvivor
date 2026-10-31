#!/usr/bin/env python3
"""Export the local runtime vector sketches as SVG; no external service."""
import html
import json
import re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def main():
    data=json.loads((ROOT/'UnityMigration/Data/PrototypeData.json').read_text())
    paths=dict(re.findall(r'\{"([a-z]+)", "([MLZ 0-9]+)"\}',(ROOT/'UnityMigration/Runtime/DSIconArt.cs').read_text()))
    assert set(paths)=={i['id'] for i in data['items']}
    out=ROOT/'docs/art/items';out.mkdir(parents=True,exist_ok=True)
    parts=['<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="640" viewBox="0 0 1024 640"><rect width="1024" height="640" fill="#101b20"/>']
    for n,item in enumerate(data['items']):
        path=paths[item['id']]; c=item['color'].lstrip('#');color='#'+''.join(f'{round(int(c[k:k+2],16)*.4+153):02x}' for k in (0,2,4))
        x=(n%8)*128;y=(n//8)*160
        (out/(item['id']+'.svg')).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 32 32"><path d="{path}" fill="none" stroke="{color}" stroke-width="1.5" stroke-linejoin="round" stroke-linecap="round"/></svg>')
        parts.append(f'<g transform="translate({x+24},{y+12}) scale(2.5)"><path d="{path}" fill="none" stroke="{color}" stroke-width="1.3"/></g><text x="{x+64}" y="{y+113}" text-anchor="middle" fill="#eeeecc" font-size="15">{html.escape(item["name"])}</text><text x="{x+64}" y="{y+139}" text-anchor="middle" fill="#aabbbb" font-size="12">{item["id"]} / {len(item["shape"])}칸</text>')
    parts.append('</svg>');(out/'catalog.svg').write_text(''.join(parts))
    print('PASS 32 local vector icons and catalog')
if __name__=='__main__':main()
