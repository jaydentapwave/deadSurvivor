# Adapted from Assets/Game/02.Scripts/Editor/HD2D/Round8/FieldGate.py SHA256 108dbd407e7fb69f31a192780f1114a9b1ae856d976f8fdc343ca5fa3c47672c
"""Declare before capture, then bind actual Play outputs to the immutable gate plan.
External reference images are never loaded by this tool. Run with /usr/bin/python3 -B.
"""
import argparse
import colorsys
import datetime
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'output/hd2d-관문/main1'
CAP=ROOT/'output/hd2d-background/plates'
KIT=Path('/Users/hyeongwoo/Desktop/Workspace/studio-kit-25d/plugins/studio-kit/skills/25d-art-setup/payload/root/.ai/25d-art/tools')
sys.path.insert(0,str(KIT))
import gate
gate.dependencies()

def dump(path,value):
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n')

def plan():
    OUT.mkdir(parents=True,exist_ok=True)
    m={'schema':'25d-gate-manifest/1','run_id':'Main1-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ'),'required_plates':['Main1'],'required_settings':{'quality':'Mobile','renderScale':1.0,'srpBatcher':True},'files':{},'sources':[],'capture_record':'capture-record','plates':[]}
    def add(key,path,size=None):
        m['files'][key]={'path':str(path)}
        if size:m['files'][key]['size']=size
        return key
    add('main',CAP/'Main1-main.png',[1920,1080])
    add('pond-view',CAP/'Main1-pond-view.png',[1920,1080])
    for i in range(8):add('move'+str(i),CAP/f'Main1-move-{i}.png',[1920,1080])
    for key,name,size in [('wide','Main1-2520x1080.png',[2520,1080]),('tall','Main1-2048x1536.png',[2048,1536]),('mask','Main1-hero-mask.png',[1920,1080]),('close','closeup-Main1.png',[4896,2832]),('repeat','Main1-repeat.png',[1920,1080]),('grass-cut','Main1-ground-crop.png',[1920,1080]),('lighting-image','Main1-lighting.png',[1920,1080]),('close-mask','Main1-close-hero-mask.png',[4896,2832]),('corner','Main1-pond-corner.png',[2400,960])]:add(key,CAP/name,size)
    for key,name in [('settings','settings.json'),('after-state','runtime-state-after-capture.json'),('camera','camera.json'),('collection','collection.json'),('context','capture-context.json'),('motion','motion.json'),('texels','texels.json'),('lighting','lighting.json'),('shadow','shadow.json'),('capture-record','capture-record.json'),('review','review.json')]:add(key,OUT/name)
    add('metrics',CAP/'plates-main1.json')
    add('bank-color',CAP/'Main1-bank-color.png',[1920,1080])
    add('bank-mask',CAP/'Main1-bank-mask.png',[1920,1080])
    add('pond-crop-before',OUT/'pond-crop-before.json')
    add('C10',OUT/'C10.json')
    add('C15',OUT/'C15.json')
    dependencies=json.loads((OUT/'dependencies.json').read_text())['result']
    sources=set()
    for folder in ['Assets/Game/02.Scripts/Effect/HD2DView','Assets/Game/02.Scripts/Features/Field','Assets/Game/03.Resources/Materials/HD2D','Assets/Game/03.Resources/Shaders/HD2D','Assets/Game/03.Resources/Textures/HD2D','Assets/Game/03.Resources/Models/HD2D','Assets/Game/03.Resources/Art/AIGenerated/Decor','Assets/Game/03.Resources/Art/AIGenerated/Characters','Assets/Game/03.Resources/Art/AIGenerated/Monsters']:
        sources.update(str(p) for p in (ROOT/folder).rglob('*') if p.is_file())
    sources.update(dependencies)
    sources.update(['Tools/hd2d_main_gate.py','Tools/hd2d_field_textures.py','Tools/test_hd2d_field_textures.py','Assets/Game/03.Resources/Resources/Data/Field/field_layout.json','Assets/Game/03.Resources/Resources/Data/Field/ground_path.json','Assets/Game/02.Scripts/Editor/HD2DMainPlateCapture.cs'])
    # B9 updates completion counts; bind immutable copies of the capture-time criteria.
    for original,name in [('docs/features/hd2d-background/prep.md','prep-before.md'),('docs/art/아트-기준서.md','art-spec-before.md')]:
        snapshot=OUT/name;snapshot.write_bytes((ROOT/original).read_bytes());sources.add(str(snapshot))
    for i,path in enumerate(sorted(sources)):
        path=Path(path)
        if not path.is_absolute():path=ROOT/path
        if not path.exists() and 'Packages/' in str(path):
            suffix=str(path).split('Packages/',1)[1];package,relative=suffix.split('/',1)
            path=next((ROOT/'Library/PackageCache').glob(package+'@*'))/relative
        if not path.is_file():raise ValueError('Missing source '+str(path))
        key='source'+str(i);add(key,path);m['sources'].append(key)
    m['plates']=[{'id':'Main1','role':'candidate','main':'main','motion':['move'+str(i) for i in range(8)],'aspects':['wide','tall'],'hero_mask':'mask','closeup':'close','repeat_image':'repeat','capture_settings':'settings','camera':'camera','expected_camera':{'mode':'ortho','angle':30,'height_px':96,'ortho_size':5.625},'motion_measurement':'motion','texel_measurement':'texels','lighting_measurement':'lighting','shadow_measurement':'shadow','performance':['after-state'],'reviews':'review','regions':{'source':'pre-capture: source F-int camera and object anchors projected analytically; fixed paint interiors and empty floor','hero':[928,504,990,600],'ground':[[640,805,1280,901]],'props':[{'name':'tree','box':[500,650,540,690],'ground_box':[620,800,700,850]},{'name':'stone','box':[364,660,384,676],'ground_box':[400,680,430,710]}],'monsters':[{'name':'purple rock monster','box':[1155,375,1195,405],'ground_box':[1220,410,1250,440]}]},'lighting_region':[0,0,1920,1080],'closeup_crop':{'source':'main','box':[420,96,1644,804],'scale':4,'filter':'Point'},'closeup_regions':{'ground':[1360,2656,1760,2816],'prop':[3712,1024,4072,1328]}}]
    m['plates'][0]['id']='Main1'
    m['plates'][0]['regions']=gate.read(OUT/'regions-before.json')
    # Project the selected actual prop into the unchanged exact main crop before capture.
    box=m['plates'][0]['regions']['props'][1]['box']
    assert 420 <= box[0] < box[2] <= 1644 and 96 <= box[1] < box[3] <= 804, 'Selected prop outside common closeup'
    m['plates'][0]['closeup_regions']['prop']=[(box[0]-420)*4,(box[1]-96)*4,(box[2]-420)*4,(box[3]-96)*4]
    h=gate.plan_hash(m)
    dump(OUT/'declaration.json',{'run_id':m['run_id'],'phase':'pre_capture','manifest_plan_sha256':h})
    m['declaration']={'path':'declaration.json','sha256':gate.sha(OUT/'declaration.json')}
    dump(OUT/'manifest.json',m)
    dump(OUT/'source-before.json',{k:gate.sha(m['files'][k]['path']) for k in m['sources']})
    dump(OUT/'capture-context.json',{'runId':m['run_id'],'planHash':h})
    print(m['run_id'],h,len(m['sources']))

