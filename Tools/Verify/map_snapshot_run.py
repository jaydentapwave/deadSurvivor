#!/usr/bin/env python3
"""Owned source-to-DTO diagnostics only, not full map v1/G2/G3 acceptance."""
import copy,hashlib,json,math,os,re,sys,uuid
from pathlib import Path
from datetime import datetime
from zoneinfo import ZoneInfo
import map_blockout as b
from map_io import load_json,strict_loads,reject_symlinks,read_input_bytes
from map_input_scope import verify_then_release,verified_scopes
m=b.motion
BASE=m.ROOT/'output/map-full-026'
ROLE='SNAPSHOT_FOUNDATION_ONLY_031'
ROOT_KEYS=set('schema_version document_role contentHash geometryHash comparisonPolicy binding game renderMetadata selected liveEnemyIds team stash enemyPool shots effects history config rng lootRng routes navigation protocol'.split())
SOURCE_FIELDS={
 'Gear':'id grade x y type rotation cd lastPeriod activationFlash',
 'Survivor':'index hp maxhp shield damageTaken healingReceived aliveTime cells pos healClock shieldClock flash shareClock shareProvider sharePeriod',
 'Enemy':'id type phase pos charge flock hp maxhp damage cd flash phaseTime rushTime offscreenTime',
 'Shot':'type pos dir target speed damage range travel dead pierce radius',
 'Effect':'pos life max r color text',
 'History':'wave kills survivors time realBattle prepTime pausedTime formation skipped defeated',
 'Config':'adaptiveDrops duration speed countScale drops',
 'Weight':'id weight',
 'Wave':'duration drops count hp damage',
 'Queued':'id nonce kind source value sequence enqueueFrame',
 'Event':'id nonce kind source result before after value sequence enqueueFrame consumeFrame',
 'Route':'index target retry'}
REJECTIONS='escaped-output-limit rng-alias shot-hit-set-alias survivor-cells-alias config-array-alias history-alias live-enemy-alias gear-alias gear-id team-alias enemy-id unknown-owner unknown-dash-hit untraced-rng content-mutation nonfinite derived-gear reentrant effect-limit selected-limit aggregate-shot-hits shot-alias effect-alias identical-content-replacement identical-items-dictionary identical-equipment identical-nested-settings identical-nested-array identical-layout-object'.split()

def finite_tree(value):
    if isinstance(value,float) and not math.isfinite(value):raise ValueError('SNAPSHOT_NONFINITE')
    if type(value) is dict:
        for v in value.values():finite_tree(v)
    elif type(value) is list:
        for v in value:finite_tree(v)

def keys(value,names):
    if type(value) is not dict or set(value)!=set(names.split() if type(names) is str else names):raise ValueError('SNAPSHOT_OBJECT_FIELDS')
def integer(value,wide=False):
    bits=64 if wide else 32
    if type(value) is not int or not -(2**(bits-1))<=value<2**(bits-1):raise ValueError('SNAPSHOT_INTEGER')
def number(value,single=True):
    if type(value) not in (int,float) or not math.isfinite(value) or single and abs(value)>3.4028234663852886e38:raise ValueError('SNAPSHOT_NUMBER')
def flag(value):
    if type(value) is not bool:raise ValueError('SNAPSHOT_BOOLEAN')
def array(value,maximum):
    if type(value) is not list or len(value)>maximum:raise ValueError('SNAPSHOT_ARRAY')
def text(value,maximum=4096):
    keys(value,'isNull value');flag(value['isNull'])
    if type(value['value']) is not str or len(value['value'])>maximum or value['isNull'] and value['value']!='':raise ValueError('SNAPSHOT_TEXT')
    value['value'].encode('utf-8',errors='strict')
def vector(value,grid=False):
    keys(value,'x y')
    for v in value.values():(integer if grid else number)(v)
def named_types(groups):
    result={}
    for kind,names in groups.items():
        for name in names.split():result[name]=kind
    return result
GAME_TYPES=named_types({
 'System.Int32':'Formation PendingFormation mapSearches mapSpawnSearches mapEnemyCursor <MapSpawnFailures>k__BackingField Wave Kills Spawned WaveKills uid enemyUid burningSpawned bomberSpawnCount',
 'System.Single':'RealBattle RealPrep RealPaused WaveReal WavePaused LobbyTime ResultTime Elapsed TotalTime spawnClock recycleCooldown burningAngle bomberSpawnAngle',
 'System.Boolean':'TestControlsUsed VerificationRun mapRequired Paused Win','UnityEngine.Vector2':'Center MoveInput','System.String':'State'})
