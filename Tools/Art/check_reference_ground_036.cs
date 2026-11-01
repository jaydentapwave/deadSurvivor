using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using DeadSurvivor;
using Newtonsoft.Json;
public static class ReferenceGroundPlay036
{
 const string Out="output/reference-ground-036/20261102-r001";
 const string Scene="Assets/DeadSurvivor/Scenes/Map036_ReferenceGround_r003/Map036_ReferenceGround.unity";
 sealed class Vectors:JsonConverter { public override bool CanConvert(Type t)=>t==typeof(Vector2)||t==typeof(Vector3);public override void WriteJson(JsonWriter w,object o,JsonSerializer s){w.WriteStartObject();if(o is Vector2 v){w.WritePropertyName("x");w.WriteValue(v.x);w.WritePropertyName("y");w.WriteValue(v.y);}else{var v3=(Vector3)o;w.WritePropertyName("x");w.WriteValue(v3.x);w.WritePropertyName("y");w.WriteValue(v3.y);w.WritePropertyName("z");w.WriteValue(v3.z);}w.WriteEndObject();}public override object ReadJson(JsonReader r,Type t,object o,JsonSerializer s)=>throw new NotSupportedException();}
 public static object Run(string name,int count,float x,float z,string key,int steps)
 {
  if(!EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=Scene)throw new Exception("NOT_OWNED_PLAY");
  string path=Out+"/"+name+".json";if(File.Exists(path))throw new Exception("CASE_EXISTS");
  var g=UnityEngine.Object.FindAnyObjectByType<DSGame>();if(!g || g.MapSession.IsFixture || g.MapSession.BoundCombatEnabled)throw new Exception("WRONG_GAME");
  var wall=GameObject.Find("OB-004_RimBlocks").GetComponent<Renderer>();var original=wall.sharedMaterial;
  var originalInput=InputSystem.settings;var originalInputJson=JsonUtility.ToJson(originalInput);
  var clonedInput=UnityEngine.Object.Instantiate(originalInput);clonedInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;clonedInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=clonedInput;
  var prior=Keyboard.current;var kb=InputSystem.AddDevice<Keyboard>("Reference036_TrialKeyboard");kb.MakeCurrent();
  Key held=(Key)Enum.Parse(typeof(Key),key);bool drive=false,finished=false;int phase=0,last=-1;double deadline=EditorApplication.timeSinceStartup+25;
  var frames=new List<object>();Vector2 previous=Vector2.zero;Vector2[] previousParty=null,previousReserved=null;
  var center=new Vector2(x/.02f,-z/.02f);
  Action before=null;Action<InputEventPtr,InputDevice> external=null;Action<PlayModeStateChange> mode=null;string cancelled=null;EditorApplication.CallbackFunction tick=null;
  var initial=(object)null;
  void Cleanup() {drive=false;InputSystem.onBeforeUpdate-=before;InputSystem.onEvent-=external;EditorApplication.playModeStateChanged-=mode;EditorApplication.update-=tick;if(kb.added)InputSystem.RemoveDevice(kb);if(prior!=null&&prior.added)prior.MakeCurrent();if(InputSystem.settings==clonedInput)InputSystem.settings=originalInput;UnityEngine.Object.DestroyImmediate(clonedInput);g.enabled=false;g.MoveInput=Vector2.zero;g.World.Render();g.MapSession.Visibility.Refresh();}
  void Finish(string status,string error) {
   if(finished)return;finished=true;Cleanup();
   var mat=wall.sharedMaterial;var result=new{status,error,name,partyCount=count,heldKey=key,deviceId=kb.deviceId,initial,frames,
    final=new{frame=Time.frameCount,center=g.Center,party=g.Team.Select(c=>c.pos).ToArray(),input=g.MapInputObserved,elapsed=g.Elapsed,
    remnantAlpha=mat.GetColor("_BaseColor").a,remnantMaterial=mat.name,fadedTotal=g.MapSession.Visibility.FadedCount,
    cameraElevation=g.MapSession.BoundElevation,ortho=g.MapSession.ViewCamera.orthographicSize,focus=g.MapSession.Visibility.Focus},
    restoredInput=Keyboard.current==prior && InputSystem.settings==originalInput && JsonUtility.ToJson(originalInput)==originalInputJson,scene=Scene,scope="Actual normal Update with temporary virtual keyboard; pose reset only before case"};
   File.WriteAllText(path,JsonConvert.SerializeObject(result,Formatting.Indented,new JsonSerializerSettings{Converters={new Vectors()}}));
  }
  before=()=>{if(phase>=3 && kb.added && InputState.currentUpdateType==InputUpdateType.Dynamic)InputSystem.QueueStateEvent(kb,drive?new KeyboardState(held):new KeyboardState());};
  tick=()=>{
   try {
    if(!EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=Scene){Finish("FAIL","PLAY_OR_SCENE_CHANGED");return;}
    if(EditorApplication.timeSinceStartup>deadline){Finish("FAIL","TIMEOUT");return;}
    if(cancelled!=null){Finish("FAIL",cancelled);return;}
    if(g.MapCommandError!=""){Finish("FAIL","GAME:"+g.MapCommandError);return;}
    if(phase==0) {g.enabled=true;g.VerificationRun=true;if(g.State!="lobby")g.SubmitMapCommand("ReturnLobby",0,"TEST");phase=1;return;}
    if(phase==1) {if(g.State!="lobby")return;g.Selected=Enumerable.Range(0,count).ToList();g.SubmitMapCommand("StartRun",0,"TEST");phase=2;return;}
    if(phase==2) {
     if(g.State!="battle"||g.Team.Count!=count)return;
     g.enabled=false;g.Config.speed=1;g.Center=center;for(int i=0;i<count;i++)g.Team[i].pos=center+g.Offset(i);
     if(!DSMapPartyRules.CanStart(g.MapSession.Geometry,center,count,g.MapSession.BodyProfile)){Finish("FAIL","UNSAFE_INITIAL_POSE");return;}
     previous=g.Center;previousParty=g.Team.Select(c=>c.pos).ToArray();previousReserved=Enumerable.Range(0,count).Select(i=>g.Center+g.Offset(i)).ToArray();
     initial=new{frame=Time.frameCount,center=g.Center,party=previousParty,reserved=previousReserved,elapsed=g.Elapsed};
     g.World.Render();g.MapSession.Visibility.Refresh();last=g.MapInputFrame;drive=true;g.enabled=true;phase=3;return;
    }
    if(g.MapInputFrame==last)return;last=g.MapInputFrame;
    if(g.MapInputDevice!=kb.deviceId){Finish("FAIL","INPUT_DEVICE_CHANGED");return;}
    var actual=g.Team.Select(c=>c.pos).ToArray();var reserved=Enumerable.Range(0,count).Select(i=>g.Center+g.Offset(i)).ToArray();
    bool safe=g.MapSession.Geometry.SafeSweep(previous,g.Center,g.MapSession.BodyProfile.CenterRadius);
    for(int i=0;i<count;i++)safe&=g.MapSession.Geometry.SafeSweep(previousParty[i],actual[i],g.MapSession.BodyProfile.Radius(i,count))&&g.MapSession.Geometry.SafeSweep(previousReserved[i],reserved[i],g.MapSession.BodyProfile.Radius(i,count));
    frames.Add(new{phase=drive?"held":"released",frame=g.MapInputFrame,dt=Time.unscaledDeltaTime,elapsed=g.Elapsed,input=g.MapInputObserved,deviceId=g.MapInputDevice,center=g.Center,party=actual,reserved,safe,
     remnantAlpha=wall.sharedMaterial.GetColor("_BaseColor").a});
    previous=g.Center;previousParty=actual;previousReserved=reserved;
    if(!safe){Finish("FAIL","UNSAFE_SWEEP");return;}
    if(frames.Count==steps)drive=false;
    if(frames.Count>=steps+20)Finish("PASS_OBSERVATION",null);
   }catch(Exception e){Finish("FAIL",e.ToString());}
  };
  external=(evt,device)=>{if(device==null||device==kb)return;if((evt.type==StateEvent.Type||evt.type==DeltaStateEvent.Type)&&evt.EnumerateChangedControls(device).Any())cancelled="EXTERNAL_DEVICE_INPUT";};
  mode=state=>{if(state==PlayModeStateChange.ExitingPlayMode)Finish("FAIL","PLAY_STOPPED");};
  InputSystem.onEvent+=external;EditorApplication.playModeStateChanged+=mode;InputSystem.onBeforeUpdate+=before;EditorApplication.update+=tick;
  return new{status="STARTED",path,name};
 }
}