def widths(a):
    changes=np.flatnonzero(np.abs(np.diff(a.astype(float),axis=0)).max(1)>6)+1
    lengths=np.diff(changes)
    return {str(int(v)):int((lengths==v).sum()) for v in np.unique(lengths)}

def color(a):
    pixels=a.reshape(-1,3).astype(float)
    bins=(pixels//16).astype(int)
    labels,counts=np.unique(bins,axis=0,return_counts=True)
    chosen=labels[counts.argmax()]
    rep=pixels[(bins==chosen).all(1)].mean(0)
    hsv=np.array([colorsys.rgb_to_hsv(*(c/255)) for c in pixels])
    return {'representative':'#'+''.join(f'{round(v):02x}' for v in rep),'S':float(np.median(hsv[:,1])),'V':float(np.median(hsv[:,2]))}

def finish():
    m=gate.read(OUT/'manifest.json');p=m['plates'][0];collection=gate.read(OUT/'collection.json')
    assert collection['context']['planHash']==gate.plan_hash(m)
    assert collection['context']['runId']==m['run_id']
    paths={k:Path(v['path']) for k,v in m['files'].items()}
    frames=[gate.image(paths[k]) for k in p['motion']];mask=gate.image(paths['mask']).max(2)>0
    shots={s['file']:s for s in collection['shots']}
    bank=gate.image(paths['bank-color']);bankmask=gate.image(paths['bank-mask'])
    lum=lambda a:a[:,:,0]*.2126+a[:,:,1]*.7152+a[:,:,2]*.0722
    red=(bankmask[:,:,0]>200)&(bankmask[:,:,1]<40)
    green=(bankmask[:,:,1]>200)&(bankmask[:,:,0]<40)
    assert red.any() and green.any(),'Missing actual bank top/rim diagnostic pixels'
    top=float(np.median(lum(bank)[red]));rim=float(np.median(lum(bank)[green]))
    dump(paths['C10'],{'source':'Actual Main bank diagnostic2, exact authored rim mask; post and outline disabled identically for color and mask','top_pixels':int(red.sum()),'rim_pixels':int(green.sum()),'top_median':top,'rim_median':rim,'ratio':rim/top,'PASS':bool(1.1<=rim/top<=1.3)})
    c15=[]
    for key in p['motion']:
        a=lum(gate.image(paths[key]));center=float(a[440:640,800:1120].mean());corner=float(np.mean([a[20:100,20:100].mean(),a[20:100,-100:-20].mean(),a[-100:-20,20:100].mean(),a[-100:-20,-100:-20].mean()]));shot=shots[paths[key].name]
        c15.append({'image':key,'corner':corner,'center':center,'ratio':corner/center,'vignetteIntensity':shot['settings']['vignetteIntensity'],'vignetteSmoothness':shot['settings']['vignetteSmoothness']})
    dump(paths['C15'],{'frames':c15,'volume_constant':len({(v['vignetteIntensity'],v['vignetteSmoothness']) for v in c15})==1,'interpretation':'If volume values are constant, brightness ratio variation comes from changing picture content; light/effect tuning deferred to phase6.'})
    moves=[shots[f'Main1-move-{i}.png'] for i in range(8)]
    motion={'measure_from_images':True,'hero_box':p['regions']['hero'],'background_box':[680,805,1260,895],'hero':[],'background':[]}
    for i,(a,key) in enumerate(zip(frames,p['motion'])):
        dx=-round((moves[i]['cameraPosition']['x']-moves[0]['cameraPosition']['x'])*96)
        for region in ['hero','background']:
            diff=gate.motion_mae(frames[0],a,[dx,0],motion[region+'_box'],mask if region=='hero' else None)
            motion[region].append({'image_sha256':gate.sha(paths[key]),'shift':[dx,0],'difference':diff})
    dump(paths['motion'],motion)
    dump(paths['settings'],shots['Main1-main.png']['settings'])
    close=gate.image(paths['close']);cm=gate.image(paths['close-mask']).max(2)>0;ys,xs=np.where(cm)
    dot=(int(ys.max()-ys.min()+1))/48
    ground=gate.crop(close,p['closeup_regions']['ground']);prop=gate.crop(close,p['closeup_regions']['prop'])
    texels={'hero_dot_px':[dot,dot],'ground_dot_px':[8,8],'prop_dot_px':[8,8],'source':'Actual shared background RT integerScale2 × exact Point cropScale4 =8px; both-axis scanline runs also recorded.','runs':{}}
    for label,a in [('ground',ground),('prop',prop)]:texels['runs'][label]={'x':widths(a[len(a)//2]),'y':widths(a[:,a.shape[1]//2])}
    dump(paths['texels'],texels)
    diag=gate.image(paths['lighting-image']);values=np.unique(gate.crop(diag,p['lighting_region']).reshape(-1,3),axis=0)
    gray=[int(c[0]) for c in values if c.max()-c.min()<2 and c[0]>140]
    steps=len(gray)
    dump(paths['lighting'],{'prop_steps':[steps],'measured_gray_levels':gray,'source':'Main-scene bank and fence lighting-only diagnostic; actual 2D rock paint is not claimed as 3D lighting coefficients.'})
    dump(paths['shadow'],{'space':'world','length':collection['shadowLength'],'hero_height':1,'source':'Furthest opaque source texel corner projected along directional-light rays onto y=0, measured from feet in XZ.'})
    main=gate.image(paths['main']);field=gate.crop(main,p['regions']['ground'][0]);gray=field[:,:,0]*.2126+field[:,:,1]*.7152+field[:,:,2]*.0722;blocks=[gray[y:y+24,x:x+24].mean() for y in range(0,len(gray)-23,24) for x in range(0,gray.shape[1]-23,24)]
    threshold=gate.read(KIT.parent/'config/gate.default.json')['checks']['G15']['thresholds']
    repeat=gate.repeat(gate.crop(gate.image(paths['repeat']),p['regions']['ground'][0]),threshold)
    yy,xx=np.where(mask)
    metrics={'run_id':m['run_id'],'manifest_plan_sha256':gate.plan_hash(m),'hero_height_px':int(yy.max()-yy.min()+1),'ground_autocorrelation':repeat,'patch24_luma_sd':float(np.std(blocks)),'colors':{'grass':color(field),'tree':color(gate.crop(main,p['regions']['props'][0]['box'])),'water':color(gate.crop(main,[700,150,850,250])),'rock':color(gate.crop(main,p['regions']['props'][1]['box']))},'ledge':{'worldHeight':collection['ledgeHeight'],'heroHeight':1,'rimFactorAuthored':1.2,'sidePalette':[65,61,40]},'motion':{k:max(f['difference'] for f in motion[k]) for k in ['hero','background']},'texels':texels,'lighting':gate.read(paths['lighting']),'shadow':gate.read(paths['shadow'])}
    metrics['bank_visibility']={s['file']:s['banksInside'] for s in collection['shots'] if s['file'] in ['Main1-main.png','Main1-2520x1080.png','Main1-2048x1536.png']}
    expected=np.repeat(np.repeat(main[96:804,420:1644],4,0),4,1)
    assert np.array_equal(close,expected),'Closeup must be an exact point-enlarged main crop'
    metrics['closeup_crop']={'sourceSha256':gate.sha(paths['main']),'outputSha256':gate.sha(paths['close']),'box':[420,96,1644,804],'scale':4,'exactPixelEquality':True}
    pond_box=gate.read(paths['pond-crop-before'])['box']
    pond_source=gate.image(paths['pond-view']);pond_corner=gate.image(paths['corner'])
    expected_pond=np.repeat(np.repeat(pond_source[pond_box[1]:pond_box[3],pond_box[0]:pond_box[2]],4,0),4,1)
    assert np.array_equal(pond_corner,expected_pond),'Pond closeup must be an exact point-enlarged actual pond view crop'
    metrics['pond_closeup_crop']={'source':'pond-view','box':pond_box,'scale':4,'exactPixelEquality':True}

    dump(paths['metrics'],metrics)
    before=gate.read(OUT/'source-before.json');after={k:gate.sha(paths[k]) for k in m['sources']}
    assert before==after,'Source changed during collection'
    for k,f in m['files'].items():
        if k not in ['capture-record','review']:f['sha256']=gate.sha(paths[k])
    record={'run_id':m['run_id'],'manifest_plan_sha256':gate.plan_hash(m),'files':{k:f['sha256'] for k,f in m['files'].items() if k not in ['capture-record','review']},'source_before':before,'source_after':after}
    dump(paths['capture-record'],record);m['files']['capture-record']['sha256']=gate.sha(paths['capture-record'])
    dump(OUT/'manifest.json',m)
    print(json.dumps(metrics,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('phase',choices=['plan','finish']);args=parser.parse_args()
    {'plan':plan,'finish':finish}[args.phase]()