RENDER_TYPES=named_types({'System.Single':'ViewWidth ViewHeight InitialLoadTime','System.Double':'initializationStarted','System.String':'<MapRejection>k__BackingField'})
PROTOCOL_TYPES=named_types({'System.Boolean':'mapCommandPermit mapCommandConsuming mapQueueBootstrapUsed <MapInputD>k__BackingField mapUnitScope mapFramePermit fixtureFramePermit','System.String':'mapQueueNonce mapQueueError mapUnitRegisteredOwner','System.Int32':'mapQueueSequence mapQueueConsumed <MapInputFrame>k__BackingField <MapInputDevice>k__BackingField mapUnitRegisteredInstance','UnityEngine.Vector2':'<MapInputObserved>k__BackingField','System.Int64':'normalGameFrames normalGameSimulations'})
def scalars(rows,expected):
    array(rows,len(expected))
    if any(type(row) is not dict or type(row.get('name')) is not str for row in rows):raise ValueError('SNAPSHOT_SCALAR_ROW')
    if len(rows)!=len(expected) or {r.get('name') for r in rows}!=set(expected):raise ValueError('SNAPSHOT_SCALAR_COVERAGE')
    for row in rows:
        keys(row,'name kind integer longInteger real doubleReal flag vector text')
        if row['kind']!=expected[row['name']]:raise ValueError('SNAPSHOT_SCALAR_KIND')
        integer(row['integer']);integer(row['longInteger'],True);number(row['real']);number(row['doubleReal'],False);flag(row['flag']);vector(row['vector']);text(row['text'])
