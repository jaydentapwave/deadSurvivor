using System;
using System.Linq;
using UnityEngine;
namespace DeadSurvivor
{
    // Opt-in development binding. DSGame alone owns party and battle state.
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
        public void Bind(DSGame game)
        {
            if(owner!=null)throw new InvalidOperationException("MAP_ALREADY_BOUND");
            if(!Layout || DSMapBlockoutPreview.Hash(Layout.text)!=ExpectedDataSha || !ViewCamera || !StaticGeometry || !RockHolder)throw new InvalidOperationException("MAP_BINDING_INVALID");
            Data=JsonUtility.FromJson<DSBlockoutData>(Layout.text);Data.Validate();Geometry=Data.Geometry(ImageScale);
            StaticGeometry.localScale=new Vector3(ImageScale/Data.build_scale,1,ImageScale/Data.build_scale);
            RockHolder.localPosition=World(Data.Logical(Data.rock_center_px,ImageScale))+RockPlacementOffset;
            if(!DSMapPartyRules.CanStart(Geometry,StartCenter,4))throw new InvalidOperationException("MAP_START_BLOCKED");
            owner=game;
        }
        public Vector3 World(Vector2 p)=>DSBlockoutMesh.World(p,Data.world_scale);
        public void Frame(Vector2 center)
        {
            ViewCamera.orthographic=true;ViewCamera.orthographicSize=6;ViewCamera.nearClipPlane=.1f;ViewCamera.farClipPlane=500;
            ViewCamera.transform.rotation=Quaternion.Euler(Elevation,0,0);
            ViewCamera.transform.position=World(center)-ViewCamera.transform.forward*150;
        }
    }
    public static class DSMapPartyRules
    {
        public static bool CanStart(DSMapGeometry map,Vector2 center,int count)
        {
            if(count<1 || count>4 || !map.SafeSweep(center,center,8))return false;
            return Enumerable.Range(0,count).All(i=>{var p=center+DSGame.FormationOffset(0,i,count);return map.SafeSweep(p,p,8);});
        }
        public static bool CanTransition(DSMapGeometry map,Vector2 center,int current,int next,Survivor[] team,int revive=-1)
        {
            if(team.Length<1 || team.Length>4 || current<0 || current>2 || next<0 || next>2 || !map.SafeSweep(center,center,8))return false;
            for(int i=0;i<team.Length;i++)
            {
                var old=center+DSGame.FormationOffset(current,i,team.Length);var target=center+DSGame.FormationOffset(next,i,team.Length);
                if(!map.SafeSweep(old,target,8))return false;
                if((team[i].hp>0 || i==revive) && !map.SafeSweep(i==revive?old:team[i].pos,target,8))return false;
            }
            return true;
        }
    }
}
