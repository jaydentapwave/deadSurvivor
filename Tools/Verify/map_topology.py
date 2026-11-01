#!/usr/bin/env python3
"""Certified convex-cover nerve analysis. Candidate geometry, not G3 acceptance."""
import argparse
from dataclasses import dataclass
from datetime import datetime
from fractions import Fraction as F
import hashlib
import itertools
import json
import math
import os
import re
from pathlib import Path
import struct
import sys
import time
import uuid
from zoneinfo import ZoneInfo

from map_io import MapInputError, canonical_hash, load_json, reject_symlinks, safe_relative_path, strict_loads
from map_oracle import location, on_segment, polygon, point_distance_squared

ROOT=Path(__file__).resolve().parents[2]


class TopologyBlocked(RuntimeError):pass


@dataclass
class Budget:
    seconds:float=300
    max_cells:int=4096
    max_pairs:int=300000
    max_triples:int=600000
    max_certificate_bytes:int=128*1024*1024
    max_rank_bytes:int=128*1024*1024
    max_cache_bytes:int=128*1024*1024
    max_certificate_memory:int=128*1024*1024
    max_output_bytes:int=128*1024*1024
    def __post_init__(self):self.started=time.monotonic();self.operations=0;self.cache_bytes=0;self.output_bytes=0
    def check(self):
        self.operations+=1
        if time.monotonic()-self.started>self.seconds:raise TopologyBlocked('WALL_LIMIT')


def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])


def hull(points,budget=None):
    unique=set()
    for point in points:
        if budget:budget.check()
        if len(point)!=2 or any(type(x) not in (int,F) for x in point):raise ValueError('NON_RATIONAL_POINT')
        unique.add(tuple(F(x) for x in point))
        if len(unique)>8192:raise TopologyBlocked('PRIMITIVE_VERTEX_LIMIT')
    points=sorted(unique)
    if budget:budget.check()
    if any(max(x.numerator.bit_length(),x.denominator.bit_length())>4096 for p in points for x in p):raise TopologyBlocked("RATIONAL_BITS_LIMIT")
    if len(points)<3:return tuple(points)
    def half(rows):
        result=[]
        for p in rows:
            if budget:budget.check()
            while len(result)>1 and cross(result[-2],result[-1],p)<=0:result.pop()
            result.append(p)
        return result
    return tuple(half(points)[:-1]+half(list(reversed(points)))[:-1])


def convex_contains(poly,p):
    if len(poly)==1:return p==poly[0]
    if len(poly)==2:return on_segment(p,*poly)
    return all(cross(a,b,p)>=0 for a,b in zip(poly,poly[1:]+poly[:1]))


def clip(subject,container,budget):
    """Closed convex intersection; retains singleton and segment intersections."""
    out=tuple(subject)
    if not out:return ()
    if len(container)<3:raise ValueError('CLIP_CONTAINER_NOT_AREA')
    for a,b in zip(container,container[1:]+container[:1]):
        budget.check()
        source,out=out,[]
        if not source:return ()
        previous=source[-1];cp=cross(a,b,previous)
        for p in source:
            budget.check()
            c=cross(a,b,p)
            if (c>=0)!=(cp>=0):
                t=cp/(cp-c);out.append((previous[0]+t*(p[0]-previous[0]),previous[1]+t*(p[1]-previous[1])))
            if c>=0:out.append(p)
            previous,cp=p,c
        # Removing exact duplicate vertices does not remove zero-area intersections.
        out=tuple(dict.fromkeys(out))
        if any(max(v.numerator.bit_length(),v.denominator.bit_length())>4096 for p in out for v in p):raise TopologyBlocked('RATIONAL_BITS_LIMIT')
    return out


def merge(intervals):
    result=[]
    for low,high in sorted(intervals):
        if low>high:raise ValueError('INTERVAL_ORDER')
        if result and low<=result[-1][1]:result[-1]=(result[-1][0],max(high,result[-1][1]))
        else:result.append((low,high))
    return result


def crossings(poly,x):
    ys=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        if a[0]==b[0]:
            if x==a[0]:ys.extend((a[1],b[1]))
        elif min(a[0],b[0])<=x<=max(a[0],b[0]):ys.append(a[1]+(x-a[0])*(b[1]-a[1])/(b[0]-a[0]))
    return sorted(set(ys))