def schema(data):
    binding=data['binding'];keys(binding,'mapMode layoutHash sourceLayoutHash sourceRole imageScale worldScale bodyRadius elevation epsilon coordinateLimit startCenter')
    if binding['sourceRole']!='PREVIEW_ONLY' or binding['mapMode']!='MapRequired':raise ValueError('SNAPSHOT_BINDING_ROLE')
    for key in ('layoutHash','sourceLayoutHash'):
        if type(binding[key]) is not str or not re.fullmatch('[0-9a-f]{64}',binding[key]):raise ValueError('SNAPSHOT_BINDING_HASH')
    for key in ('imageScale','worldScale','bodyRadius','elevation'):number(binding[key])
    for key in ('epsilon','coordinateLimit'):number(binding[key],False)
    vector(binding['startCenter']);scalars(data['game'],GAME_TYPES);scalars(data['renderMetadata'],RENDER_TYPES)
    game={r['name']:r for r in data['game']}
    if not game['mapRequired']['flag'] or not game['VerificationRun']['flag'] or game['State']['text']['isNull'] or game['State']['text']['value'] not in ('lobby','battle','prep','result'):raise ValueError('SNAPSHOT_MAP_REQUIRED_STATE')
    if data['comparisonPolicy']!='031/r001-game-protocol-render-separated':raise ValueError('SNAPSHOT_COMPARISON_POLICY')
    for key,maximum in [('selected',4),('liveEnemyIds',4096),('team',4),('stash',256),('enemyPool',4096),('shots',8192),('effects',8192),('history',128),('routes',4096),('navigation',256)]:array(data[key],maximum)
    for value in data['selected']+data['liveEnemyIds']:integer(value)
    gear_ids=[]
    def gear(row):
        keys(row,SOURCE_FIELDS['Gear']);text(row['type']);gear_ids.append(row['id'])
        for key in ('id','grade','x','y','rotation'):integer(row[key])
        for key in ('cd','lastPeriod','activationFlash'):number(row[key])
    for index,row in enumerate(data['team']):
        keys(row,SOURCE_FIELDS['Survivor']+' slot reserved radius items')
        if row['slot']!=index:raise ValueError('SNAPSHOT_SLOT_ORDER')
        for key in ('slot','index','shareProvider'):integer(row[key])
        for key in 'hp maxhp shield damageTaken healingReceived aliveTime healClock shieldClock flash shareClock sharePeriod radius'.split():number(row[key])
        vector(row['pos']);vector(row['reserved']);array(row['cells'],36)
        if len(row['cells'])!=36:raise ValueError('SNAPSHOT_CELLS')
        for value in row['cells']:flag(value)
        array(row['items'],256)
        for item in row['items']:gear(item)
    for row in data['stash']:gear(row)
    if len(gear_ids)>256 or len(set(gear_ids))!=len(gear_ids):raise ValueError('SNAPSHOT_GEAR_IDS')
    enemy_ids=[]
    for row in data['enemyPool']:
        keys(row,SOURCE_FIELDS['Enemy']+' dashHitSlots');integer(row['id']);enemy_ids.append(row['id']);text(row['type']);text(row['phase'])
        for key in ('pos','charge','flock'):vector(row[key])
        for key in 'hp maxhp damage cd flash phaseTime rushTime offscreenTime'.split():number(row[key])
        array(row['dashHitSlots'],4)
        for slot in row['dashHitSlots']:integer(slot)
        if row['dashHitSlots']!=sorted(set(row['dashHitSlots'])):raise ValueError('SNAPSHOT_DASH_SET')
        for slot in row['dashHitSlots']:
            integer(slot)
            if not 0<=slot<len(data['team']):raise ValueError('SNAPSHOT_DASH_REFERENCE')
    if len(set(enemy_ids))!=len(enemy_ids) or len(set(data['liveEnemyIds']))!=len(data['liveEnemyIds']) or any(v not in enemy_ids for v in data['liveEnemyIds']):raise ValueError('SNAPSHOT_ENEMY_IDS')
    hit_count=0
    for row in data['shots']:
        keys(row,SOURCE_FIELDS['Shot']+' ownerSlot hits');text(row['type']);flag(row['dead']);integer(row['pierce']);integer(row['ownerSlot'])
        if not -1<=row['ownerSlot']<len(data['team']):raise ValueError('SNAPSHOT_SHOT_OWNER')
        for key in ('pos','dir','target'):vector(row[key])
        for key in ('speed','damage','range','travel','radius'):number(row[key])
        array(row['hits'],65536);hit_count+=len(row['hits'])
        for hit in row['hits']:integer(hit)
        if row['hits']!=sorted(set(row['hits'])) or hit_count>65536:raise ValueError('SNAPSHOT_SHOT_HITS')
        for value in row['hits']:integer(value)
    for row in data['effects']:
        keys(row,SOURCE_FIELDS['Effect']);vector(row['pos']);text(row['color']);text(row['text'])
        for key in ('life','max','r'):number(row[key])
    for row in data['history']:
        keys(row,SOURCE_FIELDS['History']+' revived')
        for key in ('wave','kills','survivors','formation'):integer(row[key])
        for key in ('time','realBattle','prepTime','pausedTime'):number(row[key])
        flag(row['skipped']);flag(row['defeated']);array(row['revived'],256)
        for value in row['revived']:text(value)
    config=data['config'];keys(config,SOURCE_FIELDS['Config']+' weights waves');flag(config['adaptiveDrops']);integer(config['drops'])
    for key in ('duration','speed','countScale'):number(config[key])
    array(config['weights'],256);array(config['waves'],128);type_count=0
    for row in config['weights']:keys(row,'id weight');text(row['id']);number(row['weight'])
    for row in config['waves']:
        keys(row,SOURCE_FIELDS['Wave']+' types');integer(row['drops']);integer(row['count'])
        for key in ('duration','hp','damage'):number(row[key])
        array(row['types'],8192);type_count+=len(row['types'])
        if type_count>16384:raise ValueError('SNAPSHOT_CONFIG_TYPES')
        for value in row['types']:text(value)
    for key in ('rng','lootRng'):
        rng=data[key];keys(rng,'seed count traceHash calls');integer(rng['seed']);integer(rng['count']);array(rng['calls'],16384)
        if type(rng['traceHash']) is not str or not re.fullmatch('[0-9a-f]{64}',rng['traceHash']) or rng['count']!=len(rng['calls']):raise ValueError('SNAPSHOT_RNG_SCHEMA')
        for index,call in enumerate(rng['calls']):
            keys(call,'sequence method args response');integer(call['sequence'])
            if call['sequence']!=index or any(type(call[k]) is not str for k in ('method','args','response')):raise ValueError('SNAPSHOT_RNG_CALL')
            if call['method']=='NextDouble':
                if call['args']!='' or not math.isfinite(float(call['response'])) or not 0<=float(call['response'])<1:raise ValueError('SNAPSHOT_RNG_RESPONSE')
            elif call['method']=='Next':
                args=[] if call['args']=='' else call['args'].split(',')
                if len(args)>2 or any(not re.fullmatch('-?[0-9]+',a) for a in args) or not re.fullmatch('-?[0-9]+',call['response']):raise ValueError('SNAPSHOT_RNG_ARGS')
                args=list(map(int,args));response=int(call['response']);integer(response)
                for a in args:integer(a)
                lo,hi=(0,2147483647) if not args else (0,args[0]) if len(args)==1 else tuple(args)
                if lo>hi or not (response==lo if lo==hi else lo<=response<hi):raise ValueError('SNAPSHOT_RNG_RANGE')
            else:raise ValueError('SNAPSHOT_RNG_METHOD')
    point_count=cache_count=0
    for row in data['routes']:
        keys(row,'enemyId index target retry hasPoints points');integer(row['enemyId']);integer(row['index']);vector(row['target']);number(row['retry']);flag(row['hasPoints']);array(row['points'],65536);point_count+=len(row['points'])
        if row['enemyId'] not in enemy_ids or point_count>65536 or not row['hasPoints'] and row['points']:raise ValueError('SNAPSHOT_ROUTE_REFERENCE')
        for value in row['points']:vector(value)
    if len({r.get('enemyId') for r in data['routes'] if type(r) is dict})!=len(data['routes']):raise ValueError('SNAPSHOT_DUPLICATE_ROUTE')
    nav_radii=[]
    for row in data['navigation']:
        keys(row,'radius lastVisited nodes edges');number(row['radius']);integer(row['lastVisited']);array(row['nodes'],100000);array(row['edges'],100000);cache_count+=len(row['nodes'])+len(row['edges'])
        if cache_count>100000:raise ValueError('SNAPSHOT_CACHE_LIMIT')
        nav_radii.append(row['radius'])
        for node in row['nodes']:keys(node,'point safe');vector(node['point'],True);flag(node['safe'])
        for edge in row['edges']:keys(edge,'a b safe');vector(edge['a'],True);vector(edge['b'],True);flag(edge['safe'])
        node_keys=[(n['point']['x'],n['point']['y']) for n in row['nodes']];edge_keys=[(e['a']['x'],e['a']['y'],e['b']['x'],e['b']['y']) for e in row['edges']]
        if node_keys!=sorted(set(node_keys)) or edge_keys!=sorted(set(edge_keys)):raise ValueError('SNAPSHOT_CACHE_KEYS')
    if nav_radii!=sorted(set(nav_radii)):raise ValueError('SNAPSHOT_NAV_RADIUS_KEYS')
    protocol=data['protocol'];keys(protocol,'scalars pending knownCommandIds events');scalars(protocol['scalars'],PROTOCOL_TYPES);array(protocol['pending'],32);array(protocol['knownCommandIds'],256);array(protocol['events'],128)
    for value in protocol['knownCommandIds']:text(value)
    for row in protocol['pending']:
        keys(row,SOURCE_FIELDS['Queued'])
        for key in ('id','nonce','kind','source'):text(row[key])
        for key in ('value','sequence','enqueueFrame'):integer(row[key])
    for row in protocol['events']:
        keys(row,SOURCE_FIELDS['Event'])
        for key in ('id','nonce','kind','source','result','before','after'):text(row[key],1024*1024 if key in ('before','after') else 4096)
        for key in ('value','sequence','enqueueFrame','consumeFrame'):integer(row[key])

