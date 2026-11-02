using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace DeadSurvivor.Editor
{
    // 044: probe for the saved 3D-map main scene. Drives the real UI buttons / map command
    // queue and a virtual Touchscreen in Play mode; reads state. Verification-only; it never
    // edits the scene asset. Previous scene setup survives domain reload through SessionState.
    public static class DSProtoMain044Verification
    {
        public const string ScenePath="Assets/DeadSurvivor/Scenes/Proto044/Proto044_Main.unity";
        const string Key="DeadSurvivor.Proto044.";
        const string TouchName="Proto044Touch";
        static DSGame Game=>UnityEngine.Object.FindAnyObjectByType<DSGame>();

        [Serializable] class StateRecord
        {
            public bool playing,paused,win,joystickActive,touchDevice;public string scene,state,mapError;public int frame,wave,spawned,enemies,kills,selectedCount;
            public float elapsed,duration,totalTime;public float[] center,joystick,teamHp;public int[] selected;public string[] buttons;
        }
        [Serializable] class SetupRow {public string path;public bool isLoaded,isActive,isSubScene;}
        [Serializable] class Setup {public SetupRow[] rows;}

        static void Target(){if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets")throw new Exception("WRONG_PROJECT");}

        public static string State()
        {
            Target();var g=Game;var s=new StateRecord{playing=EditorApplication.isPlaying,scene=SceneManager.GetActiveScene().path,frame=Time.frameCount,
                touchDevice=InputSystem.devices.Any(d=>d.name.StartsWith(TouchName))};
            if(g)
            {
                s.state=g.State;s.paused=g.Paused;s.win=g.Win;s.wave=g.Wave;s.spawned=g.Spawned;s.enemies=g.Enemies.Count;s.kills=g.Kills;s.elapsed=g.Elapsed;
                s.duration=g.Config!=null&&g.Config.waves!=null&&g.Config.waves.Length>0?g.Duration:0;s.totalTime=g.TotalTime;s.center=new[]{g.Center.x,g.Center.y};s.teamHp=g.Team.Select(c=>c.hp).ToArray();
                s.selected=g.Selected.ToArray();s.selectedCount=g.Selected.Count;s.mapError=g.MapCommandError;
                if(g.UI){s.joystickActive=g.UI.JoystickActive;s.joystick=new[]{g.UI.Joystick.x,g.UI.Joystick.y};}
                s.buttons=g.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.interactable).Select(b=>string.Join("|",b.GetComponentsInChildren<TextMeshProUGUI>().Select(t=>t.text))).ToArray();
            }
            return JsonUtility.ToJson(s);
        }

        // Edit mode: remember the current scenes, refuse unsaved scenes, open the main scene.
        public static string OpenMain()
        {
            Target();if(EditorApplication.isPlaying)throw new Exception("ALREADY_PLAYING");
            for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE:"+EditorSceneManager.GetSceneAt(i).path);
            if(SessionState.GetString(Key+"setup","")!="")throw new Exception("UNFINISHED_PREVIOUS_RUN");
            // Keep the complete setup (loaded and unloaded scenes, active scene) so Restore is exact.
            var setup=EditorSceneManager.GetSceneManagerSetup();
            SessionState.SetString(Key+"setup",JsonUtility.ToJson(new Setup{rows=setup.Select(x=>new SetupRow{path=x.path,isLoaded=x.isLoaded,isActive=x.isActive,isSubScene=x.isSubScene}).ToArray()}));
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            return State();
        }
        public static string EnterPlay(){Target();if(SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("MAIN_SCENE_NOT_OPEN");EditorApplication.isPlaying=true;return "REQUESTED";}
        public static string ExitPlay(){Target();RemoveTouch();EditorApplication.isPlaying=false;return "REQUESTED";}

        // Edit mode: reopen the scenes that were open before OpenMain. Never saves the main scene.
        public static string Restore()
        {
            Target();if(EditorApplication.isPlaying)throw new Exception("STILL_PLAYING");
            var raw=SessionState.GetString(Key+"setup","");if(raw=="")return "NOTHING_TO_RESTORE";
            var s=JsonUtility.FromJson<Setup>(raw);var main=SceneManager.GetActiveScene();if(main.path==ScenePath&&main.isDirty)throw new Exception("MAIN_SCENE_DIRTY");
            if(s.rows==null||s.rows.Length==0)throw new Exception("EMPTY_SETUP");
            EditorSceneManager.RestoreSceneManagerSetup(s.rows.Select(r=>new SceneSetup{path=r.path,isLoaded=r.isLoaded,isActive=r.isActive,isSubScene=r.isSubScene}).ToArray());
            var now=EditorSceneManager.GetSceneManagerSetup().Select(x=>x.path+":"+x.isLoaded+":"+x.isActive).ToArray();
            var want=s.rows.Select(x=>x.path+":"+x.isLoaded+":"+x.isActive).ToArray();
            if(!now.SequenceEqual(want))throw new Exception("SETUP_NOT_RESTORED:"+string.Join(",",now));
            SessionState.EraseString(Key+"setup");return JsonUtility.ToJson(s);
        }

        public static string Click(string label)
        {
            var g=Game;if(!g)throw new Exception("NO_GAME");
            var b=g.GetComponentsInChildren<UnityEngine.UI.Button>(false).FirstOrDefault(x=>x.interactable&&x.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text==label));
            if(!b)throw new Exception("BUTTON_MISSING:"+label);b.onClick.Invoke();return State();
        }
        // Same queue path the UI and keyboard use (e.g. ToggleSelection, DebugFinish).
        public static string Submit(string kind,int value)
        {
            var g=Game;if(!g)throw new Exception("NO_GAME");if(!g.SubmitMapCommand(kind,value,"TEST"))throw new Exception("SUBMIT_REJECTED:"+kind);return State();
        }
        // Test injection for the defeat path: the real frames then judge the wipe.
        public static string KillParty(){var g=Game;if(!g||g.State!="battle")throw new Exception("NOT_IN_BATTLE");foreach(var c in g.Team)c.hp=0;return State();}

        public static string MovementRect()
        {
            var g=Game;var r=g.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(x=>x.name=="MovementInput");if(!r)throw new Exception("NO_MOVEMENT_INPUT");
            var c=new Vector3[4];r.GetWorldCorners(c);var canvas=r.GetComponentInParent<Canvas>();var cam=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var p=c.Select(v=>RectTransformUtility.WorldToScreenPoint(cam,v)).ToArray();
            var ci=System.Globalization.CultureInfo.InvariantCulture;float x0=p.Min(v=>v.x),y0=p.Min(v=>v.y),x1=p.Max(v=>v.x),y1=p.Max(v=>v.y);
            return string.Format(ci,"{{\"x\":{0},\"y\":{1},\"width\":{2},\"height\":{3}}}|{4}x{5}",x0,y0,x1-x0,y1-y0,Screen.width,Screen.height);
        }
        static Touchscreen TouchDevice()=>(Touchscreen)InputSystem.devices.FirstOrDefault(d=>d.name.StartsWith(TouchName))??InputSystem.AddDevice<Touchscreen>(TouchName);
        public static string Touch(string phase,float x,float y)
        {
            if(!EditorApplication.isPlaying)throw new Exception("NOT_PLAYING");
            var ts=TouchDevice();var ph=phase=="began"?UnityEngine.InputSystem.TouchPhase.Began:phase=="moved"?UnityEngine.InputSystem.TouchPhase.Moved:phase=="ended"?UnityEngine.InputSystem.TouchPhase.Ended:throw new Exception("PHASE");
            InputSystem.QueueStateEvent(ts,new TouchState{touchId=1,phase=ph,position=new Vector2(x,y),pressure=ph==UnityEngine.InputSystem.TouchPhase.Ended?0:1});
            return "{\"queued\":\""+phase+"\",\"device\":"+ts.deviceId+"}";
        }
        public static string RemoveTouch(){var n=0;foreach(var d in InputSystem.devices.Where(d=>d.name.StartsWith(TouchName)).ToArray()){InputSystem.RemoveDevice(d);n++;}return n.ToString();}
        public static string FocusGameView(){EditorApplication.ExecuteMenuItem("Window/General/Game");return "FOCUSED";}
        // Editor-only input scope (same approach as DSOwnedInputScope, without the 017 session protocol):
        // unattended CLI runs leave the editor unfocused, so the default settings drop touch input
        // (game-view focus) and disable non-background devices (background behavior). Install a clone
        // with AllDeviceInputAlwaysGoesToGameView + IgnoreFocus for the touch step, then put back the
        // original settings object. There is no InputSettings asset in this project, so nothing is saved.
        static InputSettings originalInput,cloneInput;
        public static string BeginInputScope()
        {
            if(cloneInput)throw new Exception("INPUT_SCOPE_ACTIVE");
            originalInput=InputSystem.settings;cloneInput=UnityEngine.Object.Instantiate(originalInput);cloneInput.name="Proto044Input";
            cloneInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            cloneInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            bool asset=AssetDatabase.Contains(originalInput);InputSystem.settings=cloneInput;
            foreach(var d in InputSystem.devices.Where(d=>d.name.StartsWith(TouchName)&&!d.enabled))InputSystem.EnableDevice(d);
            return "original="+originalInput.editorInputBehaviorInPlayMode+"/"+originalInput.backgroundBehavior+"|asset="+asset;
        }
        public static string EndInputScope()
        {
            if(!cloneInput)return "NO_SCOPE";
            InputSystem.settings=originalInput;UnityEngine.Object.DestroyImmediate(cloneInput);cloneInput=null;
            return "restored="+InputSystem.settings.editorInputBehaviorInPlayMode+"/"+InputSystem.settings.backgroundBehavior;
        }
        public static string TouchDebug()
        {
            var ts=(Touchscreen)InputSystem.devices.FirstOrDefault(d=>d.name.StartsWith(TouchName));var es=UnityEngine.EventSystems.EventSystem.current;
            var m=es?es.currentInputModule:null;var ui=m as UnityEngine.InputSystem.UI.InputSystemUIInputModule;
            var t=ts!=null?ts.primaryTouch:null;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,"device={0}|phase={1}|pos={2}|press={3}|current={4}|module={5}|actions={6}|overUI={7}|behavior={8}|enabled={9}",
                ts!=null?ts.deviceId:-1,t!=null?t.phase.ReadValue().ToString():"-",t!=null?t.position.ReadValue().ToString():"-",t!=null?t.press.isPressed:false,
                Touchscreen.current!=null?Touchscreen.current.name:"-",m?m.GetType().Name:"-",ui&&ui.actionsAsset?ui.actionsAsset.name:"-",es&&es.IsPointerOverGameObject(),InputSystem.settings.editorInputBehaviorInPlayMode,ts!=null&&ts.enabled);
        }
    }
}
