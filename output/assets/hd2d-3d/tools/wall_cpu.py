"""Wall-only CPU fallback for hosts where Blender cannot initialise Metal.
Geometry follows build.py's two-course profile. Reuses the archived Blender FBX
hierarchy/axis/unit settings and replaces geometry/UV/normal arrays only.
No GPU rendering; paint.py and preview.py remain the adopted pixel pipeline.
"""
import json, math, struct, zlib
from pathlib import Path
import numpy as np


def build(kind, folder):
    height = .513 if kind == 'wall_a' else .521
    courses = [(0., .255, [-.5, -.17, .19, .5]),
               (.255, height, [-.5, .03, .5] if kind == 'wall_a' else [-.5, -.28, .16, .5])]
    tris = []; group = 0; islands = []
    for ri, (z0, z1, xs) in enumerate(courses):
        for bi, (x0, x1) in enumerate(zip(xs, xs[1:])):
            top = z1 if ri == 0 or bi == 0 else z1 - (.018 if bi % 2 else .009)
            profile = [(-.15,z0),(.15,z0),(.15,top-.035),(.115,top),(-.115,top),(-.15,top-.035)]
            verts = np.array([(x,y,z) for x in (x0,x1) for y,z in profile]); centre = verts.mean(0)
            faces = [list(range(5,-1,-1)), list(range(6,12))]
            faces += [[i,(i+1)%6,(i+1)%6+6,i+6] for i in range(6)]
            for face in faces:
                p = verts[face]; n = np.cross(p[1]-p[0],p[2]-p[0]); n /= np.linalg.norm(n)
                if n @ (p.mean(0)-centre) < 0: face.reverse(); p = verts[face]; n = -n
                if n[2] < -.97 and max(p[:,2]) < .0001: continue
                # Same world-aligned UV basis and 48 texel/m as build.py.
                u = np.array([1.,0,0]) if abs(n[2])>.8 else np.cross([0,0,1],n)
                u -= n*(u@n); u/=np.linalg.norm(u); v=np.cross(n,u)
                if v[2]<0 and abs(n[2])<=.8:v=-v
                uv=np.c_[p@u,p@v]*48; origin=np.floor(uv.min(0)); extent=np.ceil(uv.max(0))-origin
                island=[]
                for i in range(1,len(face)-1):
                    ix=[0,i,i+1]; q=p[ix]; a=np.linalg.norm(np.cross(q[1]-q[0],q[2]-q[0]))/2
                    if a<1e-7:continue
                    t={'p':q.tolist(),'n':n.tolist(),'vn':[n.tolist()]*3,'part':'brick','grp':group,'uv':(uv[ix]-origin).tolist()}
                    tris.append(t);island.append(t)
                islands.append((int(extent[0]),int(extent[1]),island))
            group+=1
    for W,H in [(64,64),(128,64),(128,128),(256,128),(256,256)]:
        x=y=3;rh=0;places=[]
        for w,h,items in sorted(islands,key=lambda t:-t[1]):
            if x+w+3>W:x=3;y+=rh+3;rh=0
            places.append((x,y,items));x+=w+3;rh=max(rh,h)
        if y+rh+3<=H:break
    for x,y,items in places:
        for t in items:t['uv']=((np.array(t['uv'])+[x,y])/[W,H]).tolist()
    allp=np.array([t['p'] for t in tris]).reshape(-1,3)
    area=sum(np.linalg.norm(np.cross(np.array(t['p'])[1]-t['p'][0],np.array(t['p'])[2]-t['p'][0]))/2 for t in tris)
    m={'kind':kind,'S':W,'SH':H,'A3':float(area),'islands':len(islands),'bounds':[allp.min(0).tolist(),allp.max(0).tolist()],'tris':tris,'producer':'wall_cpu.py; Blender Metal unavailable'}
    Path(folder,'mesh.json').write_text(json.dumps(m))
    print(kind,len(tris),'tris',W,H,'atlas',m['bounds'])


class Node:
    def __init__(self,name,props,count,children=None):self.name=name;self.props=props;self.count=count;self.children=children or []
    def child(self,name):return next(c for c in self.children if c.name==name)


def read_fbx(path):
    data=Path(path).read_bytes();assert data[:23]==b'Kaydara FBX Binary  \x00\x1a\x00';assert struct.unpack_from('<I',data,23)[0]==7400
    def read(o):
        end,n,pl,nl=struct.unpack_from('<IIIB',data,o);o+=13;name=data[o:o+nl].decode();o+=nl
        node=Node(name,data[o:o+pl],n);o+=pl
        while o<end-13 and any(data[o:o+13]):c,o=read(o);node.children.append(c)
        return node,end
    nodes=[];o=27
    while any(data[o:o+13]):n,o=read(o);nodes.append(n)
    return data[:27],nodes,data[o+13:]