def section(poly,x,ymin,ymax,outside=False):
    ys=sorted(set([ymin,ymax]+[y for y in crossings(poly,x) if ymin<=y<=ymax]));intervals=[]
    def valid(y):
        where=location((x,y),poly)
        return where!=1 if outside else where>=0
    for y in ys:
        if valid(y):intervals.append((y,y))
    for a,b in zip(ys,ys[1:]):
        if valid((a+b)/2):intervals.append((a,b))
    return merge(intervals)


def cell_section(cell,x):
    if x<min(p[0] for p in cell) or x>max(p[0] for p in cell):return []
    if len(cell)==1:return [(cell[0][1],cell[0][1])]
    ys=crossings(cell,x)
    return [(ys[0],ys[-1])] if ys else []


def decompose(poly,frame,budget,outside=False):
    xmin,ymin,xmax,ymax=frame
    xs=sorted(set([xmin,xmax]+[p[0] for p in poly]));cells=[]
    edges=list(zip(poly,poly[1:]+poly[:1]))
    for left,right in zip(xs,xs[1:]):
        budget.check();mid=(left+right)/2
        spans=[]
        for a,b in edges:
            if min(a[0],b[0])<mid<max(a[0],b[0]):
                y=a[1]+(mid-a[0])*(b[1]-a[1])/(b[0]-a[0]);spans.append((y,a,b))
        spans.sort()
        if len(spans)%2:raise ValueError('ODD_SLAB_CROSSINGS')
        bounds=[(ymin,None,None),*spans,(ymax,None,None)]
        pairs=list(zip(bounds[::2],bounds[1::2])) if outside else list(zip(spans[::2],spans[1::2]))
        for lower,upper in pairs:
            def y_at(bound,x):
                y,a,b=bound
                return y if a is None else a[1]+(x-a[0])*(b[1]-a[1])/(b[0]-a[0])
            points=((left,y_at(lower,left)),(right,y_at(lower,right)),(right,y_at(upper,right)),(left,y_at(upper,left)))
            cell=hull(points,budget)
            if cell:cells.append(cell)
    # Closed slab cells usually already cover the original boundary. Exact cover
    # validation, including every cut line, certifies that redundant segment
    # primitives can be omitted. Otherwise retain them and revalidate below.
    core=list(dict.fromkeys(cells))
    try:
        verify_cover(poly,frame,core,budget,outside)
        return core
    except ValueError as error:
        if not str(error).startswith('EXACT_COVER_MISMATCH'):raise
    cells.extend(hull((a,b)) for a,b in edges)
    if outside:
        box=((xmin,ymin),(xmax,ymin),(xmax,ymax),(xmin,ymax));cells.extend(hull((a,b)) for a,b in zip(box,box[1:]+box[:1]))
    cells=list(dict.fromkeys(cells))
    verify_cover(poly,frame,cells,budget,outside)
    return cells


def verify_cover(poly,frame,cells,budget,outside=False):
    xmin,ymin,xmax,ymax=frame;xs=sorted(set([xmin,xmax]+[p[0] for p in poly]));source=list(zip(poly,poly[1:]+poly[:1]))
    # Candidate boundaries may only lie on source edges, cut lines, or frame edges.
    # Thus no unexamined boundary crossing can occur inside an open source slab.
    for cell in cells:
        budget.check()
        if not cell or hull(cell)!=cell:raise ValueError('NONCONVEX_CELL')
        for a,b in zip(cell,cell[1:]+cell[:1]):
            if not (xmin<=a[0]<=xmax and ymin<=a[1]<=ymax):raise ValueError('CELL_OUTSIDE_FRAME')
            if a[0]==b[0] and a[0] in xs:continue
            if outside and a[1]==b[1] and a[1] in (ymin,ymax):continue
            if not any(on_segment(a,c,d) and on_segment(b,c,d) for c,d in source):raise ValueError('CELL_EDGE_NOT_SOURCE')
    for x in xs+[(a+b)/2 for a,b in zip(xs,xs[1:])]:
        budget.check();actual=merge([span for cell in cells for span in cell_section(cell,x)])
        if actual!=section(poly,x,ymin,ymax,outside):raise ValueError('EXACT_COVER_MISMATCH:'+str(x))


_DISKS={}

