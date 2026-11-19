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

def plan(vignette_pairs=False):
    OUT.mkdir(parents=True,exist_ok=True)
    m={'schema':'25d-gate-manifest/1','run_id':OUT.name.title()+'-'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ'),'required_plates':['Main1'],'required_settings':{'quality':'Mobile','renderScale':1.0,'srpBatcher':True},'files':{},'sources':[],'capture_record':'capture-record','plates':[]}
    def add(key,path,size=None):
        m['files'][key]={'path':str(path)}
        if size:m['files'][key]['size']=size
        return key
    add('main',CAP/'Main1-main.png',[1920,1080])
    add('pond-view',CAP/'Main1-pond-view.png',[1920,1080])
    for i in range(8):add('move'+str(i),CAP/f'Main1-move-{i}.png',[1920,1080])
    if vignette_pairs:
        for i in range(8):
            add('vignetteOn'+str(i),CAP/'C15-motion'/str(i)/'main.png',[1920,1080])
            add('vignetteOff'+str(i),CAP/'C15-motion'/str(i)/'vignette-off.png',[1920,1080])
            add('vignetteFrame'+str(i),CAP/'C15-motion'/str(i)/'frame-pair.json')
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
    sources.update(str(p) for p in (ROOT/'Assets/Game/02.Scripts/Effect/Ambient').rglob('*') if p.is_file())
    sources.update(['Assets/Game/03.Resources/Resources/Prefab/HD2DAmbientDust.prefab','Assets/Game/02.Scripts/App/HD2DLightCapture.cs','Tools/hd2d_main_gate.py','Tools/hd2d_field_textures.py','Tools/test_hd2d_field_textures.py','Assets/Game/03.Resources/Resources/Data/Field/field_layout.json','Assets/Game/03.Resources/Resources/Data/Field/ground_path.json','Assets/Game/02.Scripts/Editor/HD2DMainPlateCapture.cs'])
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

def vignette_ratio(on,off):
    """D-L1 effect attenuation, cancelling picture content and global exposure."""
    def sample(a):
        l=a[:,:,0]*.2126+a[:,:,1]*.7152+a[:,:,2]*.0722
        return float(np.mean([l[20:100,20:100].mean(),l[20:100,-100:-20].mean(),l[-100:-20,20:100].mean(),l[-100:-20,-100:-20].mean()])),float(l[440:640,800:1120].mean())
    corner,center=sample(on);off_corner,off_center=sample(off)
    if min(off_corner,off_center,center)<=0:raise ValueError('C15 needs nonzero reference and centre brightness')
    corner_ratio=corner/off_corner;center_ratio=center/off_center
    return {'method':'D-L1: (corners ON/OFF) / (centre ON/OFF), same frozen frame','cornerOn':corner,'cornerOff':off_corner,'centerOn':center,'centerOff':off_center,'cornerRatio':corner_ratio,'centerRatio':center_ratio,'ratio':corner_ratio/center_ratio,'range':'.6~.8'}

def selftest():
    off=np.full((1080,1920,3),100.0)
    assert abs(vignette_ratio(off,off)['ratio']-1)<1e-12
    assert abs(vignette_ratio(off*.7,off)['ratio']-1)<1e-12
    on=off.copy()
    for rows,cols in [(slice(20,100),slice(20,100)),(slice(20,100),slice(-100,-20)),(slice(-100,-20),slice(20,100)),(slice(-100,-20),slice(-100,-20))]:on[rows,cols]*=.7
    assert abs(vignette_ratio(on,off)['ratio']-.7)<1e-12
    try:vignette_ratio(on,off*0)
    except ValueError:pass
    else:raise AssertionError('zero reference accepted')
    print('C15 selftest PASS 4/4: neutral, exposure cancellation, corner attenuation, invalid black reference')

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
    paired='vignetteOn0' in paths
    if paired:
        for i,value in enumerate(c15):
            frame=gate.read(paths['vignetteFrame'+str(i)])
            assert frame['onFrame']==frame['offFrame'] and frame['timeScale']==0,'C15 pair was not a single frozen frame'
            value['rawContentRatio']=value.pop('ratio')
            value.update(vignette_ratio(gate.image(paths['vignetteOn'+str(i)]),gate.image(paths['vignetteOff'+str(i)])))
    dump(paths['C15'],{'frames':c15,'paired':paired,'volume_constant':len({(v['vignetteIntensity'],v['vignetteSmoothness']) for v in c15})==1,'interpretation':'D-L1 effect attenuation from same-frame ON/OFF pairs; rawContentRatio is historical content only.' if paired else 'Historical raw picture-content ratio; not a D-L1 effect-strength judgment.'})
    moves=[shots[f'Main1-move-{i}.png'] for i in range(8)]
    motion={'measure_from_images':True,'hero_box':p['regions']['hero'],'background_box':p['regions']['ground'][0],'hero':[],'background':[]}
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
    dump(paths['lighting'],{'prop_steps':[steps],'measured_gray_levels':gray,'source':'Main-scene mesh lighting-only diagnostic; UV albedo is excluded from the measured lighting coefficients.'})
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

def measure(args):
    """Phase-local observations, separate from the immutable final gate declaration."""
    a=gate.image(Path(args.image))
    regions=json.loads(Path(args.regions).read_text())
    lum=lambda v:v[:,:,0]*.2126+v[:,:,1]*.7152+v[:,:,2]*.0722
    field=gate.crop(a,regions['ground'][0]); grass=color(field)
    rgb=np.array([int(grass['representative'][i:i+2],16) for i in (1,3,5)])
    delta=float(np.linalg.norm(gate.lab(rgb)-gate.lab(np.array([169,164,105]))))
    l=lum(a);corner=float(np.mean([l[20:100,20:100].mean(),l[20:100,-100:-20].mean(),l[-100:-20,20:100].mean(),l[-100:-20,-100:-20].mean()]))
    center=float(l[440:640,800:1120].mean())
    tree=color(gate.crop(a,regions['props'][0]['box']))
    report={'image':str(args.image),'sha256':gate.sha(args.image),'C3':dict(grass,deltaE=delta,range='deltaE<=12, S .30~.42'), 'C8':{'tree':tree,'differenceV':tree['V']-grass['V'],'range':'-.30~-.20'},'C15':{'corner':corner,'center':center,'ratio':corner/center,'range':'.6~.8'},'G09_observation':[dict(name=p['name'],soft=gate.soft(gate.crop(a,p['box']),10)) for p in regions['props']]}
    if 'water' in regions:report['C9']=dict(color(gate.crop(a,regions['water'])),range='V<=.36')
    if args.pair:
        b=gate.image(args.pair);diff=np.abs(a-b)
        report['pair']={'image':args.pair,'sha256':gate.sha(args.pair),'groundMeanDifference':float(gate.crop(diff,regions['ground'][0]).mean()),'maximumDifference':int(diff.max())}
        if 'effect' in regions:
            effect_diff=gate.crop(diff,regions['effect'])
            report['pair'].update(effectBox=regions['effect'],effectMeanDifference=float(effect_diff.mean()),effectMaximumDifference=int(effect_diff.max()),effectChangedPixels=int(np.any(effect_diff>0,axis=2).sum()))
        if args.vignette_pair:
            report['C15_raw_content_ratio']=report['C15']
            report['C15']=vignette_ratio(a,b)
        if 'blurStrength' in regions:
            strengths={}
            for name,box in regions['blurStrength'].items():
                on=lum(gate.crop(a,box));off=lum(gate.crop(b,box))
                on_hf=float(np.abs(np.diff(on,axis=1)).mean())
                off_hf=float(np.abs(np.diff(off,axis=1)).mean())
                strengths[name]={'box':box,'onHF':on_hf,'offHF':off_hf,'attenuation':1-on_hf/off_hf}
            report['blurStrength']={'method':'identical 4-output-pixel stripes, equal normalized depth .5 in each band; horizontal HF attenuation relative to OFF', 'samples':strengths,'bottomAtLeastTop':strengths['bottom']['attenuation']>=strengths['top']['attenuation']}
        if 'hpSharpness' in regions:
            box=regions['hpSharpness'];local=gate.crop(diff,box)
            report['hpSharpness']={'box':box,'maximumDifference':int(local.max()),'meanDifference':float(local.mean()),'identical':bool(not local.any())}
    if args.motion:
        for index,path in enumerate(sorted(Path(args.motion).glob('motion-[0-7].png'))):
            shot=Image.open(path).convert('RGB');flag=shot.crop(regions.get('motionFlagBox',(130,850,250,1000)));water=shot.crop((800,55,1000,100))
            detail=Image.new('RGB',(flag.width+water.width,max(flag.height,water.height)),(32,32,32));detail.paste(flag,(0,0));detail.paste(water,(flag.width,0));OUT.mkdir(parents=True,exist_ok=True);detail.resize((detail.width*4,detail.height*4),Image.Resampling.NEAREST).save(OUT/f'motion-detail-{index}.png')
    directory=Path(args.image).parent
    if (directory/'shadow.json').exists():report['G18']=gate.read(directory/'shadow.json')
    if (directory/'sun-off.png').exists():
        unshadowed=gate.image(directory/'sun-off.png')
        samples=[]
        for box in regions.get('shadow',[]):
            on=gate.crop(a,box); off=gate.crop(unshadowed,box)
            mask=lum(off)-lum(on)>8
            pixels=on[mask]
            if len(pixels):samples.extend(pixels.tolist())
        if samples:
            hsv=np.array([colorsys.rgb_to_hsv(*(np.array(v)/255)) for v in samples])
            report['shadowColor']={'pixels':len(samples),'medianHue':float(np.median(hsv[:,0])*360),'medianV':float(np.median(hsv[:,2])),'range':'H15~50,V>.10 [method, estimated]'}
    lighting=directory/'Main1-lighting.png'
    if lighting.exists():
        diag=gate.image(lighting); values=np.unique(diag.reshape(-1,3),axis=0); gray=[int(v[0]) for v in values if v.max()-v.min()<2 and v[0]>140]
        report['toonDiagnostic']={'levels':gray,'steps':len(gray),'range':'2~3'}
    mask_path=directory/'Main1-bank-mask.png'
    if mask_path.exists():
        mask=gate.image(mask_path);bank=lum(gate.image(directory/'Main1-bank-color.png'))
        red=(mask[:,:,0]>200)&(mask[:,:,1]<40); green=(mask[:,:,1]>200)&(mask[:,:,0]<40);blue=(mask[:,:,2]>200)&(mask[:,:,0]<40)
        top=float(np.median(bank[red]));report['C10']={'rimTop':float(np.median(bank[green]))/top,'sideTop':float(np.median(bank[blue]))/top,'range':'rim1.1~1.3,side.25~.5'}
    if (directory/'blur-off.png').exists():
        sharp=gate.image(directory/'blur-off.png'); difference=np.abs(a-sharp)[:,700:1220]; change=np.where(difference>2,difference,0).max(2).mean(1)
        # Scan outward from the centre, not across unrelated scene texture frequencies.
        active=change>.02; middle=len(active)//2
        above=np.flatnonzero(active[:middle]);below=np.flatnonzero(active[middle:])+middle
        report['blur']={'sharpUVMin':float(1-(below[0]+.5)/len(active)) if len(below) else None,'sharpUVMax':float(1-(above[-1]+.5)/len(active)) if len(above) else None,'range':'v .16/.76 +/-.02','rowDifference':change.tolist(),'highFrequencyByRow':np.abs(np.diff(lum(a),axis=1)).mean(1).tolist()}
    if 'hero' in regions:

        hero=color(gate.crop(a,regions['hero']));report['C16']={'grassS':grass['S'],'heroS':hero['S'],'status':'WARN' if grass['S']>=hero['S'] else 'PASS','judgment':'excluded; use C3'}
    OUT.mkdir(parents=True,exist_ok=True);Image.fromarray(field.astype(np.uint8)).save(OUT/'grass.png');dump(OUT/'measure.json',report);print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('phase',choices=['plan','finish','measure','selftest']);parser.add_argument('--out');parser.add_argument('--captures');parser.add_argument('--image');parser.add_argument('--pair');parser.add_argument('--regions');parser.add_argument('--motion');parser.add_argument('--vignette-pair',action='store_true');args=parser.parse_args()
    if args.phase=='measure' and args.vignette_pair and not args.pair:parser.error('--vignette-pair requires --pair')
    if args.out: OUT=Path(args.out).resolve()
    if args.captures: CAP=Path(args.captures).resolve()
    if args.phase == 'measure': measure(args)
    elif args.phase == 'plan':plan(vignette_pairs=args.vignette_pair)
    else: {'selftest':selftest,'finish':finish}[args.phase]()
