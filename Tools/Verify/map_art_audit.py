#!/usr/bin/env python3
"""Exact projected-edge containment of exported visual stone meshes, not G2/G3."""
import argparse,hashlib,json,math,struct,time
from fractions import Fraction as F
from pathlib import Path
from map_io import strict_loads
from map_oracle import at,edges,intersections,location,polygon
ROOT=Path(__file__).resolve().parents[2]
def no_links(p):
    for part in (p,*p.parents):
        if part.is_symlink():raise ValueError('SYMLINK_ANCESTOR')
def f32(value):return F(struct.unpack('f',struct.pack('f',value))[0])
def bounded(p):
    if p.stat().st_size>16*1024*1024:raise ValueError('EXPORT_TOO_LARGE')
    with p.open('rb') as stream:data=stream.read(16*1024*1024+1)
    if len(data)>16*1024*1024:raise ValueError('EXPORT_TOO_LARGE')
    return data
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def contained_edge(a,b,poly):
    ts={F(0),F(1)}
    for c,d in edges(poly):ts.update(intersections(a,b,c,d))
    ts=sorted(ts)
    return all(location(at(a,b,t),poly)>=0 for t in ts+[(a+b)/2 for a,b in zip(ts,ts[1:])])
def run(path):
    no_links(path);path=path.resolve(strict=True)
    if not path.is_relative_to((ROOT/'output/map-art-028').resolve()):raise ValueError('EXPORT_OUTSIDE_SCOPE')
    if path.stat().st_size>16*1024*1024:raise ValueError("EXPORT_TOO_LARGE")
    raw=bounded(path)
    deadline=time.monotonic()+90

    if len(raw)>16*1024*1024:raise ValueError("EXPORT_TOO_LARGE")
    data=strict_loads(raw.decode());layout_path=ROOT/'Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json'
    if data['scope']!='VISUAL_MESH_CONTAINMENT_ONLY' or data['layout_sha256']!=digest(layout_path):raise ValueError('LAYOUT_IDENTITY')
    layout=json.loads(layout_path.read_bytes());k=F(data['k'])
    exporter=ROOT/'Assets/DeadSurvivor/Editor/DSMapArtVerification.cs'
    if data['exporter_sha256']!=digest(exporter):raise ValueError('EXPORTER_CHANGED')
    if not math.isfinite(float(k)) or k!=F(float.fromhex('0x1.47ae140000000p-6')):raise ValueError('WORLD_SCALE')
    shapes={o['id']:o for o in layout['objects']};expected={f'OB-{n:03d}_Faceted' for n in range(1,6)}
    if len(data['meshes'])!=5 or {m['id'] for m in data['meshes']}!=expected:raise ValueError('MESH_IDENTITY')
    rows=[];sources={}
    for file in data['files']:
        p=ROOT/file['path'];no_links(p)
        if not p.resolve(strict=True).is_relative_to(ROOT/'Assets/DeadSurvivor/Scenes/Map028_ArtExperimental_r004') or p.is_symlink() or digest(p)!=file['sha256']:raise ValueError('MESH_SOURCE_CHANGED')
        sources[file['path']]=file['sha256']
    if len(sources)!=5 or {Path(p).stem for p in sources}!=expected:raise ValueError('MESH_SOURCE_SET')
    bit_rows={row['id']:row['xyz'] for row in data['bits']}
    if len(data['bits'])!=5 or set(bit_rows)!=expected:raise ValueError('FLOAT_BITS_IDENTITY')
    for mesh in data['meshes']:
        source=shapes[mesh['id'].removesuffix('_Faceted')];poly=tuple((F(p['x']-layout['origin_px']['x'])*4,F(p['y']-layout['origin_px']['y'])*4) for p in source['points_px'])
        anchor=poly[0];poly=polygon([[float(x-anchor[0]),float(y-anchor[1])] for x,y in poly])
        vertices=mesh['vertices'];indices=mesh['triangles']
        if not vertices or not indices or len(indices)%3 or len(vertices)>200000 or len(indices)>600000:raise ValueError('EMPTY_OR_MALFORMED_MESH')
        if any(type(i)!=int or i<0 or i>=len(vertices) for i in indices):raise ValueError('MESH_INDEX')
        if any(type(v[a]) not in (float,int) or not math.isfinite(v[a]) for v in vertices for a in ('x','y','z')):raise ValueError('NONFINITE_VERTEX')
        bits=[struct.unpack('<i',struct.pack('<f',v[d]))[0] for v in vertices for d in ('x','y','z')]
        if bits!=bit_rows[mesh['id']]:raise ValueError('NATIVE_FLOAT_ROUNDTRIP_FAILED')
        projected=[(f32(v['x'])/k-anchor[0],-f32(v['z'])/k-anchor[1]) for v in vertices];checked=set()
        for v in projected:
            if time.monotonic()>deadline:raise TimeoutError('AUDIT_DEADLINE')
            if location(v,poly)<0:raise ValueError('UNREFERENCED_VERTEX_OUTSIDE_C')
        for i in range(0,len(indices),3):
            if time.monotonic()>deadline:raise TimeoutError('AUDIT_DEADLINE')
            tri=indices[i:i+3]
            a,b,c=[tuple(f32(vertices[j][d]) for d in ('x','y','z')) for j in tri]
            u=tuple(b[j]-a[j] for j in range(3));v=tuple(c[j]-a[j] for j in range(3))
            cross=(u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
            if sum(q*q for q in cross)==0:raise ValueError('DEGENERATE_3D_TRIANGLE')
            for a,b in zip(tri,tri[1:]+tri[:1]):
                a,b=projected[a],projected[b];key=tuple(sorted((a,b)))
                if key in checked:continue
                if not contained_edge(a,b,poly):raise ValueError('VISUAL_OUTSIDE_C:'+mesh['id'])
                checked.add(key)
        rows.append(dict(id=mesh['id'],vertices=len(vertices),triangles=len(indices)//3,projected_edges=len(checked),status='PASS'))
    if raw!=bounded(path) or digest(exporter)!=data['exporter_sha256'] or any(digest(ROOT/p)!=h for p,h in sources.items()) or digest(layout_path)!=data['layout_sha256']:raise ValueError('SOURCE_CHANGED_DURING_AUDIT')
    result=dict(status='PASS',scope=data['scope'],g2='NOT_RUN',g3='NOT_RUN',export=str(path.relative_to(ROOT)),export_sha256=hashlib.sha256(raw).hexdigest(),layout_sha256=data['layout_sha256'],trusted_exporter_sha256=data['exporter_sha256'],coordinate_recovery='Native IEEE754 bits verified after JSON float32 roundtrip, then exact Fraction',sources=sources,meshes=rows)
    output=path.with_suffix('.audit.json')
    no_links(output)
    with output.open('x') as stream:stream.write(json.dumps(result,indent=2)+'\n')
    return output,result
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('export',type=Path);a=p.parse_args();out,result=run(a.export);print(json.dumps(dict(status=result['status'],report=str(out),triangles=sum(m['triangles'] for m in result['meshes']))))
