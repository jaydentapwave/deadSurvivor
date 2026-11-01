#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace DeadSurvivor.Editor
{
    [InitializeOnLoad]
    public static class DSOwnedInputScope
    {
        [Serializable] class DeviceState {public int id;public bool enabled;}
        [Serializable] class Journal
        {
            public string scope="OWNED_VIRTUAL_INPUT_CONFIGURATION",owner,phase,time,originalJson,cloneJson,error,externalDeviceLayout;
            public int editorPid,originalId,cloneId,externalDeviceId;public bool originalIsAsset;
            public DeviceState[] devicesBefore,devicesAfter;public int[] ownedDevices;
        }
        static InputSettings original,clone;
        static string owner="",originalJson,cloneJson,path;
        static Journal journal;
        static Action abort;
        static readonly HashSet<int> devices=new();
        public static string Error {get;private set;}="";
        public static string EvidencePath=>path??"";
        static string Base=>Path.GetFullPath(Path.Combine(Application.dataPath,"../output/map-full-026/input-scopes"));
        static DSOwnedInputScope()
        {
            AssemblyReloadEvents.beforeAssemblyReload+=BeforeReload;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingPlayMode)BeforeReload();};
        }
        static DeviceState[] DeviceStates()=>InputSystem.devices.Where(d=>!devices.Contains(d.deviceId)).Select(d=>new DeviceState{id=d.deviceId,enabled=d.enabled}).OrderBy(d=>d.id).ToArray();
        static void SafePath(string value)
        {
            string at=Path.GetFullPath(value);
            while(!string.IsNullOrEmpty(at))
            {
                if((File.Exists(at)||Directory.Exists(at))&&(File.GetAttributes(at)&FileAttributes.ReparsePoint)!=0)throw new Exception("INPUT_SCOPE_SYMLINK");
                at=Path.GetDirectoryName(at);
            }
        }
        static void Write(string phase)
        {
            journal.phase=phase;journal.time=DateTimeOffset.Now.ToString("o");journal.error=Error;journal.ownedDevices=devices.OrderBy(x=>x).ToArray();
            string temp=path+"."+Guid.NewGuid().ToString("N")+".pending";
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream)){writer.Write(JsonUtility.ToJson(journal,true));writer.Flush();stream.Flush(true);}
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        public static void Begin(string run,string nonce,Action cancelled)
        {
            if(owner!=""||cancelled==null||!EditorApplication.isPlaying||SessionState.GetString("DS.Map017.owner","")!=run+":"+nonce||SessionState.GetString("DS.Map017.phase","")!="PLAY")throw new Exception("INPUT_SCOPE_NOT_OWNED");
            SafePath(Base);Directory.CreateDirectory(Base);
            foreach(var existing in Directory.GetFiles(Base,"*.json"))
            {
                var prior=JsonUtility.FromJson<Journal>(File.ReadAllText(existing));
                if(prior.phase!="RESTORED")throw new Exception("UNFINISHED_INPUT_SCOPE:"+Path.GetFileName(existing));
            }
            owner=run+":"+nonce;Error="";devices.Clear();abort=cancelled;
            original=InputSystem.settings;originalJson=JsonUtility.ToJson(original);
            clone=UnityEngine.Object.Instantiate(original);clone.name="OwnedMapInput026_"+nonce;
            clone.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            clone.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            cloneJson=JsonUtility.ToJson(clone);
            path=Path.Combine(Base,run+"-"+nonce+"-"+Guid.NewGuid().ToString("N")+".json");
            journal=new Journal{owner=owner,editorPid=System.Diagnostics.Process.GetCurrentProcess().Id,originalId=original.GetInstanceID(),cloneId=clone.GetInstanceID(),originalIsAsset=AssetDatabase.Contains(original),originalJson=originalJson,cloneJson=cloneJson,devicesBefore=DeviceStates()};
            Write("PLANNED");
            InputSystem.settings=clone;InputSystem.onEvent+=OnInput;Write("INSTALLED");
        }
        public static void RegisterDevice(InputDevice device)
        {
            if(owner==""||device==null||!device.added||InputSystem.settings!=clone)throw new Exception("INPUT_DEVICE_NOT_OWNED");
            devices.Add(device.deviceId);Write("INSTALLED");
        }
        static void OnInput(InputEventPtr eventPtr,InputDevice device)
        {
            if(owner==""||Error!=""||device==null||devices.Contains(device.deviceId))return;
            bool changed=eventPtr.type==TextEvent.Type;
            if(eventPtr.type==StateEvent.Type||eventPtr.type==DeltaStateEvent.Type)changed=eventPtr.EnumerateChangedControls(device).Any();
            if(!changed)return;
            Error="EXTERNAL_DEVICE_INPUT";journal.externalDeviceId=device.deviceId;journal.externalDeviceLayout=device.layout;
            try{abort?.Invoke();}finally{Write("CANCELLED");}
        }
        public static void Restore(string run,string nonce)
        {
            if(owner=="")return;
            if(owner!=run+":"+nonce)throw new Exception("INPUT_SCOPE_OWNER_CONFLICT");
            try
            {
                if(!original||!clone||JsonUtility.ToJson(original)!=originalJson)throw new Exception("INPUT_ORIGINAL_CHANGED");
                if(JsonUtility.ToJson(clone)!=cloneJson)throw new Exception("INPUT_CLONE_CHANGED");
                if(InputSystem.settings!=clone && InputSystem.settings!=original)throw new Exception("INPUT_CURRENT_REFERENCE_CHANGED");
                InputSystem.onEvent-=OnInput;
                if(InputSystem.settings==clone)
                {
                    InputSystem.settings=original;
                }
                else if(InputSystem.settings!=original)throw new Exception("INPUT_CURRENT_REFERENCE_CHANGED");
                if(InputSystem.settings!=original||JsonUtility.ToJson(original)!=originalJson)throw new Exception("INPUT_RESTORE_MISMATCH");
                journal.devicesAfter=DeviceStates();
                if(JsonUtility.ToJson(new DeviceEnvelope{rows=journal.devicesBefore})!=JsonUtility.ToJson(new DeviceEnvelope{rows=journal.devicesAfter}))throw new Exception("INPUT_ORIGINAL_DEVICES_CHANGED");
                UnityEngine.Object.DestroyImmediate(clone);clone=null;Write("RESTORED");
                owner="";original=null;abort=null;devices.Clear();
            }
            catch(Exception e){Error=e.Message;Write("CONFLICT");throw;}
        }
        [Serializable] class DeviceEnvelope {public DeviceState[] rows;}
        static void BeforeReload()
        {
            if(owner=="")return;
            var parts=owner.Split(':');
            try{Restore(parts[0],parts[1]);}catch(Exception e){Debug.LogError("Owned input restore blocked: "+e.Message);}
        }
    }
}
#endif