def array_prop(kind,values):
    vals=list(values); raw=struct.pack('<'+kind*len(vals),*vals);compressed=zlib.compress(raw)
    return kind.encode()+struct.pack('<III',len(vals),1,len(compressed))+compressed


def export(folder):
    folder=Path(folder);m=json.loads((folder/'mesh.json').read_text());kind=m['kind'];assert kind in ('wall_a','wall_b')
    header,nodes,footer=read_fbx(folder.parent/'r2-wall-archive'/kind/'model.fbx')
    objects=next(n for n in nodes if n.name=='Objects');geo=objects.child('Geometry')
    p=np.array([t['p'] for t in m['tris']]).reshape(-1,3);n=np.array([t['vn'] for t in m['tris']]).reshape(-1,3)
    # Adopted Blender FBX geometry basis: +Y up, original Blender -Y becomes +Z.
    p=p[:,[0,2,1]]*[1,1,-1];n=n[:,[0,2,1]]*[1,1,-1];uv=np.array([t['uv'] for t in m['tris']]).reshape(-1,2)
    def put(node,name,typ,values):a=node.child(name);a.props=array_prop(typ,values);a.count=1
    put(geo,'Vertices','d',p.ravel());ix=np.arange(len(p));poly=ix.copy();poly[2::3]=-poly[2::3]-1
    put(geo,'PolygonVertexIndex','i',poly)
    geo.children=[c for c in geo.children if c.name!='Edges']
    norm=geo.child('LayerElementNormal');put(norm,'Normals','d',n.ravel());put(norm,'NormalsIndex','i',ix)
    uvnode=geo.child('LayerElementUV');put(uvnode,'UV','d',uv.ravel());put(uvnode,'UVIndex','i',ix)
    def encode(node,start):
        name=node.name.encode();prefix=13+len(name)+len(node.props);body=b''
        for child in node.children:body+=encode(child,start+prefix+len(body))
        if node.children:body+=b'\0'*13
        end=start+prefix+len(body)
        return struct.pack('<IIIB',end,node.count,len(node.props),len(name))+name+node.props+body
    data=header
    for node in nodes:data+=encode(node,len(data))
    data+=b'\0'*13+footer;(folder/'model.fbx').write_bytes(data)
    # Standard GLB companion with the same outward triangles and embedded albedo.
    blob=bytearray();views=[]
    def view(raw,target=None):
        while len(blob)%4:blob.append(0)
        v={'buffer':0,'byteOffset':len(blob),'byteLength':len(raw)}
        if target:v['target']=target
        views.append(v);blob.extend(raw);return len(views)-1
    pv=view(p.astype('<f4').tobytes(),34962);nv=view(n.astype('<f4').tobytes(),34962);tv=view((uv*[1,-1]+[0,1]).astype('<f4').tobytes(),34962);iv=view((folder/'albedo.png').read_bytes())
    j={'asset':{'version':'2.0','generator':'HD2D wall CPU fallback'},'buffers':[{'byteLength':len(blob)}],'bufferViews':views,'accessors':[{'bufferView':pv,'componentType':5126,'count':len(p),'type':'VEC3','min':p.min(0).tolist(),'max':p.max(0).tolist()},{'bufferView':nv,'componentType':5126,'count':len(p),'type':'VEC3'},{'bufferView':tv,'componentType':5126,'count':len(p),'type':'VEC2'}],'images':[{'bufferView':iv,'mimeType':'image/png'}],'samplers':[{'magFilter':9728,'minFilter':9728,'wrapS':33071,'wrapT':33071}],'textures':[{'source':0,'sampler':0}],'materials':[{'pbrMetallicRoughness':{'baseColorTexture':{'index':0},'metallicFactor':0,'roughnessFactor':1}}],'meshes':[{'primitives':[{'attributes':{'POSITION':0,'NORMAL':1,'TEXCOORD_0':2},'material':0}]}],'nodes':[{'mesh':0}],'scenes':[{'nodes':[0]}],'scene':0}
    js=json.dumps(j,separators=(',',':')).encode();js+=b' ' *((-len(js))%4);blob+=b'\0'*((-len(blob))%4)
    glb=struct.pack('<III',0x46546c67,2,12+8+len(js)+8+len(blob))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(blob),0x004e4942)+blob
    (folder/'model.glb').write_bytes(glb)
    # Roundtrip the emitted binary, including offsets and outward geometry array lengths.
    read_fbx(folder/'model.fbx');print('EXPORTED CPU',kind,len(m['tris']),'triangles')

if __name__=='__main__':
    import sys
    if sys.argv[1]=='build':build(sys.argv[2],sys.argv[3])
    elif sys.argv[1]=='export':export(sys.argv[2])
    else:raise SystemExit('build <wall_a|wall_b> <folder> | export <folder>')