def snapshot(text):
    if type(text) is not str or len(text.encode())>32*1024*1024:raise ValueError('SNAPSHOT_BYTES')
    data=strict_loads(text)
    finite_tree(data)
    if type(data) is not dict or set(data)!=ROOT_KEYS or data['document_role']!='FULL_MAP_SNAPSHOT_031' or type(data['schema_version']) is not int or data['schema_version']!=1:raise ValueError('SNAPSHOT_SCHEMA')
    for key in ('contentHash','geometryHash'):
        if type(data[key]) is not str or not re.fullmatch('[0-9a-f]{64}',data[key]):raise ValueError('SNAPSHOT_HASH')
    schema(data)
    if data['binding']['mapMode']!='MapRequired' or data['binding']['bodyRadius']!=8 or any(row['radius']!=8 for row in data['team']):raise ValueError('SNAPSHOT_BODY_PROFILE')
    return data

def expected_fields(data):
    rows=[]
    def add(prefix,kind):rows.extend(prefix+'.'+name for name in SOURCE_FIELDS[kind].split())
    rows.extend('Game.'+name for name in GAME_TYPES);rows.extend('Render.'+name for name in RENDER_TYPES);rows.extend('Protocol.'+name for name in PROTOCOL_TYPES);add('Route','Route')
    for i,team in enumerate(data['team']):
        add('Survivor'+str(i),'Survivor')
        for j,_ in enumerate(team['items']):add(f'Gear{i}-{j}','Gear')
    for i,_ in enumerate(data['stash']):add('Stash'+str(i),'Gear')
    add('Enemy','Enemy');add('SecondEnemy','Enemy');add('GhostEnemy','Enemy');add('PendingCommand','Queued');add('CommandEvent','Event')
    for i,_ in enumerate(data['shots']):add('Shot'+str(i),'Shot')
    for i,_ in enumerate(data['effects']):add('Effect'+str(i),'Effect')
    add('History','History');add('Config','Config')
    for i,_ in enumerate(data['config']['weights']):add('Weight'+str(i),'Weight')
    for i,_ in enumerate(data['config']['waves']):add('Wave'+str(i),'Wave')
    return rows