def disk_bounds(n,budget=None):
    if budget:budget.check()
    if n not in (8,16,32,64,128,256,512):raise ValueError('DISK_RESOLUTION')
    if n in _DISKS:return _DISKS[n]
    points=[]
    # The same finest-grid rational circle points are shared by every resolution.
    for quadrant in range(4):
        for k in range(n//4):
            if budget:budget.check()
            index=k*(512//n)
            t=F(math.tan((index*math.pi/256)/2)).limit_denominator(1<<20) if index else F(0)
            p=((1-t*t)/(1+t*t),2*t/(1+t*t))
            for _ in range(quadrant):p=(-p[1],p[0])
            points.append(p)
    inner=hull(points,budget)
    if len(inner)!=n or any(x*x+y*y!=1 for x,y in inner):raise ValueError('DISK_INNER_INVALID')
    outer=[]
    for a,b in zip(inner,inner[1:]+inner[:1]):
        if budget:budget.check()
        determinant=a[0]*b[1]-a[1]*b[0]
        if determinant<=0:raise ValueError('DISK_TANGENT_ORDER')
        outer.append(((b[1]-a[1])/determinant,(a[0]-b[0])/determinant))
    outer=hull(outer,budget)
    for p in outer:
        for a in inner:
            if budget:budget.check()
            if sum(a[i]*p[i] for i in (0,1))>1:raise ValueError('DISK_OUTER_INVALID')
    _DISKS[n]=(inner,outer)
    return inner,outer


def minkowski(cell,disk,radius,budget=None):return hull(((p[0]+radius*q[0],p[1]+radius*q[1]) for p in cell for q in disk),budget)


def bbox(cell,radius=F(0)):
    return (min(p[0] for p in cell)-radius,min(p[1] for p in cell)-radius,max(p[0] for p in cell)+radius,max(p[1] for p in cell)+radius)


def boxes_overlap(a,b):return not (a[2]<b[0] or b[2]<a[0] or a[3]<b[1] or b[3]<a[1])


def certify_intersection(cells,radius,budget,cache,indices,levels=(8,16,32,64,128,256,512)):
    # A common base-cell point is already a certified witness for all dilations.
    # This avoids expensive circle approximations for deep interior intersections.
    selected=[cells[i] for i in indices];bounds=[bbox(c) for c in selected]
    low=[max(b[j] for b in bounds) for j in (0,1)];high=[min(b[j] for b in bounds) for j in (2,3)]
    if all(low[j]<=high[j] for j in (0,1)):
        witness=tuple((low[j]+high[j])/2 for j in (0,1))
        if all(convex_contains(cell,witness) for cell in selected):return dict(nonempty=True,resolution=0,certificate='EXACT_BASE_WITNESS',witness=[str(x) for x in witness])
        if all(len(c)>=3 for c in selected):
            common=selected[0]
            for cell in selected[1:]:
                common=clip(common,cell,budget) if common else ()
            if common:return dict(nonempty=True,resolution=0,certificate='EXACT_BASE_INTERSECTION',witness=[str(x) for x in common[0]])
    for n in levels:
        budget.check();polys=[]
        for index in indices:
            if (index,n) not in cache:
                budget.check();inner,outer=disk_bounds(n,budget)
                value=(minkowski(cells[index],inner,radius,budget),minkowski(cells[index],outer,radius,budget));budget.check()
                budget.cache_bytes+=128+sum(sys.getsizeof(p)+sum(sys.getsizeof(v)+sum(sys.getsizeof(x)+sys.getsizeof(x.numerator)+sys.getsizeof(x.denominator) for x in v) for v in p) for p in value)
                if budget.cache_bytes>budget.max_cache_bytes:raise TopologyBlocked('POLYGON_CACHE_LIMIT')
                cache[index,n]=value
            polys.append(cache[index,n])
        inside,outside=polys[0]
        for inner,outer in polys[1:]:
            inside=clip(inside,inner,budget) if inside else ()
            outside=clip(outside,outer,budget) if outside else ()
        if inside:
            witness=inside[0]
            return dict(nonempty=True,resolution=n,witness=[str(x) for x in witness])
        if not outside:return dict(nonempty=False,resolution=n,certificate='EXACT_OUTER_EMPTY')
    return dict(nonempty=None,resolution=levels[-1],certificate='UNRESOLVED_CONTACT_BAND')


def homology(vertices,edges,triangles,budget):
    parent=list(range(vertices))
    def find(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
        return i
    for a,b in edges:
        budget.check();a,b=find(a),find(b)
        if a!=b:parent[a]=b
    beta0=len({find(i) for i in range(vertices)});edge_index={edge:i for i,edge in enumerate(edges)};pivots={};rank_bytes=0
    for a,b,c in triangles:
        budget.check()
        vector=(1<<edge_index[a,b])^(1<<edge_index[a,c])^(1<<edge_index[b,c])
        while vector:
            budget.check();pivot=vector.bit_length()-1
            if pivot not in pivots:
                rank_bytes+=(vector.bit_length()+7)//8
                if rank_bytes>budget.max_rank_bytes:raise TopologyBlocked('RANK_MEMORY_LIMIT')
                pivots[pivot]=vector;break
            vector^=pivots[pivot]
    beta1=len(edges)-vertices+beta0-len(pivots)
    if beta1<0:raise ValueError('INVALID_HOMOLOGY_RANK')
    return dict(vertices=vertices,edges=len(edges),triangles=len(triangles),beta0=beta0,boundary2_rank=len(pivots),bounded_complement_components=beta1)


def memory_cost(value):
    total=sys.getsizeof(value)
    if isinstance(value,dict):return total+sum(memory_cost(k)+memory_cost(v) for k,v in value.items())
    if isinstance(value,(tuple,list)):return total+sum(memory_cost(v) for v in value)
    return total


def nerve(cells,radius,budget,levels=(8,16,32,64,128,256,512)):
    if not cells:raise ValueError('EMPTY_COVER')
    if type(radius) is not F or radius<=0:raise ValueError('NON_RATIONAL_RADIUS')
    for cell in cells:
        budget.check()
        if tuple(cell)!=hull(cell,budget):raise ValueError('NONCONVEX_COVER_CELL')
    if len(cells)>budget.max_cells:raise TopologyBlocked('CELL_LIMIT')
    cells=list(cells);boxes=[bbox(c,radius) for c in cells];cache={};edges=[];triples=[];uncertain=[];certificates=[];cert_bytes=0;cert_memory=0;pair_candidates=0;pair_excluded=0
    def keep(kind,indices,proof):
        nonlocal cert_bytes,cert_memory
        row=dict(kind=kind,cells=list(indices),**proof);cert_bytes+=len(json.dumps(row))
        cert_memory+=memory_cost(row)+64
        if cert_bytes>budget.max_certificate_bytes or cert_memory>budget.max_certificate_memory:raise TopologyBlocked('CERTIFICATE_LIMIT')
        certificates.append(row)
        if proof['nonempty'] is None:uncertain.append(row)
    for i in range(len(cells)):
        for j in range(i+1,len(cells)):
            budget.check()
            if not boxes_overlap(boxes[i],boxes[j]):pair_excluded+=1;continue
            pair_candidates+=1
            if pair_candidates>budget.max_pairs:raise TopologyBlocked('PAIR_LIMIT')
            proof=certify_intersection(cells,radius,budget,cache,(i,j),levels);keep('edge',(i,j),proof)
            if proof['nonempty']:edges.append((i,j))
    if pair_candidates+pair_excluded!=len(cells)*(len(cells)-1)//2:raise ValueError('PAIR_COMPLETENESS')
    neighbors=[set() for _ in cells]
    for a,b in edges:neighbors[a].add(b)
    triple_candidates=0
    for a,b in edges:
        for c in sorted(neighbors[a]&neighbors[b]):
            budget.check();triple_candidates+=1
            if triple_candidates>budget.max_triples:raise TopologyBlocked('TRIPLE_LIMIT')
            proof=certify_intersection(cells,radius,budget,cache,(a,b,c),levels);keep('triangle',(a,b,c),proof)
            if proof['nonempty']:triples.append((a,b,c))
    result=dict(status='BLOCKED' if uncertain else 'PASS',scope='TOPOLOGY_DIAGNOSTIC_ONLY',pair_candidates=pair_candidates,pair_excluded=pair_excluded,triple_candidates=triple_candidates,unresolved=uncertain,certificates=certificates,
                cell_sha256=canonical_hash([[[str(x),str(y)] for x,y in c] for c in cells]),g2='NOT_RUN',g3='NOT_RUN')
    if not uncertain:result.update(homology(len(cells),edges,triples,budget))
    else:result['bounded_complement_components']=None
    return result


def point_forbidden(cells,radius,point):
    for cell in cells:
        if convex_contains(cell,point):return True
        if any(point_distance_squared(point,a,b)<=radius*radius for a,b in zip(cell,cell[1:]+cell[:1])):return True
    return False


def f32(x):return struct.unpack('!f',struct.pack('!f',x))[0]


def offsets(count,formation):
    points=([(-24,-24),(24,-24),(-24,24),(24,24)],[(0,-48),(-48,0),(48,0),(0,48)],[(-72,0),(-24,0),(24,0),(72,0)])[formation][:count]
    mean=[f32(sum(p[i] for p in points)/count) for i in (0,1)]
    return [(F(f32(p[0]-mean[0]))/4,F(f32(p[1]-mean[1]))/4) for p in points]


def validate_layout(layout):
    if type(layout) is not dict or layout.get('package')!='MAP-MEADOW-007' or layout.get('revision')!='r001' or layout.get('status')!='PREPRODUCTION_CANDIDATES' or layout.get('coordinate')!='original_pixel_x_right_y_down':raise ValueError('LAYOUT_IDENTITY')
    if layout.get('origin_px')!=[565,580] or type(layout.get('boundary')) is not dict or type(layout['boundary'].get('points_px')) is not list or type(layout.get('objects')) is not list:raise ValueError('LAYOUT_FIELDS')
    points=[layout['boundary']['points_px']]
    for row in layout['objects']:
        if type(row) is not dict:raise ValueError('LAYOUT_OBJECT')
        if row.get('C') is not None:
            if type(row['C']) is not dict or type(row['C'].get('points_px')) is not list:raise ValueError('LAYOUT_C')
            points.append(row['C']['points_px'])
    for poly in points:
        if not 3<=len(poly)<=256:raise ValueError('LAYOUT_POLYGON')
        for point in poly:
            if type(point) is not list or len(point)!=2 or any(type(x) not in (int,float) or (type(x) is float and not math.isfinite(x)) or abs(x)>16384 for x in point):raise ValueError('LAYOUT_COORDINATE')


def verify_production_profile(layout,bundle,root=ROOT):
    if bundle.get('document_role')!='TOPOLOGY_FROZEN_RUNTIME_PROFILE_027' or bundle.get('schema_version')!=1:raise ValueError('RUNTIME_PROFILE_ROLE')
    if type(bundle.get('sources')) is not dict or not bundle['sources']:raise ValueError('PROFILE_DEPENDENCIES')
    required={'Assets/DeadSurvivor/Runtime/DSGame.Formation.cs','Assets/DeadSurvivor/Runtime/DSMapGeometry.cs','Assets/DeadSurvivor/Runtime/DSBlockoutData.cs','Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json','Assets/DeadSurvivor/Editor/DSMapFullVerification.cs','Assets/DeadSurvivor/Editor/DSMapMotionVerification.cs'}
    if set(bundle['sources'])!=required:raise ValueError('PROFILE_DEPENDENCIES')
    for name,digest in bundle['sources'].items():
        if hashlib.sha256(safe_relative_path(root,name).read_bytes()).hexdigest()!=digest:raise ValueError('PROFILE_SOURCE_CHANGED:'+name)
    profile=bundle['runtime'];actual=load_json(safe_relative_path(root,'Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json'))
    if profile.get('scope')!='TOPOLOGY_PRODUCTION_PROFILE_027' or profile.get('sourceDataHash')!=bundle['sources']['Assets/DeadSurvivor/Scenes/Map018_r001/Layout.json'] or profile.get('sourceLayoutHash')!=hashlib.sha256((root/'docs/design/map-meadow-007-r001-layout.json').read_bytes()).hexdigest():raise ValueError('PROFILE_LAYOUT_MISMATCH')
    if (profile.get('scale')!=4 or profile.get('radius')!=8 or profile.get('epsilon')!=float(f32(.001)) or profile.get('worldScale')!=f32(.02) or profile.get('coordinateLimit')!=16384):raise ValueError('RUNTIME_NUMERIC_PROFILE')
    for original,native in zip(layout['boundary']['points_px'],profile['boundary']):
        if any(F(native[k])/4!=F(f32(f32(f32(original[i])-f32(layout['origin_px'][i]))*4))/4 for i,k in enumerate(('x','y'))):raise ValueError('RUNTIME_BOUNDARY_MISMATCH')
    if len(profile['boundary'])!=len(layout['boundary']['points_px']):raise ValueError('RUNTIME_BOUNDARY_COUNT')
    solids=[o for o in layout['objects'] if (o.get('C') or {}).get('points_px')]
    if len(profile['logicalObjects'])!=64 or len(actual['objects'])!=64:raise ValueError('RUNTIME_OBJECT_COUNT')
    for source,native,imported in zip(solids,profile['logicalObjects'],actual['objects']):
        if source['id']!=native['id'] or source['id']!=imported['id'] or len(source['C']['points_px'])!=len(native['points']) or len(native['points'])!=len(imported['points_px']):raise ValueError('RUNTIME_OBJECT_MISMATCH')
        for p,n,q in zip(source['C']['points_px'],native['points'],imported['points_px']):
            if any(F(n[k])!=F(f32(f32(f32(p[i])-f32(layout['origin_px'][i]))*4)) or f32(p[i])!=f32(q[k]) for i,k in enumerate(('x','y'))):raise ValueError('RUNTIME_C_MISMATCH')
    catalog=strict_loads(profile['catalog'])['entries']
    if len(catalog)!=12 or {(c['count'],c['formation']) for c in catalog}!={(n,f) for n in range(1,5) for f in range(3)}:raise ValueError('RUNTIME_CATALOG_COUNT')
    for row in catalog:
        expected=offsets(row['count'],row['formation'])
        if len(row['offsets'])!=len(expected) or any(any(F(p[k])/4!=off[i] for i,k in enumerate(('x','y'))) for p,off in zip(row['offsets'],expected)):raise ValueError('RUNTIME_CATALOG_MISMATCH')
    raw_path=safe_relative_path(root,bundle['raw_path'])
    if hashlib.sha256(raw_path.read_bytes()).hexdigest()!=bundle['raw_sha256'] or load_json(raw_path)!=profile:raise ValueError('NATIVE_PROFILE_EVIDENCE_CHANGED')
    return profile


def revalidate_sources(root,inputs):
    try:
        current={name:hashlib.sha256(safe_relative_path(root,name).read_bytes()).hexdigest() for name in inputs}
    except OSError as error:raise MapInputError('SOURCE_UNAVAILABLE',str(error),'BLOCKED') from error
    if inputs!=current:raise ValueError('SOURCE_CHANGED')


def fresh_output(root):
    base=reject_symlinks(Path(root)/'output/map-full-026');base.mkdir(parents=True,exist_ok=True)
    path=base/('topology-'+uuid.uuid4().hex);path.mkdir();return path


def bounded_write(path,value,budget,summary=False):
    reject_symlinks(path.parent);temporary=path.with_suffix(path.suffix+'.pending');size=0
    with temporary.open('x') as stream:
        for chunk in json.JSONEncoder(ensure_ascii=False,separators=(',',':'),allow_nan=False).iterencode(value):
            if not summary:budget.check()
            raw=chunk.encode('utf-8');size+=len(raw)
            limit=16384 if summary else budget.max_output_bytes-16384-budget.output_bytes
            if size>limit:raise TopologyBlocked('OUTPUT_LIMIT')
            stream.write(chunk)
        stream.flush();os.fsync(stream.fileno())
        if not summary:budget.check()
    temporary.replace(path)
    if not summary:budget.output_bytes+=size


def store_analysis(out,result,profile,budget):
    certificates=result.pop('certificates')
    unresolved=result.pop('unresolved')
    result['unresolved_count']=len(unresolved);result['profile']=profile
    result['incidence_artifact']='incidences.json';result['unresolved_artifact']='unresolved.json'
    bounded_write(out/'incidences.json',certificates,budget)
    bounded_write(out/'unresolved.json',unresolved,budget)


def candidate_cells(layout,count,formation,budget):
    validate_layout(layout)
    origin=layout['origin_px'];scale=f32(4)
    def normalized(points):
        values=[[F(f32(f32(f32(p[i])-f32(origin[i]))*scale))/4 for i in (0,1)] for p in points]
        return polygon([[float(x),float(y)] for x,y in values])
    boundary=normalized(layout['boundary']['points_px'])
    solids=[(o['id'],normalized(o['C']['points_px'])) for o in layout['objects'] if (o.get('C') or {}).get('points_px')]
    if [id for id,_ in solids]!=[f'OB-{i:03}' for i in range(1,65)]:raise ValueError('OBJECT_SET')
    shifts=sorted(set([(F(0),F(0)),*offsets(count,formation)]))
    all_points=[(p[0]-s[0],p[1]-s[1]) for _,poly in [('PERIM-01',boundary),*solids] for s in shifts for p in poly]
    radius=F(float(8+2*float(f32(.001))))/4
    lo_x,lo_y,hi_x,hi_y=bbox(all_points);margin=F(128)
    if margin<=radius:raise ValueError('FRAME_MARGIN')
    frame=(lo_x-margin,lo_y-margin,hi_x+margin,hi_y+margin)
    cells=[];origins=[]
    for shift in shifts:
        for label,poly in [('PERIM-01',boundary),*solids]:
            budget.check();moved=tuple((p[0]-shift[0],p[1]-shift[1]) for p in poly)
            if any(min(p[0]-frame[0],frame[2]-p[0],p[1]-frame[1],frame[3]-p[1])<=radius for p in moved):raise ValueError('FRAME_CONTAINMENT')
            parts=decompose(moved,frame,budget,outside=label=='PERIM-01')
            cells.extend(parts);origins.extend(dict(source=label,offset=[str(x) for x in shift]) for _ in parts)
            if len(cells)>budget.max_cells:raise TopologyBlocked('CELL_LIMIT')
    # Identical closed cells contribute redundant nerve vertices and can be removed.
    unique={}
    for cell,source in zip(cells,origins):unique.setdefault(cell,[]).append(source)
    return list(unique),list(unique.values()),radius,dict(count=count,formation=formation,frame=[str(x) for x in frame],radius_pixel_exact=str(radius),epsilon_float32=f32(.001),image_scale=4,coordinate='logical_binary32_divided_by_4',shifts=[[str(x),str(y)] for x,y in shifts])


def invalidate_acceptance(result):
    if result.get('status')=='PASS':return result
    unaccepted=result.setdefault('unaccepted',{})
    for key in ('bounded_complement_components','p01_safe','geometric_connectivity'):
        if key in result and key not in unaccepted:unaccepted[key]=result[key]
    result.update(bounded_complement_components=None,p01_safe=None,geometric_connectivity='BLOCKED',completion_marker='INCOMPLETE')
    return result


def seal_artifacts(out,budget):
    rows={}
    for name in ('decomposition.json','incidences.json','unresolved.json'):
        path=reject_symlinks(out/name);digest=hashlib.sha256();size=0
        with path.open('rb') as stream:
            while True:
                budget.check();chunk=stream.read(1024*1024)
                if not chunk:break
                size+=len(chunk)
                if size>budget.max_output_bytes:raise TopologyBlocked('ARTIFACT_SIZE_LIMIT')
                digest.update(chunk)
        rows[name]=dict(sha256=digest.hexdigest(),bytes=size)
    return rows


def validate_launch(path,args):
    launch_path=safe_relative_path(ROOT,path);launch=load_json(launch_path)
    fields={'schema_version','document_role','suite_id','case_id','launch_nonce','manifest_path','manifest_sha256','count','formation','seconds','profile'}
    if type(launch) is not dict or set(launch)!=fields or type(launch['schema_version']) is not int or launch['schema_version']!=1 or launch['document_role']!='TOPOLOGY_CHILD_LAUNCH_029':raise ValueError('LAUNCH_SCHEMA')
    for key in ('suite_id','launch_nonce'):
        if type(launch[key]) is not str or not re.fullmatch('[0-9a-f]{32}',launch[key]):raise ValueError('LAUNCH_ID')
    if any(type(launch[k]) is not int for k in ('count','formation','seconds')):raise ValueError('LAUNCH_PARAMETERS')
    if launch['case_id']!=f'N{args.count}-F{args.formation}' or any(launch[k]!=getattr(args,k) for k in ('count','formation','seconds','profile')):raise ValueError('LAUNCH_PARAMETERS')
    manifest_path=safe_relative_path(ROOT,launch['manifest_path'])
    if hashlib.sha256(manifest_path.read_bytes()).hexdigest()!=launch['manifest_sha256']:raise ValueError('LAUNCH_MANIFEST_HASH')
    manifest=load_json(manifest_path)
    rows=[r for r in manifest['cases'] if r['case_id']==launch['case_id']]
    if manifest['document_role']!='TOPOLOGY_SUITE_DIAGNOSTIC_029' or manifest['suite_id']!=launch['suite_id'] or len(rows)!=1 or rows[0]['launch_nonce']!=launch['launch_nonce'] or rows[0]['launch_path']!=path:raise ValueError('LAUNCH_MEMBERSHIP')
    return dict(mode='SUITE_CHILD',suite_id=launch['suite_id'],case_id=launch['case_id'],launch_nonce=launch['launch_nonce'],parent_manifest_sha256=launch['manifest_sha256']),{path:hashlib.sha256(launch_path.read_bytes()).hexdigest(),launch['manifest_path']:launch['manifest_sha256']}


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--count',type=int,choices=range(1,5),default=1);parser.add_argument('--formation',type=int,choices=range(3),default=0);parser.add_argument('--seconds',type=int,default=300);parser.add_argument('--profile',required=True);parser.add_argument('--launch')
    args=parser.parse_args(argv);out=None;inputs={};budget=Budget(seconds=args.seconds)
    started=datetime.now(ZoneInfo('Asia/Seoul')).isoformat()
    invocation=dict(mode='STANDALONE',case_id=f'N{args.count}-F{args.formation}',launch_nonce=uuid.uuid4().hex)
    result=dict(status='NOT_RUN',scope='TOPOLOGY_DIAGNOSTIC_ONLY',g2='NOT_RUN',g3='NOT_RUN')
    try:
        out=fresh_output(ROOT)
        if not 1<=args.seconds<=3600:raise ValueError('DEADLINE_RANGE')
        source=safe_relative_path(ROOT,'docs/design/map-meadow-007-r001-layout.json');profile_path=safe_relative_path(ROOT,args.profile)
        paths=[source,profile_path,Path(__file__),Path(__file__).with_name('map_oracle.py'),Path(__file__).with_name('map_io.py')]
        inputs={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
        if args.launch:
            invocation,launch_inputs=validate_launch(args.launch,args);inputs.update(launch_inputs)
        layout=load_json(source);validate_layout(layout);bundle=load_json(profile_path);verify_production_profile(layout,bundle)
        inputs.update(bundle['sources']);inputs[bundle['raw_path']]=bundle['raw_sha256']
        cells,origins,radius,profile=candidate_cells(layout,args.count,args.formation,budget)
        bounded_write(out/'decomposition.json',dict(profile=profile,cells=[dict(points=[[str(x),str(y)] for x,y in c],origins=o) for c,o in zip(cells,origins)]),budget)
        result=nerve(cells,radius,budget)
        result['p01_safe']=not point_forbidden(cells,radius,(F(0),F(0)))
        result['geometric_connectivity']='PASS' if result.get('bounded_complement_components')==1 and result['p01_safe'] else 'BLOCKED' if result['status']=='BLOCKED' else 'FAIL'
        store_analysis(out,result,profile,budget)
        revalidate_sources(ROOT,inputs);budget.check()
        if result['status']=='PASS':
            result['artifacts']=seal_artifacts(out,budget)
            revalidate_sources(ROOT,inputs);budget.check();result['completion_marker']='SEALED'
    except (TopologyBlocked,MapInputError,ValueError,OSError,KeyError,TypeError,OverflowError) as e:result.update(status='BLOCKED' if isinstance(e,TopologyBlocked) else getattr(e,'status','FAIL'),error=str(e))
    invalidate_acceptance(result)
    result.update(time=datetime.now(ZoneInfo('Asia/Seoul')).isoformat(),started_at=started,invocation=invocation,worker_pid=os.getpid(),inputs=inputs,operations=budget.operations,elapsed_s=time.monotonic()-budget.started)
    if out is not None:
        try:bounded_write(out/'summary.json',result,budget,summary=True)
        except (OSError,TopologyBlocked) as e:
            result.update(status='BLOCKED',error='SUMMARY_WRITE:'+str(e));invalidate_acceptance(result)
    print(json.dumps(dict(status=result['status'],report=str(out/'summary.json') if out else None,error=result.get('error'),components=result.get('bounded_complement_components'),scope=result['scope'])))
    return {'PASS':0,'FAIL':1,'BLOCKED':2,'NOT_RUN':2}[result['status']]
if __name__=='__main__':raise SystemExit(main())
