#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace DeadSurvivor.Editor
{
    public static class DSMapBodyVerification
    {
        public static string BoundChecks(DSGame game,string run,string nonce)
        {
            var checks=new System.Collections.Generic.List<string>();
            void Check(bool value,string id){if(!value)throw new Exception("BODY_CHECK:"+id);checks.Add(id);}
            void Reject(Action action,string expected,string id){bool rejected=false;try{action();}catch(Exception e){rejected=e.Message.Contains(expected);}Check(rejected,id);}
            game.RunOwnedMapUnits(run,nonce,()=>{
                var session=game.MapSession;
                foreach(int count in Enumerable.Range(1,4))Check(DSMapBodyProfile.Default8.Supports(count)&&DSMapBodyProfile.Default8.CopyRadii(count).All(r=>r==8),"default8-count-"+count);
                var values=new[]{.25f,1f};var profile=DSMapBodyProfile.Fixture(values);values[0]=99;var copy=profile.CopyRadii(2);copy[0]=77;Check(profile.Radius(0,2)==.25f&&profile.CenterRadius==1,"radii-owned-copy");
                Reject(()=>DSMapBodyProfile.Fixture(new[]{float.NaN}),"INVALID","nonfinite-radius");Reject(()=>DSMapBodyProfile.Fixture(new[]{0f}),"INVALID","zero-radius");
                Reject(()=>DSMapBodyProfile.Fixture(new[]{1025f}),"INVALID","oversize-radius");
                Reject(()=>profile.CopyRadii(1),"MISMATCH","slot-count");
                Reject(()=>session.Bind(game),"ALREADY_ATTEMPTED","bind-once");
                Reject(()=>session.PrepareOwnedFixture(run,nonce,game,session.Geometry.Boundary,session.Geometry.Obstacles,new[]{1f},.001f,Vector2.zero),"SETUP_PHASE","setup-after-bind");
                Vector3 before=session.World(new Vector2(3,5));float world=session.Data.world_scale,scale=session.ImageScale,elevation=session.Elevation;var start=session.StartCenter;var camera=session.ViewCamera;var visibility=session.Visibility;
                try
                {
                    session.Data.world_scale=999;session.ImageScale=999;session.Elevation=1;session.StartCenter=new Vector2(999,999);session.ViewCamera=null;session.Visibility=null;
                    session.Frame(game.Center);game.World.Render();
                    Check(session.World(new Vector2(3,5))==before&&session.BoundCamera==camera&&session.BoundElevation==elevation,"bound-values-not-authoring-data");
                }
                finally{session.Data.world_scale=world;session.ImageScale=scale;session.Elevation=elevation;session.StartCenter=start;session.ViewCamera=camera;session.Visibility=visibility;}
                if(session.IsFixture)
                {
                    var selected=game.Selected.ToArray();
                    try
                    {
                        game.Selected=Enumerable.Range(0,selected.Length==1?2:1).ToList();string snapshot=game.CaptureMapFrameState();game.StartRun();Check(game.CaptureMapFrameState()==snapshot,"count-rejected-before-start-mutation");
                    }
                    finally{game.Selected=selected.ToList();}
                    Reject(()=>game.CaptureFullMapSnapshot(),"UNSUPPORTED_PROFILE_031","snapshot031-blocks-fixture");
                }
                else Check(game.CaptureFullMapSnapshot().binding.bodyRadius==8,"snapshot031-default-compatible");
            });
            return JsonUtility.ToJson(new DSMapBlockoutVerification.UnitRecord{status="PASS",count=checks.Count,checks=checks.ToArray()});
        }
        public static string ColdRequired(string run,string nonce)
        {
            var root=DSMapMotionVerification.CreateExternalRoot(run,nonce,"Blockout018_"+nonce);
            var child=new GameObject("ColdRequired032");child.transform.SetParent(root.transform,false);var game=child.AddComponent<DSGame>();game.enabled=false;game.RequireMap=true;
            DSMapMotionVerification.GuardExternalRoot(run,nonce,root,()=>{if(!game||game.transform.root!=root.transform)throw new Exception("COLD_OWNER_LOST");},()=>{if(game)game.enabled=false;});
            root.SetActive(true);
            return JsonUtility.ToJson(new ColdRecord{error=game.MapCommandError,disabled=!game.enabled,noLegacy=game.World==null&&game.UI==null&&game.Data==null,instance=game.GetInstanceID()});
        }
        [Serializable] class ColdRecord {public string error;public bool disabled,noLegacy;public int instance;}
    }
}
#endif