def judge(spec,raw,sha):
    if raw.get('role')!=ROLE or any(raw.get(k)!=spec[k] for k in ('run','nonce')) or raw.get('requestSha')!=sha:raise ValueError('SNAPSHOT_RAW_IDENTITY')
    if raw.get('complete') is not True or raw.get('error'):raise ValueError('SNAPSHOT_NATIVE_INCOMPLETE:'+str(raw.get('error')))
    unit=raw['unit']
    if unit['role']!='SOURCE_DTO_UNIT_DIAGNOSTIC_031':raise ValueError('SNAPSHOT_UNIT_ROLE')
    if unit.get('sourceReferencesUnchanged') is not True or unit.get('initialSourceBefore')!=unit.get('initialSourceAfter') or unit.get('baselineSourceBefore')!=unit.get('baselineSourceAfter') or any(type(unit.get(k)) is not str or not re.fullmatch('[0-9a-f]{64}',unit[k]) for k in ('initialSourceBefore','initialSourceAfter','baselineSourceBefore','baselineSourceAfter')):raise ValueError('SNAPSHOT_SOURCE_CHANGED_DURING_CAPTURE')
    initial=snapshot(unit['initial']);base=snapshot(unit['baseline']);after=snapshot(unit['afterSourceMutation'])
    owner_anchors(spec,raw,(initial,base,after))
    if unit['frozenBefore']!=unit['baseline'] or unit['frozenAfter']!=unit['frozenBefore']:raise ValueError('SNAPSHOT_ALIAS_AFTER_CAPTURE')
    fields=expected_fields(base)
    if len(unit['checkedFields'])!=len(set(unit['checkedFields'])) or set(unit['checkedFields'])!=set(fields):raise ValueError('SOURCE_FIELD_COVERAGE')
    required=set('initial-capture-source-unchanged baseline-capture-source-unchanged independent-identical-seed-rng-allowed rng-rejected-api-preserves-internal-state fixture-pending-command fixture-invalid-event queue-known-id-copy fixture-two-survivors-with-gear capture-is-pure-and-repeatable live-and-ghost-membership dash-hit-slot-reference history-strings-copy ghost-route-copy navigation-cache-copy rng-prefix-identity all-nested-values-detached-from-source new-capture-observes-source-change wrong-thread-rejected-before-unity-access rng-supported-api-golden rng-export-does-not-alias rng-unsupported-api-does-not-advance'.split())
    required.update('field:'+name for name in fields)
    required.update(f'survivor-slot-reference:{i}' for i in range(2))
    required.update(f'{name}:{i}' for name in ('shot-owner-reference','historical-shot-hit-set') for i in range(2))
    required.update(f'wave-types-copy:{i}' for i in range(len(base['config']['waves'])))
    required.update(f'{kind}:{name}' for name in REJECTIONS for kind in ('reject','restore'))
    if len(unit['checks'])!=len(set(unit['checks'])) or set(unit['checks'])!=required:raise ValueError('SNAPSHOT_CHECK_COVERAGE')
    if len(base['team'])!=2 or len(base['enemyPool'])!=3 or base['liveEnemyIds']!=[10001,10000] or len(base['shots'])!=2 or len(base['effects'])!=2 or len(base['stash'])!=1:raise ValueError('SNAPSHOT_FIXTURE_COUNTS')
    enemy={row['id']:row for row in base['enemyPool']}
    if enemy[10001]['pos']!={'x':11.25,'y':-13.5} or enemy[10001]['dashHitSlots']!=[0,1] or enemy[10002]['hp']!=0:raise ValueError('SNAPSHOT_ENEMY_CONTENT')
    if [s['ownerSlot'] for s in base['shots']]!=[1,-1] or base['shots'][0]['hits']!=[10001,90000]:raise ValueError('SNAPSHOT_REFERENCE_HISTORY')
    if not base['effects'][0]['color']['isNull'] or base['effects'][0]['text']['isNull'] or base['effects'][1]['color']['isNull'] or not base['effects'][1]['text']['isNull']:raise ValueError('SNAPSHOT_NULL_NOT_EMPTY')
    if base['team'][0]['pos']!={'x':1.25,'y':-2.5} or base['team'][0]['items'][0]['cd']!=1.25 or base['stash'][0]['x']!=2:raise ValueError('SNAPSHOT_NONSERIALIZED_CONTENT')
    if base['navigation'][0]['nodes']!=[dict(point=dict(x=-2,y=1),safe=False),dict(point=dict(x=3,y=4),safe=True)] or base['navigation'][0]['edges']!=[dict(a=dict(x=-2,y=1),b=dict(x=3,y=4),safe=True)] or after['navigation'][0]['nodes'][1]['safe'] is not False:raise ValueError('SNAPSHOT_CACHE_VALUES')
    if base['routes'][0]['points']!=[dict(x=2,y=3),dict(x=5,y=7)] or after['routes'][0]['points'][-1]!=dict(x=17,y=19):raise ValueError('SNAPSHOT_ROUTE_ORDER')
    if base['routes'][0]['enemyId']!=10002 or base['routes'][0]['index']!=1 or len(base['routes'][0]['points'])!=2 or base['navigation'][0]['lastVisited']!=7 or len(base['navigation'][0]['nodes'])!=2 or len(base['navigation'][0]['edges'])!=1:raise ValueError('SNAPSHOT_CACHE_CONTENT')
    if after['enemyPool'][0]['pos']!={'x':14.25,'y':-9.5} or after['team'][0]['items'][0]['cd']!=6.25 or after['stash'][0]['x']!=3 or after['rng']['count']!=base['rng']['count']+1 or after['navigation'][0]['lastVisited']!=9 or len(after['routes'][0]['points'])!=3:raise ValueError('SNAPSHOT_SOURCE_MUTATION_NOT_OBSERVED')
    if after['team'][0]['cells'][0]==base['team'][0]['cells'][0] or after['config']['weights'][0]['weight']!=base['config']['weights'][0]['weight']+2 or after['config']['waves'][0]['types'][0]['value']!='changed' or after['history'][0]['revived'][-1]['value']!='changed' or after['effects'][0]['text']['value']!='changed' or after['shots'][0]['hits']!=[10001,90000,99999] or after['enemyPool'][0]['dashHitSlots']!=[]:raise ValueError('SNAPSHOT_NESTED_MUTATION_NOT_OBSERVED')
    if base['protocol']['pending'][0]['value']!=0 or after['protocol']['pending'][0]['value']!=1 or base['protocol']['events'][0]['result']['value']!='INVALID_ENVELOPE' or after['protocol']['events'][0]['result']['value']!='changed' or [r['value'] for r in base['protocol']['knownCommandIds']]!=['snapshot-pending'] or [r['value'] for r in after['protocol']['knownCommandIds']]!=['snapshot-more-id','snapshot-pending']:raise ValueError('SNAPSHOT_PROTOCOL_COPY')
    if len(initial['enemyPool']) or len(initial['shots']) or len(initial['effects']) or len(initial['history']):raise ValueError('SNAPSHOT_INITIAL_CONTENT')
    for value in (initial,base,after):
        if value['contentHash']!=base['contentHash'] or value['geometryHash']!=base['geometryHash']:raise ValueError('SNAPSHOT_IMMUTABLE_HASH')
        for key,seed in [('rng',spec['seed']),('lootRng',spec['lootSeed'])]:
            rng=value[key]
            if rng['seed']!=seed or rng['count']!=len(rng['calls']) or [r['sequence'] for r in rng['calls']]!=list(range(rng['count'])):raise ValueError('SNAPSHOT_RNG_PREFIX')
            text=';'.join(str(r['sequence'])+':'+r['method']+':'+r['args']+':'+r['response'] for r in rng['calls'])
            if hashlib.sha256(text.encode()).hexdigest()!=rng['traceHash']:raise ValueError('SNAPSHOT_RNG_HASH')
    return dict(status='PASS',scope=ROLE,source_fields=len(fields),checks=len(required),expected_rejections=len(REJECTIONS),deep_copy='PASS',g2='NOT_RUN',g3='NOT_RUN')

