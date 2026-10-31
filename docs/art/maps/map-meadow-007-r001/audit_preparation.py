"""Static preparation audit only. No movement, collision gameplay, or Unity execution."""
from pathlib import Path
import json,hashlib,csv,datetime,copy,tempfile,subprocess,sys,uuid,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[4];d=root/'docs/design';p=d/'map-meadow-007-r001-layout.json';a=json.loads(p.read_text()); checks=[]
def check(name,ok):checks.append({'name':name,'status':'PASS' if ok else 'FAIL'})
def cross(a,b,c):return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
def on(a,b,c):return cross(a,b,c)==0 and min(a[0],b[0])<=c[0]<=max(a[0],b[0]) and min(a[1],b[1])<=c[1]<=max(a[1],b[1])
def intersect(a,b,c,d):
 x,y,z,w=cross(a,b,c),cross(a,b,d),cross(c,d,a),cross(c,d,b)
 return (x*y<0 and z*w<0) or on(a,b,c) or on(a,b,d) or on(c,d,a) or on(c,d,b)
def edges(ps):return list(zip(ps,ps[1:]+ps[:1]))
def simple(ps):
 es=edges(ps)
 return all(not intersect(*e,*f) for i,e in enumerate(es) for j,f in enumerate(es) if j>i+1 and not(i==0 and j==len(es)-1))
def inside(p,ps):
 x,y=p;v=False
 for aa,bb in edges(ps):
  if on(aa,bb,p):return True
  if (aa[1]>y)!=(bb[1]>y) and x<(bb[0]-aa[0])*(y-aa[1])/(bb[1]-aa[1])+aa[0]:v=not v
 return v
check('69 unique registration rows',len(a['objects'])==69 and len({o['id'] for o in a['objects']})==69)
for o in a['objects']+[{'id':'PERIM-01','V':{'points_px':a['boundary']['points_px']}}]:
 if not o['V']:continue
 ps=o['V']['points_px'];id=o['id']
 check(id+' coordinates in image',all(0<=x<=1195 and 0<=y<=1110 for x,y in ps))
 check(id+' simple nondegenerate',simple(ps) and len(set(map(tuple,ps)))==len(ps) and sum(x*v-y*u for (x,y),(u,v) in edges(ps))!=0)
check('64 observed candidates',sum(bool(o['V']) for o in a['objects'])==64)
check('parent never collides',next(o for o in a['objects'] if o['id']=='OB-G01')['C'] is None)
check('all collision validity blocked',all(o['C'] is None or o['C']['validity']=='BLOCKED' for o in a['objects']))
r={r['id']:r for r in a['routes']}
check('18 unique route candidates',len(r)==len(a['routes'])==18)
check('continuous first junction',r['RT-006']['points_px'][-1]==r['RT-018']['points_px'][0])
check('continuous second junction',r['RT-018']['points_px'][-1]==r['RT-008']['points_px'][-1])
rows=list(csv.DictReader((d/'map-meadow-007-r001-cases.csv').open(encoding='utf-8-sig')))
check('56 unique plans not runs',len(rows)==56 and len({x['id'] for x in rows})==56 and all(x['execution']=='NOT_RUN' and x['readiness']=='BLOCKED' for x in rows))
h=hashlib.sha256(p.read_bytes()).hexdigest()
for f in (root/'docs/art/maps/map-meadow-007-r001').glob('*.svg'):check(f.name+' layout hash',f'layout_sha256={h}' in f.read_text())
# Counterexamples for the elementary intersection helper, not a map collision harness.
check('segment crossing fixture',intersect([0,0],[4,0],[2,-1],[2,1]))
check('segment separated fixture',not intersect([0,0],[1,0],[2,-1],[2,1]))
check('segment touching fixture',intersect([0,0],[1,0],[1,0],[2,1]))
risks=[]
for route in a['routes']+a.get('connectors',[]):
 for o in a['objects']:
  if not o['C']:continue
  ps=o['C']['points_px'];wps=route['points_px']
  if any(inside(pt,ps) for pt in wps) or any(intersect(x,y,u,v) for x,y in zip(wps,wps[1:]) for u,v in edges(ps)):
   risks.append({'route':route['id'],'object':o['id'],'status':'BLOCKED_CANDIDATE_OVERLAP','note':'zero-radius candidate centerline overlaps proposed envelope; not runtime failure; preserve source route and review C or route under CH'})
groups=a['routes']+a.get('connectors',[])
seen={groups[0]['id']}
while True:
 old_seen=set(seen)
 points={tuple(pt) for g in groups if g['id'] in seen for pt in g['points_px']}
 seen.update(g['id'] for g in groups if any(tuple(pt) in points for pt in g['points_px']))
 if old_seen==seen:break
check('all 18 RT groups linked by explicit candidate endpoints',all(g['id'] in seen for g in a['routes']))
# Mutate C and U independently in isolated temp data; exported fields must follow each layer.
with tempfile.TemporaryDirectory(prefix='map007-render-') as temp:
 t=Path(temp);trial=copy.deepcopy(a);o=trial['objects'][0]
 o['C']['points_px']=[[x+3,y] for x,y in o['C']['points_px']]
 o['U']['points_px']=[[x,y+5] for x,y in o['U']['points_px']]
 tp=t/'layout.json';tp.write_text(json.dumps(trial))
 subprocess.run([sys.executable,str(root/'docs/art/maps/map-meadow-007-r001/render_sheets.py'),str(tp),str(t/'sheets')],check=True,capture_output=True)
 for mode in ('collision','tests','changes'):
  tree=ET.parse(t/'sheets'/f'map-meadow-007-r001-{mode}.svg');nodes={n.attrib.get('id'):n for n in tree.iter() if 'id' in n.attrib}
  for layer in (('V',) if mode=='changes' else ('C','U')):
   expected=' '.join(f'{x},{y}' for x,y in o[layer]['points_px'])
   check(mode+' independently renders '+layer,nodes[layer+'-OB-001'].attrib['points']==expected)
now=datetime.datetime.now().astimezone();out=root/'output/verification'/('map007-preparation-'+now.strftime('%Y%m%d-%H%M%S')+'-'+uuid.uuid4().hex[:8]);out.mkdir(parents=True)
paths=list((root/'UnityMigration/Runtime').glob('DSGame*.cs'))+[root/'UnityMigration/Runtime/DSWorld.cs',root/'Tools/Verify/run.py']+[p,d/'map-meadow-007-r001-cases.csv',root/'docs/art/maps/references/VerdantMeadowsFullMap.png']+list((root/'docs/art/maps/map-meadow-007-r001').glob('*'))+list(d.glob('map-meadow-007-r001-*.md'))
result={'time':now.isoformat(),'scope':'static documentary geometry/reference audit only','status':'PASS' if all(c['status']=='PASS' for c in checks) else 'FAIL','checks':checks,'candidate_overlap_diagnostics':risks,'runtime':'NOT_RUN','collision_validity':'BLOCKED','sha256':{str(f.relative_to(root)):hashlib.sha256(f.read_bytes()).hexdigest() for f in paths if f.is_file()}}
(out/'audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'report':str(out/'audit.json'),'status':result['status'],'checks':len(checks),'failed':[c for c in checks if c['status']=='FAIL'],'candidate_overlaps':risks},ensure_ascii=False))
sys.exit(0 if result['status']=='PASS' else 1)
