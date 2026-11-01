import copy,hashlib,json,unittest,tempfile
from pathlib import Path
from unittest.mock import patch
import map_snapshot_run as h

def txt(value):return dict(isNull=value is None,value=value or '')
def scalar_rows(types):
    rows=[]
    for name,kind in types.items():
        row=dict(name=name,kind=kind,integer=0,longInteger=0,real=0,doubleReal=0,flag=False,vector=dict(x=0,y=0),text=txt(None))
        if name in ('mapRequired','VerificationRun'):row['flag']=True
        if name=='State':row['text']=txt('lobby')
        rows.append(row)
    return rows

def minimal():
    value={key:[] for key in h.ROOT_KEYS}
    value.update(schema_version=1,document_role='FULL_MAP_SNAPSHOT_031',contentHash='a'*64,geometryHash='b'*64,comparisonPolicy='031/r001-game-protocol-render-separated',binding=dict(mapMode='MapRequired',layoutHash='c'*64,sourceLayoutHash='d'*64,sourceRole='PREVIEW_ONLY',imageScale=4,worldScale=.02,bodyRadius=8,elevation=55,epsilon=.001,coordinateLimit=16384,startCenter=dict(x=0,y=0)),game=scalar_rows(h.GAME_TYPES),renderMetadata=scalar_rows(h.RENDER_TYPES),config=dict(adaptiveDrops=True,duration=30,speed=1,countScale=.49,drops=0,weights=[],waves=[]),protocol=dict(scalars=scalar_rows(h.PROTOCOL_TYPES),pending=[],knownCommandIds=[],events=[]))
    for key in ('rng','lootRng'):value[key]=dict(seed=31,count=0,traceHash=hashlib.sha256(b'').hexdigest(),calls=[])
    return value
class SnapshotSchemaTests(unittest.TestCase):
    def setUp(self):self.data=minimal()
    def check(self):return h.snapshot(json.dumps(self.data))
    def test_minimal_complete_schema(self):self.assertEqual(self.check()['document_role'],'FULL_MAP_SNAPSHOT_031')
    def test_unknown_root_field(self):
        self.data['extra']=1
        with self.assertRaisesRegex(ValueError,'SCHEMA'):self.check()
    def test_missing_render_field(self):
        self.data['renderMetadata'].pop()
        with self.assertRaisesRegex(ValueError,'SCALAR_COVERAGE'):self.check()
    def test_null_scalar_row(self):
        self.data['game'][0]=None
        with self.assertRaisesRegex(ValueError,'SCALAR_ROW'):self.check()
    def test_wrong_scalar_type(self):
        self.data['game'][0]['kind']='System.String'
        with self.assertRaisesRegex(ValueError,'SCALAR_KIND'):self.check()
    def test_boolean_is_not_integer(self):
        self.data['game'][0]['integer']=True
        with self.assertRaisesRegex(ValueError,'INTEGER'):self.check()
    def test_mode_fallback_rejected(self):
        next(r for r in self.data['game'] if r['name']=='mapRequired')['flag']=False
        with self.assertRaisesRegex(ValueError,'MAP_REQUIRED'):self.check()
    def test_nonfinite_rejected(self):
        self.data['binding']['worldScale']=float('nan')
        with self.assertRaises(ValueError):self.check()
    def test_missing_effect_field(self):
        self.data['effects']=[dict(pos=dict(x=0,y=0),life=1,max=1,r=1,color=txt(None))]
        with self.assertRaisesRegex(ValueError,'OBJECT_FIELDS'):self.check()
    def test_null_and_empty_are_distinct(self):
        self.data['effects']=[dict(pos=dict(x=0,y=0),life=1,max=1,r=1,color=txt(None),text=txt(''))]
        value=self.check()['effects'][0];self.assertNotEqual(value['color'],value['text'])
    def test_invalid_null_encoding(self):
        self.data['renderMetadata'][0]['text']=dict(isNull=True,value='hidden')
        with self.assertRaisesRegex(ValueError,'TEXT'):self.check()
    def test_unknown_comparison_policy(self):
        self.data['comparisonPolicy']='ignore-everything'
        with self.assertRaisesRegex(ValueError,'COMPARISON_POLICY'):self.check()
    def test_unknown_rng_api(self):
        self.data['rng'].update(count=1,calls=[dict(sequence=0,method='NextBytes',args='',response='0')])
        with self.assertRaisesRegex(ValueError,'RNG_METHOD'):self.check()
    def test_rng_nan_response(self):
        self.data['rng'].update(count=1,calls=[dict(sequence=0,method='NextDouble',args='',response='NaN')])
        with self.assertRaisesRegex(ValueError,'RNG_RESPONSE'):self.check()
    def test_duplicate_nav_node(self):
        node=dict(point=dict(x=1,y=2),safe=True);self.data['navigation']=[dict(radius=1,lastVisited=1,nodes=[node,node],edges=[])]
        with self.assertRaisesRegex(ValueError,'CACHE_KEYS'):self.check()
    def test_duplicate_json_key(self):
        with self.assertRaises(ValueError):h.snapshot('{"schema_version":1,"schema_version":1}')
    def test_unknown_original_identity(self):
        with self.assertRaisesRegex(ValueError,'RAW_IDENTITY'):h.judge(dict(run='a',nonce='b'),dict(role=h.ROLE,run='wrong',nonce='b',requestSha='hash'),'hash')