def evidence_counterexamples(spec,raw,sha):
    """Isolated semantic counterexamples, not native FI/G2 execution."""
    judge(spec,raw,sha)
    tests=[]
    def add(id,expected,mutation,new_spec=None):tests.append((id,expected,mutation,new_spec))
    def snapshot_edit(value,edit):
        data=strict_loads(value['unit']['baseline']);edit(data);encoded=json.dumps(data,ensure_ascii=False,allow_nan=False)
        value['unit']['baseline']=encoded;value['unit']['frozenBefore']=encoded;value['unit']['frozenAfter']=encoded
    add('SOURCE_WITNESS_CHANGED','SNAPSHOT_SOURCE_CHANGED_DURING_CAPTURE',lambda v:v['unit'].update(baselineSourceAfter='0'*64))
    add('UNKNOWN_SCHEMA','SNAPSHOT_SCHEMA',lambda v:snapshot_edit(v,lambda d:d.update(schema_version=2)))
    add('MISSING_ENEMY_TIMER','SNAPSHOT_OBJECT_FIELDS',lambda v:snapshot_edit(v,lambda d:d['enemyPool'][0].pop('cd')))
    add('UNKNOWN_SHOT_OWNER','SNAPSHOT_SHOT_OWNER',lambda v:snapshot_edit(v,lambda d:d['shots'][0].update(ownerSlot=99)))
    add('MISSING_GHOST_ENTITY','SNAPSHOT_ROUTE_REFERENCE',lambda v:snapshot_edit(v,lambda d:d.update(enemyPool=[e for e in d['enemyPool'] if e['id']!=10002])))
    add('REORDERED_LIVE_LIST','SNAPSHOT_FIXTURE_COUNTS',lambda v:snapshot_edit(v,lambda d:d['liveEnemyIds'].reverse()))
    add('NULL_CHANGED_TO_EMPTY','SNAPSHOT_NULL_NOT_EMPTY',lambda v:snapshot_edit(v,lambda d:d['effects'][0]['color'].update(isNull=False)))
    add('DUPLICATE_GEAR_ID','SNAPSHOT_GEAR_IDS',lambda v:snapshot_edit(v,lambda d:d['stash'][0].update(id=d['team'][0]['items'][0]['id'])))
    add('MISSING_PRIVATE_TIMER','SNAPSHOT_SCALAR_COVERAGE',lambda v:snapshot_edit(v,lambda d:d.update(game=[r for r in d['game'] if r['name']!='spawnClock'])))
    add('FLOAT_OVERFLOW','SNAPSHOT_NUMBER',lambda v:snapshot_edit(v,lambda d:d['enemyPool'][0]['pos'].update(x=1e100)))
    add('WRONG_UNIT_INSTANCE','SNAPSHOT_OWNER_ANCHOR',lambda v:v.update(instanceId=v['instanceId']+1))
    add('WRONG_SCOPE_PATH','SNAPSHOT_INPUT_SCOPE_ANCHOR',lambda v:v.update(inputScopeEvidence=str(m.ROOT/'output/wrong.json')))
    add('WRONG_RNG_HASH','SNAPSHOT_RNG_HASH',lambda v:snapshot_edit(v,lambda d:d['rng'].update(traceHash='0'*64)))
    add('UNSUPPORTED_RNG_API','SNAPSHOT_RNG_METHOD',lambda v:snapshot_edit(v,lambda d:d['rng']['calls'][0].update(method='NextBytes')))
    add('DROPPED_NATIVE_CHECK','SNAPSHOT_CHECK_COVERAGE',lambda v:v['unit']['checks'].remove('rng-supported-api-golden'))
    add('DROPPED_SOURCE_FIELD_CHECK','SOURCE_FIELD_COVERAGE',lambda v:v['unit']['checkedFields'].pop())
    add('MUTATED_PAST_DTO','SNAPSHOT_ALIAS_AFTER_CAPTURE',lambda v:v['unit'].update(frozenAfter='mutated'))
    add('UNKNOWN_COMPARISON_POLICY','SNAPSHOT_COMPARISON_POLICY',lambda v:snapshot_edit(v,lambda d:d.update(comparisonPolicy='ignore-all')))
    add('WRONG_WRAPPER_RUN','SNAPSHOT_RAW_IDENTITY',lambda v:v.update(run='0'*32))
    add('UNFINISHED_NATIVE','SNAPSHOT_NATIVE_INCOMPLETE',lambda v:v.update(complete=False))
    fresh=dict(spec,run='e'*32,nonce='f'*32)
    def stale(value):
        value.update(run=fresh['run'],nonce=fresh['nonce'],inputScopeEvidence=str(m.ROOT/'output/map-full-026/input-scopes'/(fresh['run']+'-'+fresh['nonce']+'-'+'c'*32+'.json')))
    add('OLD_UNIT_IN_FRESH_WRAPPER','SNAPSHOT_OWNER_ANCHOR',stale,fresh)
    results=[]
    for id,expected,mutation,case_spec in tests:
        value=copy.deepcopy(raw);mutation(value)
        try:judge(case_spec or spec,value,sha)
        except ValueError as error:
            if expected not in str(error):raise ValueError('COUNTEREXAMPLE_WRONG_DETECTOR:'+id+':'+str(error)) from error
            results.append(dict(id=id,detection='PASS',expected=expected,observed=str(error)))
        else:raise ValueError('COUNTEREXAMPLE_NOT_DETECTED:'+id)
    return dict(scope='SNAPSHOT_EVIDENCE_NEGATIVE_COPIES_ONLY_031',status='PASS',normal_control='PASS',native_execution='NOT_RUN_BY_COUNTEREXAMPLES',g2_fi='NOT_RUN',cases=results)

