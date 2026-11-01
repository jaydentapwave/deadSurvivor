#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
namespace DeadSurvivor.Editor
{
    [Serializable] public sealed class DSSnapshotUnitRequest {public int schema_version,seed,lootSeed;public string document_role,run,nonce;}
    [Serializable] public sealed class DSSnapshotUnitRaw {public string role="SNAPSHOT_FOUNDATION_ONLY_031",run,nonce,requestSha,inputScopeEvidence,error;public bool complete;public int instanceId;public DSFullSnapshotUnitRaw unit;}
    [Serializable] public sealed class DSSnapshotReceipt {public string role="SNAPSHOT_NATIVE_RECEIPT_031",path,sha256,run,nonce;public int bytes,instanceId;}
    public static class DSMapSnapshotVerification
    {
        static string SafePath(string path)
        {
            string prefix=Path.GetFullPath(Path.Combine(Application.dataPath,"../output/map-full-026/")).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string full=Path.GetFullPath(path);if(!full.StartsWith(prefix,StringComparison.Ordinal))throw new Exception("SNAPSHOT_PATH");
            for(string parent=full;!string.IsNullOrEmpty(parent);parent=Path.GetDirectoryName(parent))if((File.Exists(parent)||Directory.Exists(parent))&&(File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new Exception("SNAPSHOT_SYMLINK");
            return full;
        }
        public static string Begin(string requestPath,string resultPath,string expectedSha)
        {
            string text=File.ReadAllText(SafePath(requestPath));if(DSMapBlockoutPreview.Hash(text)!=expectedSha)throw new Exception("SNAPSHOT_REQUEST_SHA");
            var spec=JsonUtility.FromJson<DSSnapshotUnitRequest>(text);
            if(spec.schema_version!=1||spec.document_role!="SNAPSHOT_UNIT_REQUEST_031"||spec.run==null||spec.nonce==null||!System.Text.RegularExpressions.Regex.IsMatch(spec.run,"^[0-9a-f]{32}$")||!System.Text.RegularExpressions.Regex.IsMatch(spec.nonce,"^[0-9a-f]{32}$"))throw new Exception("SNAPSHOT_REQUEST");
            resultPath=SafePath(resultPath);if(File.Exists(resultPath)||File.Exists(resultPath+".pending"))throw new Exception("SNAPSHOT_OUTPUT_EXISTS");
            var wrapper=DSMapMotionVerification.CreateExternalRoot(spec.run,spec.nonce,"Blockout018_"+spec.nonce);
            var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DSMapBlockoutVerification.PrefabPath),wrapper.transform);
            var preview=obj.GetComponent<DSMapBlockoutPreview>();var session=obj.AddComponent<DSMapSession>();
            session.Layout=preview.Layout;session.ExpectedDataSha=preview.ExpectedDataSha;session.ViewCamera=preview.ViewCamera;session.ViewCamera.tag="MainCamera";session.StaticGeometry=preview.StaticGeometry;session.RockHolder=preview.RockHolder;session.RockPlacementOffset=preview.RockPlacementOffset;session.CombatEnabled=false;
            session.Visibility=DSMapPlayableVerification.AddVisibility(obj,preview);session.Visibility.Preview=null;UnityEngine.Object.DestroyImmediate(preview);
            var go=new GameObject("OwnedSnapshotGame031");go.transform.SetParent(obj.transform,false);var game=go.AddComponent<DSGame>();game.MapSession=session;game.enabled=false;
            DSMapMotionVerification.GuardExternalRoot(spec.run,spec.nonce,wrapper,()=>{if(!game||game.MapSession!=session)throw new Exception("SNAPSHOT_OWNER_LOST");},()=>{if(game)game.enabled=false;DSOwnedInputScope.Restore(spec.run,spec.nonce);});
            var raw=new DSSnapshotUnitRaw{run=spec.run,nonce=spec.nonce,requestSha=expectedSha};
            try
            {
                wrapper.SetActive(true);game.VerificationRun=true;game.Config.speed=1;game.Rng=new DSMapTraceRandom(spec.seed);game.LootRng=new DSMapTraceRandom(spec.lootSeed);game.Selected=new System.Collections.Generic.List<int>{0,1};
                DSMapMotionVerification.RegisterOwnedUnitGame(spec.run,spec.nonce,game);raw.instanceId=game.GetInstanceID();
                DSOwnedInputScope.Begin(spec.run,spec.nonce,()=>{raw.error="EXTERNAL_DEVICE_INPUT";});raw.inputScopeEvidence=DSOwnedInputScope.EvidencePath;
                game.RunOwnedMapUnits(spec.run,spec.nonce,()=>{game.StartRun();if(game.State!="battle"||game.Team.Count!=2)throw new Exception("SNAPSHOT_INITIALIZATION_REJECTED");raw.unit=game.RunFullSnapshotOwnedUnitChecks();});
                if(DSOwnedInputScope.Error!=""||raw.error!=null)throw new Exception("SNAPSHOT_INPUT_CANCELLED");
                raw.complete=true;
            }
            catch(Exception error){raw.error=error.ToString();raw.complete=false;}
            finally{if(game){game.enabled=false;game.gameObject.SetActive(false);}}
            string pending=resultPath+".pending";
            string encoded=JsonUtility.ToJson(raw);byte[] bytes=new System.Text.UTF8Encoding(false,true).GetBytes(encoded);
            if(bytes.Length>64*1024*1024)throw new InvalidOperationException("SNAPSHOT_RAW_OUTPUT_LIMIT");
            using(var stream=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}File.Move(pending,resultPath);
            return JsonUtility.ToJson(new DSSnapshotReceipt{path=resultPath,sha256=DSMapBlockoutPreview.Hash(encoded),bytes=bytes.Length,instanceId=raw.instanceId,run=spec.run,nonce=spec.nonce});
        }
    }
}
#endif