class SnapshotParentTests(unittest.TestCase):
    def owned(self):
        spec=dict(run='a'*32,nonce='b'*32);value=minimal();rows={r['name']:r for r in value['protocol']['scalars']}
        rows['mapUnitRegisteredOwner']['text']=txt(spec['run']+':'+spec['nonce']);rows['mapUnitRegisteredInstance']['integer']=-123
        rows['mapQueueNonce']['text']=txt('d'*32);rows['mapUnitScope']['flag']=True;rows['mapCommandPermit']['flag']=True
        path=h.m.ROOT/'output/map-full-026/input-scopes'/(spec['run']+'-'+spec['nonce']+'-'+'c'*32+'.json')
        return spec,dict(instanceId=-123,inputScopeEvidence=str(path)),value
    def test_fresh_owner_anchor(self):
        spec,raw,value=self.owned();h.owner_anchors(spec,raw,[value,copy.deepcopy(value)])
    def test_stale_unit_payload(self):
        spec,raw,value=self.owned();next(r for r in value['protocol']['scalars'] if r['name']=='mapUnitRegisteredOwner')['text']=txt('old:owner')
        with self.assertRaisesRegex(ValueError,'OWNER_ANCHOR'):h.owner_anchors(spec,raw,[value])
    def test_wrong_instance(self):
        spec,raw,value=self.owned();raw['instanceId']=-124
        with self.assertRaisesRegex(ValueError,'OWNER_ANCHOR'):h.owner_anchors(spec,raw,[value])
    def test_queue_nonce_changed(self):
        spec,raw,value=self.owned();other=copy.deepcopy(value);next(r for r in other['protocol']['scalars'] if r['name']=='mapQueueNonce')['text']=txt('e'*32)
        with self.assertRaisesRegex(ValueError,'QUEUE_ANCHOR'):h.owner_anchors(spec,raw,[value,other])
    def test_scope_path_and_verified_journal_link(self):
        spec,raw,value=self.owned();relative=str(Path(raw['inputScopeEvidence']).relative_to(h.m.ROOT));h.scope_link(spec,raw,[dict(path=relative)])
        with self.assertRaisesRegex(ValueError,'SCOPE_LINK'):h.scope_link(spec,raw,[dict(path=relative+'.wrong')])
        raw['inputScopeEvidence']=str(h.m.ROOT/'output/foreign.json')
        with self.assertRaisesRegex(ValueError,'SCOPE_ANCHOR'):h.owner_anchors(spec,raw,[value])
    def test_symlink_base_rejected_before_external_write(self):
        with tempfile.TemporaryDirectory(dir=h.BASE) as temp:
            base=Path(temp);project=base/'project';outside=base/'outside';(project/'output').mkdir(parents=True);outside.mkdir();link=project/'output/map-full-026';link.symlink_to(outside,target_is_directory=True)
            with patch.object(h,'BASE',link),patch.object(h.m,'ROOT',project):
                with self.assertRaises(ValueError):h.run()
            self.assertEqual(list(outside.iterdir()),[])
    def test_native_receipt_and_late_change(self):
        with tempfile.TemporaryDirectory(dir=h.BASE) as temp:
            path=Path(temp)/'native.json';payload=b'{"value":1}';path.write_bytes(payload);receipt=dict(bytes=len(payload),sha256=hashlib.sha256(payload).hexdigest())
            read=h.read_native_buffer(path,receipt);self.assertEqual(read,payload);h.native_unchanged(path,read)
            path.write_bytes(b'{"value":2}')
            with self.assertRaisesRegex(ValueError,'EVIDENCE_CHANGED'):h.native_unchanged(path,read)
            with self.assertRaisesRegex(ValueError,'RECEIPT_HASH'):h.read_native_buffer(path,receipt)
if __name__=='__main__':unittest.main()