def safe_output(path,root=None):
    root=Path(root or m.ROOT).absolute();path=Path(path).absolute()
    if not path.is_relative_to(root) or '..' in path.parts:raise ValueError('SNAPSHOT_OUTPUT_ESCAPE')
    return reject_symlinks(path)
def write_owned(path,value):
    safe_output(path);m.write(path,value)
def read_native_buffer(path,receipt):
    if type(receipt.get('bytes')) is not int or not 0<=receipt['bytes']<=64*1024*1024:raise ValueError('SNAPSHOT_RECEIPT_BYTES')
    raw=read_input_bytes(safe_output(path),64*1024*1024)
    if len(raw)!=receipt['bytes'] or hashlib.sha256(raw).hexdigest()!=receipt.get('sha256'):raise ValueError('SNAPSHOT_NATIVE_RECEIPT_HASH')
    return raw
def native_unchanged(path,raw):
    if read_input_bytes(safe_output(path),64*1024*1024)!=raw:raise ValueError('SNAPSHOT_NATIVE_EVIDENCE_CHANGED')
def scope_path(spec,raw):
    value=raw.get('inputScopeEvidence')
    if type(value) is not str or not Path(value).is_absolute():raise ValueError('SNAPSHOT_INPUT_SCOPE_ANCHOR')
    path=safe_output(value);base=m.ROOT/'output/map-full-026/input-scopes'
    if path.parent!=base or not re.fullmatch(re.escape(spec['run']+'-'+spec['nonce'])+'-[0-9a-f]{32}\\.json',path.name):raise ValueError('SNAPSHOT_INPUT_SCOPE_ANCHOR')
    return path
def owner_anchors(spec,raw,values):
    integer(raw.get('instanceId'))
    if raw['instanceId']==0:raise ValueError('SNAPSHOT_INSTANCE_ANCHOR')
    scope_path(spec,raw);queue_nonce=None
    for value in values:
        protocol={r['name']:r for r in value['protocol']['scalars']}
        if protocol['mapUnitRegisteredOwner']['text']!=dict(isNull=False,value=spec['run']+':'+spec['nonce']) or protocol['mapUnitRegisteredInstance']['integer']!=raw['instanceId']:raise ValueError('SNAPSHOT_OWNER_ANCHOR')
        nonce=protocol['mapQueueNonce']['text']
        if nonce['isNull'] or not re.fullmatch('[0-9a-f]{32}',nonce['value']) or queue_nonce is not None and queue_nonce!=nonce['value']:raise ValueError('SNAPSHOT_QUEUE_ANCHOR')
        queue_nonce=nonce['value']
        if not protocol['mapUnitScope']['flag'] or not protocol['mapCommandPermit']['flag'] or protocol['mapCommandConsuming']['flag']:raise ValueError('SNAPSHOT_UNIT_TOKEN_ANCHOR')
def scope_link(spec,raw,rows):
    if len(rows)!=1 or m.ROOT/rows[0]['path']!=scope_path(spec,raw):raise ValueError('SNAPSHOT_INPUT_SCOPE_LINK')

