using System;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace DeadSurvivor
{
    // Bind once. Public authoring data is not the runtime source after binding.
    public sealed class DSMapSession : MonoBehaviour
    {
        public TextAsset Layout;
        public string ExpectedDataSha;
        public float ImageScale=4,Elevation=55;
        public Vector2 StartCenter;
        public Camera ViewCamera;
        public Transform StaticGeometry,RockHolder;
        public Vector3 RockPlacementOffset;
        public DSMapVisibility Visibility;
        public bool CombatEnabled;
        public DSBlockoutData Data {get;private set;}
        public DSMapGeometry Geometry {get;private set;}
        DSGame owner;
        DSMapBoundState bound;
        bool bindingAttempted;
#if UNITY_EDITOR
        DSMapPendingFixture pendingFixture;
#endif
        public DSMapBodyProfile BodyProfile=>Bound.Bodies;
        public float BoundWorldScale=>Bound.WorldScale;
        public float BoundImageScale=>Bound.ImageScale;
        public float BoundElevation=>Bound.Elevation;
        public Vector2 BoundStartCenter=>Bound.StartCenter;
        public Camera BoundCamera=>Bound.Camera;
        public DSMapVisibility BoundVisibility=>Bound.Visibility;
        public bool BoundCombatEnabled=>Bound.CombatEnabled;
        public bool IsFixture=>Bound.Fixture;
        DSMapBoundState Bound=>bound??throw new InvalidOperationException("MAP_NOT_BOUND");
        static bool ValidCenter(Vector2 p)=>DSMapGeometry.Finite(p.x)&&DSMapGeometry.Finite(p.y)&&Math.Abs(p.x)<=16384&&Math.Abs(p.y)<=16384;
        public void Bind(DSGame game)
        {
            if(bindingAttempted)throw new InvalidOperationException("MAP_BINDING_ALREADY_ATTEMPTED");
            bindingAttempted=true;
#if UNITY_EDITOR
            var pending=pendingFixture;pendingFixture=null;
#endif
            try
            {
                if(!Layout||DSMapBlockoutPreview.Hash(Layout.text)!=ExpectedDataSha||!ViewCamera||!StaticGeometry||!RockHolder||transform.position!=Vector3.zero||transform.rotation!=Quaternion.identity||transform.lossyScale!=Vector3.one||Elevation!=45&&Elevation!=55||!ValidCenter(StartCenter))throw new InvalidOperationException("MAP_BINDING_INVALID");
                Data=JsonUtility.FromJson<DSBlockoutData>(Layout.text);Data.Validate();
                var geometry=Data.Geometry(ImageScale);var bodies=DSMapBodyProfile.Default8;var center=StartCenter;bool fixture=false;float imageScale=ImageScale;
#if UNITY_EDITOR
                if(pending!=null)
                {
                    pending.Validate(game,this);geometry=pending.Geometry;bodies=pending.Bodies;center=pending.Center;fixture=true;imageScale=1;
                }
#endif
                Geometry=geometry;
                bound=new DSMapBoundState(bodies,Data.world_scale,imageScale,Elevation,center,ViewCamera,Visibility,fixture,CombatEnabled);
                StaticGeometry.localScale=new Vector3(ImageScale/Data.build_scale,1,ImageScale/Data.build_scale);
                RockHolder.localPosition=World(Data.Logical(Data.rock_center_px,ImageScale))+RockPlacementOffset;
                owner=game;
            }
            catch{bound=null;Geometry=null;throw;}
        }
        public Vector3 World(Vector2 p)=>DSBlockoutMesh.World(p,Bound.WorldScale);
        public void Frame(Vector2 center)
        {
            var value=Bound;var camera=value.Camera;
            if(!camera)throw new InvalidOperationException("MAP_CAMERA_LOST");
            camera.orthographic=true;camera.orthographicSize=value.Fixture?1:6;camera.nearClipPlane=.1f;camera.farClipPlane=500;
            camera.transform.rotation=Quaternion.Euler(value.Elevation,0,0);
            camera.transform.position=World(center)-camera.transform.forward*150;
        }
#if UNITY_EDITOR
        public void PrepareOwnedFixture(string run,string nonce,DSGame game,Vector2[] boundary,Vector2[][] obstacles,float[] radii,double epsilon,Vector2 center)
        {
            if(bindingAttempted||pendingFixture!=null||owner!=null||!game||game.enabled||game.gameObject.activeInHierarchy||game.MapSession!=this||game.transform.root!=transform.root)throw new InvalidOperationException("MAP_FIXTURE_SETUP_PHASE");
            DSMapPendingFixture.CheckOwner(run,nonce,game,this);
            if(!ValidCenter(center))throw new ArgumentException("MAP_FIXTURE_CENTER");
            pendingFixture=new DSMapPendingFixture(run,nonce,game,this,new DSMapGeometry(boundary,obstacles,epsilon,16384),DSMapBodyProfile.Fixture(radii),center);
        }
#endif
    }
    sealed class DSMapBoundState
    {
        public readonly DSMapBodyProfile Bodies;
        public readonly float WorldScale,ImageScale,Elevation;public readonly Vector2 StartCenter;
        public readonly Camera Camera;public readonly DSMapVisibility Visibility;public readonly bool Fixture,CombatEnabled;
        public DSMapBoundState(DSMapBodyProfile bodies,float worldScale,float imageScale,float elevation,Vector2 center,Camera camera,DSMapVisibility visibility,bool fixture,bool combatEnabled)
        {Bodies=bodies;WorldScale=worldScale;ImageScale=imageScale;Elevation=elevation;StartCenter=center;Camera=camera;Visibility=visibility;Fixture=fixture;CombatEnabled=combatEnabled;}
    }
#if UNITY_EDITOR
    sealed class DSMapPendingFixture
    {
        readonly string run,nonce,settings;readonly DSGame game;readonly DSMapSession session;readonly int rootId;
        public readonly DSMapGeometry Geometry;public readonly DSMapBodyProfile Bodies;public readonly Vector2 Center;
        public DSMapPendingFixture(string run,string nonce,DSGame game,DSMapSession session,DSMapGeometry geometry,DSMapBodyProfile bodies,Vector2 center)
        {this.run=run;this.nonce=nonce;settings=JsonUtility.ToJson(session);this.game=game;this.session=session;rootId=game.transform.root.GetInstanceID();Geometry=geometry;Bodies=bodies;Center=center;}
        public void Validate(DSGame current,DSMapSession target)
        {if(current!=game||target!=session||current.enabled||JsonUtility.ToJson(target)!=settings||current.transform.root.GetInstanceID()!=rootId)throw new InvalidOperationException("MAP_FIXTURE_INSTANCE_CHANGED");CheckOwner(run,nonce,current,target);}
        public static void CheckOwner(string run,string nonce,DSGame game,DSMapSession session)
        {
            var root=game.transform.root;
            if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets"||SessionState.GetString("DS.Map017.owner","")!=run+":"+nonce||SessionState.GetString("DS.Map017.phase","")!="PLAY"||SessionState.GetInt("DS.Map017.pid",-1)!=System.Diagnostics.Process.GetCurrentProcess().Id||SessionState.GetInt("DS.Map017.case_root",0)!=root.gameObject.GetInstanceID()||root.name!="Blockout018_"+nonce||root.GetComponentsInChildren<DSGame>(true).Length!=1||session.transform.root!=root)throw new InvalidOperationException("MAP_FIXTURE_OWNERSHIP");
        }
    }
#endif
    public static class DSMapPartyRules
    {
        public static bool CanStart(DSMapGeometry map,Vector2 center,int count)=>CanStart(map,center,count,DSMapBodyProfile.Default8);
        public static bool CanStart(DSMapGeometry map,Vector2 center,int count,DSMapBodyProfile bodies)
        {
            if(map==null||bodies==null||!bodies.Supports(count)||!map.SafeSweep(center,center,bodies.CenterRadius))return false;
            return Enumerable.Range(0,count).All(i=>{var p=center+DSGame.FormationOffset(0,i,count);return map.SafeSweep(p,p,bodies.Radius(i,count));});
        }
        public static bool CanTransition(DSMapGeometry map,Vector2 center,int current,int next,Survivor[] team,int revive=-1)=>CanTransition(map,center,current,next,team,DSMapBodyProfile.Default8,revive);
        public static bool CanTransition(DSMapGeometry map,Vector2 center,int current,int next,Survivor[] team,DSMapBodyProfile bodies,int revive=-1)
        {
            if(map==null||team==null||bodies==null||!bodies.Supports(team.Length)||current<0||current>2||next<0||next>2||!map.SafeSweep(center,center,bodies.CenterRadius))return false;
            for(int i=0;i<team.Length;i++)
            {
                var old=center+DSGame.FormationOffset(current,i,team.Length);var target=center+DSGame.FormationOffset(next,i,team.Length);float radius=bodies.Radius(i,team.Length);
                if(!map.SafeSweep(old,target,radius))return false;
                if((team[i].hp>0||i==revive)&&!map.SafeSweep(i==revive?old:team[i].pos,target,radius))return false;
            }
            return true;
        }
    }
}