def run():
    safe_output(BASE).mkdir(parents=True,exist_ok=True);uid,nonce=uuid.uuid4().hex,uuid.uuid4().hex;out=BASE/('snapshot-031-'+uid);safe_output(out).mkdir();u=b.Unity(out)
    sources=m.source_hashes();pending=m.BASE/'unity-pending.json';request=out/'request.json';raw_path=out/'native.json'
    spec=dict(schema_version=1,document_role='SNAPSHOT_UNIT_REQUEST_031',run=uid,nonce=nonce,seed=31,lootSeed=37)
    write_owned(request,spec);write_owned(out/'manifest.json',dict(scope=ROLE,request_sha256=m.digest(request),sources=sources,expected_case_ids=['SOURCE-DTO-CONTENT-AND-COUNTEREXAMPLES'],g2='NOT_RUN',g3='NOT_RUN'))
    report=dict(status='BLOCKED',scope=ROLE,run_id=uid,nonce=nonce,time=datetime.now(ZoneInfo('Asia/Seoul')).isoformat(),g2='NOT_RUN',g3='NOT_RUN',sources=sources)
    before=saved=pid=None;native_bytes=None;raw=None;native_sha=None;verified_path=out/'native-verified.json'
    with m.editor_lock():
        if pending.exists():raise RuntimeError('UNFINISHED_OWNED_SESSION')
        try:
            u.ready();console=u.cli(['command','console','get','--caller','plugin','--skill','ds-workflow','--','--tail','1','--level','error'])['data']['result'];write_owned(out/'console-before.json',console)
            saved=u.session('Snapshot');pid=u.session('Ownership')['pid'];write_owned(out/'snapshot.json',saved)
            before={name:m.tree_hashes(b.PROJECT/name) for name in ('Assets','Packages','ProjectSettings')};write_owned(out/'target-before.json',before)
            write_owned(pending,dict(run_id=uid,nonce=nonce,snapshot=str(out/'snapshot.json'),pid=os.getpid(),scope=ROLE))
            u.session('Prepare',uid,nonce);u.session('StartOwned',uid,nonce);u.ready()
            result=u.evaluate('return DeadSurvivor.Editor.DSMapSnapshotVerification.Begin('+','.join(json.dumps(str(x)) for x in (request,raw_path,m.digest(request)))+');')
            receipt=strict_loads(result);keys(receipt,'role path sha256 run nonce bytes instanceId')
            if receipt['role']!='SNAPSHOT_NATIVE_RECEIPT_031' or receipt['path']!=str(raw_path) or any(receipt[k]!=spec[k] for k in ('run','nonce')):raise ValueError('SNAPSHOT_RECEIPT_IDENTITY')
            native_bytes=read_native_buffer(raw_path,receipt);native_sha=hashlib.sha256(native_bytes).hexdigest();raw=strict_loads(native_bytes.decode('utf-8'))
            if raw.get('instanceId')!=receipt['instanceId']:raise ValueError('SNAPSHOT_RECEIPT_INSTANCE')
            safe_output(verified_path)
            with verified_path.open('xb') as stream:stream.write(native_bytes);stream.flush();os.fsync(stream.fileno())
            report['native_receipt']=receipt;report['judged_native_sha256']=native_sha
            report['judgment']=judge(spec,raw,m.digest(request))
            report['evidence_counterexamples']=evidence_counterexamples(spec,raw,m.digest(request))
            write_owned(out/'evidence-counterexamples.json',report['evidence_counterexamples'])
            logs=u.cli(['command','console','get','--caller','plugin','--skill','ds-workflow','--','--tail','100','--level','error'])['data']['result'];write_owned(out/'console-after.json',logs)
            if logs.get('session')!=console.get('session') or logs.get('dropped') or any(e['seq']>console['cursor'] for e in logs.get('entries',[])) or logs.get('groundTruth',{}).get('consoleErrors',0):raise ValueError('SNAPSHOT_CONSOLE_ERRORS')
            report['status']='PASS'
        except Exception as error:report.update(status='FAIL' if isinstance(error,(ValueError,AssertionError)) else 'BLOCKED',error=str(error))
        finally:
            try:
                if pending.exists():m.cleanup_owned(u.session,u.ready,uid,nonce,str(out/'snapshot.json'),saved,pid)
                if before is not None:
                    def release():
                        if native_bytes is not None:
                            native_unchanged(raw_path,native_bytes);native_unchanged(verified_path,native_bytes)
                        if raw is not None and raw.get('inputScopeEvidence'):scope_link(spec,raw,verified_scopes(b.PROJECT,uid,nonce,required=True))
                        return m.finish_owned_files(b.PROJECT,[],before,lambda:sources==m.source_hashes(),pending if pending.exists() else None,None)
                    report['input_scopes'],after=verify_then_release(b.PROJECT,uid,nonce,release,required=report['status']=='PASS',expected_count=1 if report['status']=='PASS' else None)
                    write_owned(out/'target-after.json',after)
                report['cleanup']='PASS'
            except Exception as error:report.update(status='BLOCKED',cleanup='BLOCKED',cleanup_error=str(error))
            report['finished_at']=datetime.now(ZoneInfo('Asia/Seoul')).isoformat();artifacts={};total=0
            try:
                for path in out.rglob('*'):
                    if not path.is_file() or path.name=='summary.json':continue
                    value=read_input_bytes(safe_output(path),64*1024*1024);total+=len(value)
                    if total>128*1024*1024:raise ValueError('SNAPSHOT_TOTAL_EVIDENCE_LIMIT')
                    artifacts[str(path.relative_to(out))]=hashlib.sha256(value).hexdigest()
                if native_sha is not None and any(artifacts.get(name)!=native_sha for name in ('native.json','native-verified.json')):raise ValueError('SNAPSHOT_FINAL_NATIVE_HASH')
            except Exception as error:report.update(status='BLOCKED',evidence_error=str(error))
            report['artifacts']=artifacts;write_owned(out/'summary.json',report)
    print(json.dumps(dict(status=report['status'],report=str(out/'summary.json'),error=report.get('error'),cleanup=report.get('cleanup'))));return 0 if report['status']=='PASS' else 1 if report['status']=='FAIL' else 2

if __name__=='__main__':raise SystemExit(run())
