using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace DeadSurvivor.Editor
{
    // 045: probe for Proto045_Main. Same contract as the 044 probe (real UI buttons / map command queue, read state,
    // never edits the scene asset, previous scene setup kept in SessionState) plus layout checks, quality switching,
    // the editor-only load driver, occlusion injection and 3D-view counters. Touch is driven through the 044 probe.
    public static class DSKeyArt045Verification
    {
        public const string ScenePath="Assets/DeadSurvivor/Scenes/Proto045/Proto045_Main.unity";
        const string LayoutPath="Assets/DeadSurvivor/Scenes/Proto045/Layout045.json";
        const string Key="DeadSurvivor.KeyArt045.";
        static DSGame Game=>UnityEngine.Object.FindAnyObjectByType<DSGame>();
        static readonly Vector2 Start=new(-1140,-1120);

        [Serializable] class StateRecord
        {
            public bool playing,paused,win,joystickActive;public string scene,state,mapError;public int frame,wave,spawned,enemyUid,spawnFailures,enemies,kills,selectedCount,views,renderedFrame,faded,quality,enemiesInsideObstacles,heroesAlive;
            public float elapsed,duration,totalTime,cameraDistance;public float[] center,joystick,teamHp,moveInput;public int[] selected;public string[] buttons,keysPressed;
        }
        [Serializable] class SetupRow {public string path;public bool isLoaded,isActive,isSubScene;}
        [Serializable] class Setup {public SetupRow[] rows;}
        static void Target(){if(Application.dataPath!="/Users/hyeongwoo/Desktop/Workspace/Deadsurvivor/Assets")throw new Exception("WRONG_PROJECT");}

        public static string State()
        {
            Target();var g=Game;var s=new StateRecord{playing=EditorApplication.isPlaying,scene=SceneManager.GetActiveScene().path,frame=Time.frameCount,quality=QualitySettings.GetQualityLevel()};
            if(g)
            {
                s.state=g.State;s.paused=g.Paused;s.win=g.Win;s.wave=g.Wave;s.spawned=g.Spawned;s.spawnFailures=g.MapSpawnFailures;s.enemies=g.Enemies.Count;s.kills=g.Kills;s.elapsed=g.Elapsed;
                s.duration=g.Config!=null&&g.Config.waves!=null&&g.Config.waves.Length>0?g.Duration:0;s.totalTime=g.TotalTime;s.center=new[]{g.Center.x,g.Center.y};s.teamHp=g.Team.Select(c=>c.hp).ToArray();
                s.heroesAlive=g.Team.Count(c=>c.hp>0);s.enemyUid=(int)typeof(DSGame).GetField("enemyUid",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(g);s.moveInput=new[]{g.MoveInput.x,g.MoveInput.y};
                var kb=UnityEngine.InputSystem.Keyboard.current;s.keysPressed=kb==null?new string[0]:kb.allKeys.Where(k=>k!=null&&k.isPressed).Select(k=>k.name).ToArray();s.selected=g.Selected.ToArray();s.selectedCount=g.Selected.Count;s.mapError=g.MapCommandError;
                if(g.UI){s.joystickActive=g.UI.JoystickActive;s.joystick=new[]{g.UI.Joystick.x,g.UI.Joystick.y};}
                if(g.World){s.views=g.World.MapActorViewCount;s.renderedFrame=g.World.MapActorRenderedFrame;}
                if(g.MapSession&&EditorApplication.isPlaying&&g.MapSession.Geometry!=null)   // World()/Geometry need a bound session (Play)
                {
                    var v=g.MapSession.Visibility;s.faded=v?v.FadedCount:-1;
                    var cam=g.MapSession.ViewCamera;s.cameraDistance=cam?Vector3.Dot(g.MapSession.World(g.Center)-cam.transform.position,cam.transform.forward):-1;
                    var obs=g.MapSession.Geometry.Obstacles;s.enemiesInsideObstacles=g.Enemies.Count(e=>obs.Any(o=>Inside(e.pos,o)));
                }
                s.buttons=g.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.interactable).Select(b=>string.Join("|",b.GetComponentsInChildren<TextMeshProUGUI>().Select(t=>t.text))).ToArray();
            }
            return JsonUtility.ToJson(s);
        }
        static bool Inside(Vector2 p,Vector2[] poly){bool r=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].y>p.y)!=(poly[j].y>p.y)&&p.x<(poly[j].x-poly[i].x)*(p.y-poly[i].y)/(poly[j].y-poly[i].y)+poly[i].x)r=!r;return r;}

        public static string OpenMain()=>OpenScene(ScenePath);
        public static string OpenScene(string path)
        {
            Target();if(EditorApplication.isPlaying)throw new Exception("ALREADY_PLAYING");
            for(int i=0;i<EditorSceneManager.sceneCount;i++)if(EditorSceneManager.GetSceneAt(i).isDirty)throw new Exception("UNSAVED_SCENE:"+EditorSceneManager.GetSceneAt(i).path);
            if(SessionState.GetString(Key+"setup","")!="")throw new Exception("UNFINISHED_PREVIOUS_RUN");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            SessionState.SetString(Key+"setup",JsonUtility.ToJson(new Setup{rows=setup.Select(x=>new SetupRow{path=x.path,isLoaded=x.isLoaded,isActive=x.isActive,isSubScene=x.isSubScene}).ToArray()}));
            SessionState.SetInt(Key+"quality",QualitySettings.GetQualityLevel());
            SessionState.SetString(Key+"dirtyBefore",string.Join("\n",DirtyAssets().Select(o=>AssetDatabase.GetAssetPath(o)+"|"+o.GetInstanceID())));
            SessionState.SetString(Key+"scene",path);EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            return State();
        }
        public static string EnterPlay(){Target();if(SceneManager.GetActiveScene().path!=SessionState.GetString(Key+"scene",ScenePath))throw new Exception("MAIN_SCENE_NOT_OPEN");EditorApplication.isPlaying=true;return "REQUESTED";}
        public static string ExitPlay(){Target();RemoveDriver();EditorApplication.isPlaying=false;return "REQUESTED";}
        public static string Restore()
        {
            Target();if(EditorApplication.isPlaying)throw new Exception("STILL_PLAYING");
            var raw=SessionState.GetString(Key+"setup","");if(raw=="")return "NOTHING_TO_RESTORE";
            var s=JsonUtility.FromJson<Setup>(raw);var main=SceneManager.GetActiveScene();if(main.path==SessionState.GetString(Key+"scene",ScenePath)&&main.isDirty)throw new Exception("MAIN_SCENE_DIRTY");
            if(s.rows.All(r=>string.IsNullOrEmpty(r.path)))   // batch-mode editor started with an unsaved empty scene: return to one
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                SessionState.EraseString(Key+"setup");SessionState.EraseString(Key+"scene");return "EMPTY_SCENE_RESTORED";
            }
            EditorSceneManager.RestoreSceneManagerSetup(s.rows.Select(r=>new SceneSetup{path=r.path,isLoaded=r.isLoaded,isActive=r.isActive,isSubScene=r.isSubScene}).ToArray());
            var now=EditorSceneManager.GetSceneManagerSetup().Select(x=>x.path+":"+x.isLoaded+":"+x.isActive+":"+x.isSubScene).ToArray();
            var want=s.rows.Select(x=>x.path+":"+x.isLoaded+":"+x.isActive+":"+x.isSubScene).ToArray();
            if(!now.SequenceEqual(want))throw new Exception("SETUP_NOT_RESTORED:"+string.Join(",",now));
            SessionState.EraseString(Key+"setup");SessionState.EraseString(Key+"scene");return JsonUtility.ToJson(s);
        }
        // Quality level for captures (0 = Mobile, 1 = PC). RestoreQuality puts back the level seen at OpenMain.
        public static string Quality(int level){QualitySettings.SetQualityLevel(level,true);return QualitySettings.GetQualityLevel()+":"+QualitySettings.names[level];}
        // Play mode only offers the quality levels of the active build target (Standalone -> PC). To see the mobile look,
        // temporarily override the current level's pipeline with Mobile_RPAsset (in memory) and put the original back.
        static UnityEngine.Rendering.RenderPipelineAsset pipelineBefore;static bool pipelineOverridden;
        public static string MobilePipeline(bool on)
        {
            if(on){if(pipelineOverridden)throw new Exception("PIPELINE_OVERRIDE_ACTIVE");pipelineBefore=QualitySettings.renderPipeline;pipelineOverridden=true;
                QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");}
            else if(pipelineOverridden)
            {
                QualitySettings.renderPipeline=pipelineBefore;pipelineOverridden=false;
                // values are back; drop the dirty flag so Unity does not re-serialize QualitySettings.asset later (seen 11:37)
                foreach(var o in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset"))if(o)EditorUtility.ClearDirty(o);
            }
            return (QualitySettings.renderPipeline?QualitySettings.renderPipeline.name:"null")+"|level="+QualitySettings.names[QualitySettings.GetQualityLevel()]+"|levels="+string.Join(",",QualitySettings.names);
        }
        public static string RestoreQuality(){int q=SessionState.GetInt(Key+"quality",-1);if(q<0)return "NOTHING";QualitySettings.SetQualityLevel(q,true);SessionState.EraseInt(Key+"quality");return q.ToString();}

        public static string Click(string label)
        {
            var g=Game;if(!g)throw new Exception("NO_GAME");
            var b=g.GetComponentsInChildren<UnityEngine.UI.Button>(false).FirstOrDefault(x=>x.interactable&&x.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text==label));
            if(!b)throw new Exception("BUTTON_MISSING:"+label);b.onClick.Invoke();return State();
        }
        public static string Submit(string kind,int value){var g=Game;if(!g)throw new Exception("NO_GAME");if(!g.SubmitMapCommand(kind,value,"TEST"))throw new Exception("SUBMIT_REJECTED:"+kind);return State();}
        public static string KillParty(){var g=Game;if(!g||g.State!="battle")throw new Exception("NOT_IN_BATTLE");foreach(var c in g.Team)c.hp=0;return State();}

        // Visual-only test injection: place the party (centre + formation slots) at a walkable point, e.g. behind a structure.
        public static string Teleport(float x,float y)
        {
            var g=Game;if(!g||g.State!="battle")throw new Exception("NOT_IN_BATTLE");var c=new Vector2(x,y);
            if(!DSMapPartyRules.CanStart(g.MapSession.Geometry,c,g.Team.Count))throw new Exception("TELEPORT_TARGET_INVALID");
            g.Center=c;for(int i=0;i<g.Team.Count;i++)g.Team[i].pos=c+DSGame.FormationOffset(g.Formation,i,g.Team.Count);return State();
        }

        // ---- layout checks (pure functions on the saved layout; Play not required) ----
        [Serializable] class LayoutReport {public string status;public bool[] startCounts;public string[] structures;public string[] failures;}
        public static string LayoutChecks()
        {
            var data=JsonUtility.FromJson<DSBlockoutData>(AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath).text);var map=data.Geometry(4f);
            var rep=new LayoutReport{startCounts=Enumerable.Range(1,4).Select(n=>DSMapPartyRules.CanStart(map,Start,n)).ToArray()};
            var fails=new List<string>();var lines=new List<string>();
            if(rep.startCounts.Any(b=>!b))fails.Add("START_SLOTS");
            var nav18=new DSMapNavigation(map,18);var nav25=new DSMapNavigation(map,25);
            foreach(var o in data.objects.Where(o=>string.CompareOrdinal(o.id,"OB-006")<=0))
            {
                var poly=o.points_px.Select(p=>data.Logical(p,4)).ToArray();var c=new Vector2(poly.Average(p=>p.x),poly.Average(p=>p.y));
                int candidates=0,transitionOk=0,reviveOk=0,approach18=0,approach25=0,approachTried=0;
                for(int k=0;k<16;k++)
                {
                    float a=Mathf.PI*2*k/16;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    // walk outward from the centroid until the party centre fits, then a little more
                    Vector2 p=c;bool ok=false;for(float d=0;d<600;d+=8){p=c+dir*d;if(!poly.Any()||Inside(p,poly))continue;if(DSMapPartyRules.CanStart(map,p,4)){p+=dir*16;ok=DSMapPartyRules.CanStart(map,p,4);break;}}
                    if(!ok)continue;candidates++;
                    var team=Enumerable.Range(0,4).Select(i=>new Survivor{index=i,hp=100,maxhp=100,pos=p+DSGame.FormationOffset(0,i,4)}).ToArray();
                    if(DSMapPartyRules.CanTransition(map,p,0,1,team)&&DSMapPartyRules.CanTransition(map,p,0,2,team))transitionOk++;
                    var dead=team.Select((t,i)=>new Survivor{index=i,hp=i==1?0:100,maxhp=100,pos=t.pos}).ToArray();
                    if(DSMapPartyRules.CanTransition(map,p,0,0,dead,revive:1))reviveOk++;
                    // enemy approach from just outside the structure on this side to the start position
                    var e=c+dir*0;for(float d=0;d<600;d+=6){e=c+dir*d;if(!Inside(e,poly)&&map.SafeSweep(e,e,25))break;}
                    approachTried++;
                    if(nav18.TryPath(e,Start,out _))approach18++;
                    if(nav25.TryPath(e,Start,out _))approach25++;
                }
                lines.Add($"{o.id} candidates={candidates} transition={transitionOk} revive={reviveOk} approach18={approach18}/{approachTried} approach25={approach25}/{approachTried}");
                if(transitionOk<1)fails.Add(o.id+":TRANSITION");if(reviveOk<1)fails.Add(o.id+":REVIVE");
                if(approach18<approachTried||approach25<approachTried)fails.Add(o.id+":APPROACH");
            }
            rep.structures=lines.ToArray();rep.failures=fails.ToArray();rep.status=fails.Count==0?"PASS":"FAIL";
            return JsonUtility.ToJson(rep);
        }

        // ---- load segment (editor-only driver, see DSKeyArt045LoadDriver) ----
        public static string AttachDriver(int target,float hold,float reach,bool healOnly)
        {
            var g=Game;if(!g||g.State!="battle")throw new Exception("NOT_IN_BATTLE");if(g.GetComponent<DSKeyArt045LoadDriver>())throw new Exception("DRIVER_EXISTS");
            var d=g.gameObject.AddComponent<DSKeyArt045LoadDriver>();d.Target=target;d.HoldSeconds=hold;d.ReachLimitSeconds=reach;d.HealOnly=healOnly;d.Begin(g);return "ATTACHED";
        }
        [Serializable] class DriverRecord
        {
            public bool attached,healOnly,reached,held,heroDied,ended,searchFieldMissing;public string endReason;
            public int pendingSearchMax,unexplainedMax;public float unexplainedMaxRatio;public string[] unexplainedSamples;public int geoFailures,searchFailures,ticks,injected,injectFailures,skippedBudget,mismatches,orderViolations,maxAlive,minAliveWhileHolding,rawStillMax,holdRangedMax,phaseMax,noPathMax,waitingPathMax,holdFrames;
            public float holdEnd,rawStillMaxRatio,noPathMaxRatio,waitingPathMaxRatio,holdFrameMsMedian,holdFrameMsP95;public string[] order,samples,noPathSamples;
        }
        public static string DriverReport()
        {
            var g=Game;var d=g?g.GetComponent<DSKeyArt045LoadDriver>():null;if(!d)return JsonUtility.ToJson(new DriverRecord());
            var ms=d.HoldFrameMs.OrderBy(x=>x).ToArray();float Q(float q)=>ms.Length==0?-1:ms[Mathf.Clamp((int)Math.Ceiling(q*ms.Length)-1,0,ms.Length-1)];
            return JsonUtility.ToJson(new DriverRecord{pendingSearchMax=d.PendingSearchMax,unexplainedMax=d.UnexplainedMax,unexplainedMaxRatio=d.UnexplainedMaxRatio,unexplainedSamples=d.UnexplainedSamples.ToArray(),geoFailures=d.GeoFailures,searchFailures=d.SearchFailures,attached=true,healOnly=d.HealOnly,reached=d.Reached,held=d.Held,heroDied=d.HeroDied,ended=d.Ended,searchFieldMissing=d.SearchFieldMissing,endReason=d.EndReason,
                ticks=d.Ticks,injected=d.Injected,injectFailures=d.InjectFailures,skippedBudget=d.SkippedBudget,mismatches=d.Mismatches,orderViolations=d.OrderViolations,maxAlive=d.MaxAlive,
                minAliveWhileHolding=d.MinAliveWhileHolding==int.MaxValue?-1:d.MinAliveWhileHolding,rawStillMax=d.RawStillMax,holdRangedMax=d.HoldRangedMax,phaseMax=d.PhaseMax,noPathMax=d.NoPathMax,
                waitingPathMax=d.WaitingPathMax,holdFrames=ms.Length,holdEnd=d.HoldEnd,rawStillMaxRatio=d.RawStillMaxRatio,noPathMaxRatio=d.NoPathMaxRatio,waitingPathMaxRatio=d.WaitingPathMaxRatio,
                holdFrameMsMedian=Q(.5f),holdFrameMsP95=Q(.95f),order=d.Order.ToArray(),samples=d.Samples.ToArray(),noPathSamples=d.NoPathSamples.ToArray()});
        }

        // Play/test runs leave in-memory changes on unrelated assets (TMP font glyph cache, material GI flags) that a later
        // recompile/save flushes to disk (seen 12:21:53). After Play, list dirty assets outside Proto045 and drop the flag.
        static IEnumerable<UnityEngine.Object> DirtyAssets()=>Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Where(o=>o&&EditorUtility.IsDirty(o)&&AssetDatabase.Contains(o));
        // 045-J4: only drop flags that appeared during this run; assets already dirty before OpenScene (possibly the user's
        // unsaved work) are left untouched and reported.
        public static string ClearSideEffectDirty()
        {
            if(EditorApplication.isPlaying)throw new Exception("STILL_PLAYING");
            var before=new HashSet<string>(SessionState.GetString(Key+"dirtyBefore","").Split('\n').Where(x=>x!=""));
            var cleared=new List<string>();var kept=new List<string>();
            foreach(var o in DirtyAssets().ToArray())
            {
                var path=AssetDatabase.GetAssetPath(o);
                if(path.StartsWith("Assets/DeadSurvivor/Scenes/Proto045/")||path.EndsWith(".unity"))continue;
                if(before.Contains(path+"|"+o.GetInstanceID())){kept.Add(path);continue;}
                cleared.Add(path);EditorUtility.ClearDirty(o);
            }
            SessionState.EraseString(Key+"dirtyBefore");
            return "cleared="+string.Join(",",cleared.Distinct())+" | keptPreexisting="+string.Join(",",kept.Distinct());
        }

        // ---- 045-I6: view mapping / purity checks ----
        // Each live hero's view key must be "H:"+its character id.
        public static string HeroViewKeys()
        {
            var g=Game;if(!g||g.State!="battle")throw new Exception("NOT_IN_BATTLE");
            return string.Join(",",g.Team.Where(c=>c.hp>0).Select(c=>g.Def(c).id+"="+(g.World.MapActorKeyOf(c)??"null")));
        }
        // 045 stage 3: per live hero, the view's mesh / controller / humanoid flag / Run parameter / visual yaw, a pose
        // signature of its first bones and the Animator state + normalised time. In a batch editor nothing renders, so
        // CullUpdateTransforms never writes bone poses (pose constant for every hero); state time still advances.
        public static string HeroViewModels()
        {
            var g=Game;if(!g||g.State!="battle")throw new Exception("NOT_IN_BATTLE");
            var all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
            var parts=new List<string>();
            foreach(var c in g.Team.Where(c=>c.hp>0))
            {
                var id=g.Def(c).id;var key=g.World.MapActorKeyOf(c);var t=key==null?null:all.FirstOrDefault(x=>x.name==key&&x.gameObject.activeInHierarchy);
                if(!t){parts.Add(id+"=none");continue;}
                var smr=t.GetComponentInChildren<SkinnedMeshRenderer>();var an=t.GetComponentInChildren<Animator>();
                float h=0;if(smr)foreach(var b in smr.bones.Take(24))if(b){var r=b.localRotation;h+=r.x*3+r.y*5+r.z*7+r.w*11;}
                bool run=an&&an.isActiveAndEnabled&&an.runtimeAnimatorController&&an.GetBool("Run");
                // state machine progress is evaluated even when culled (CullUpdateTransforms skips only the bone writes)
                string stName="none";float stT=-1;if(an&&an.isActiveAndEnabled&&an.runtimeAnimatorController){var st=an.GetCurrentAnimatorStateInfo(0);stName=st.IsName("Run")?"Run":st.IsName("Idle")?"Idle":"other";stT=st.normalizedTime;}
                parts.Add(id+"="+(smr&&smr.sharedMesh?smr.sharedMesh.name.Replace(",","_").Replace("|","_"):"nomesh")+"|"+(an&&an.runtimeAnimatorController?an.runtimeAnimatorController.name:"noctrl")
                    +"|human="+(an&&an.avatar&&an.avatar.isHuman?1:0)+"|run="+run+"|yaw="+(t.childCount>0?t.GetChild(0).localEulerAngles.y:0).ToString("F0")+"|pose="+h.ToString("F5")+"|state="+stName+"|t="+stT.ToString("F4")
                    +"|asset="+(smr&&smr.sharedMesh?AssetDatabase.GetAssetPath(smr.sharedMesh):"none")
                    +"|body="+BodyYaw(an).ToString("F1")+"|tex="+TexOf(smr)+"|clip="+(an&&an.isActiveAndEnabled&&an.runtimeAnimatorController&&an.GetCurrentAnimatorClipInfo(0).Length>0?an.GetCurrentAnimatorClipInfo(0)[0].clip.name.Replace(",","_").Replace("|","_"):"none"));
            }
            return string.Join(",",parts);
        }
        // 045 stage 3 v2: world yaw (deg, atan2(x,z)) of the body's front from the hand bones in the current pose; the game
        // turns the view to atan2(dx,dz) of the movement, so a correctly facing hero has body ~= movement yaw while moving
        static float BodyYaw(Animator an)
        {
            if(!an||!an.isHuman)return float.NaN;var l=an.GetBoneTransform(HumanBodyBones.LeftHand);var r=an.GetBoneTransform(HumanBodyBones.RightHand);
            if(!l||!r)return float.NaN;var right=r.position-l.position;right.y=0;var f=Vector3.Cross(right.normalized,Vector3.up);
            return Mathf.Atan2(f.x,f.z)*Mathf.Rad2Deg;
        }
        // 045 stage 3 v2: base-map texture of the view's material ('HIT' while the hit-flash material is shown)
        static string TexOf(Renderer r)
        {
            if(!r||!r.sharedMaterial)return "none";var m=r.sharedMaterial;
            if(!m.HasProperty("_BaseMap")||!m.GetTexture("_BaseMap"))return m.name.StartsWith("Hit")?"HIT":"notex";
            return AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap"));
        }
        // 045 stage 3: the pose exception (NOT_RUN) applies only to a batch-mode editor, where nothing renders.
        public static string BatchMode()=>Application.isBatchMode?"1":"0";
        // Calling the renderer again must not change simulation state (views read sim only).
        public static string RenderPurity()
        {
            var g=Game;if(!g||g.State!="battle")throw new Exception("NOT_IN_BATTLE");
            // 045-J6: every DSGame field (the same set the full-snapshot contract covers), walked reflectively: nested game
            // objects, collections, System.Random internals. Unity object references are compared by identity only.
            // negative control first: a deliberate change of one sim field must be detected, then restored
            var c0=Deep(g);g.Kills++;var c1=Deep(g);g.Kills--;var c2=Deep(g);
            if(c0==c1||c0!=c2)return "CONTROL_FAILED:detect="+(c0!=c1)+" restore="+(c0==c2);
            var a=Deep(g);g.World.Render();g.World.Render();var b=Deep(g);
            if(a==b)return "PURE:"+a.Length;
            int i=0;while(i<a.Length&&i<b.Length&&a[i]==b[i])i++;
            return "CHANGED@"+i+":"+a.Substring(Math.Max(0,i-80),Math.Min(160,a.Length-Math.Max(0,i-80)));
        }
        static string Deep(DSGame g)
        {
            var sb=new System.Text.StringBuilder();var seen=new HashSet<object>(new RefEq());
            void Walk(object o,int depth)
            {
                if(o==null){sb.Append("null;");return;}
                if(depth>12){sb.Append("…;");return;}
                var t=o.GetType();
                // 045-K1: the root DSGame is a MonoBehaviour; walk its own fields. Nested Unity objects compare by identity.
                if(depth>0&&o is UnityEngine.Object uo){sb.Append("U").Append(uo?uo.GetInstanceID():0).Append(';');return;}
                if(t.IsPrimitive||o is string||o is decimal||t.IsEnum){sb.Append(Convert.ToString(o,System.Globalization.CultureInfo.InvariantCulture)).Append(';');return;}
                if(o is Delegate){sb.Append("D;");return;}
                if(t.IsValueType&&t.Namespace=="UnityEngine"){sb.Append(o.ToString()).Append(';');return;}
                if(!t.IsValueType&&!seen.Add(o)){sb.Append("ref;");return;}
                if(o is System.Collections.IDictionary dict){sb.Append('{');foreach(System.Collections.DictionaryEntry kv in dict){Walk(kv.Key,depth+1);Walk(kv.Value,depth+1);}sb.Append('}');return;}
                if(o is System.Collections.IEnumerable en){sb.Append('[');foreach(var x in en)Walk(x,depth+1);sb.Append(']');return;}
                sb.Append(t.Name).Append('(');
                for(var tt=t;tt!=null&&tt!=typeof(object)&&tt!=typeof(MonoBehaviour)&&tt!=typeof(Behaviour)&&tt!=typeof(Component);tt=tt.BaseType)
                    foreach(var f in tt.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly))
                    {sb.Append(f.Name).Append('=');Walk(f.GetValue(o),depth+1);}
                sb.Append(')');
            }
            Walk(g,0);return sb.ToString();
        }
        sealed class RefEq:IEqualityComparer<object>{public new bool Equals(object a,object b)=>ReferenceEquals(a,b);public int GetHashCode(object o)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);}
        // Self-test of DSMapActorViews with synthetic owners/prefabs (Play mode; objects die with Play).
        public static string ActorViewsSelfTest()
        {
            if(!EditorApplication.isPlaying)throw new Exception("NOT_PLAYING");
            var root=new GameObject("ActorViewsSelfTest045");var fails=new List<string>();
            try
            {
                GameObject Prefab(string n){var p=new GameObject(n);p.transform.SetParent(root.transform,false);var v=new GameObject("Visual");v.transform.SetParent(p.transform,false);var m=GameObject.CreatePrimitive(PrimitiveType.Cube);UnityEngine.Object.DestroyImmediate(m.GetComponent<Collider>());m.transform.SetParent(v.transform,false);p.SetActive(false);return p;}
                var normal=new Material(Shader.Find("Universal Render Pipeline/Unlit"));var hit=new Material(normal);var set=ScriptableObject.CreateInstance<DSMapActorSet>();
                set.Heroes=new[]{new DSMapActorSet.Entry{id="a",prefab=Prefab("A"),material=normal},new DSMapActorSet.Entry{id="b",prefab=Prefab("B"),material=normal}};
                set.Monsters=new[]{new DSMapActorSet.Entry{id="m",prefab=Prefab("M"),material=normal}};set.HitMaterial=hit;
                var parent=new GameObject("Views");parent.transform.SetParent(root.transform,false);var v=new DSMapActorViews(set,parent.transform);
                object o1=new object(),o2=new object(),e1=new object(),e2=new object();
                void Frame(Action body){v.Begin();body();v.End();}
                Frame(()=>{v.Hero(o1,"a",Vector3.zero,false);v.Monster(e1,"m",18,new Vector3(1,0,0),true);});
                if(v.KeyOf(o1)!="H:a"||!v.FlashingOf(e1)||v.LiveCount!=2)fails.Add("initial");
                Frame(()=>{v.Hero(o1,"b",Vector3.zero,false);});   // same owner, id changed; e1 not drawn -> pooled while flashing
                if(v.KeyOf(o1)!="H:b"||v.KeyOf(e1)!=null||v.LiveCount!=1)fails.Add("rekey_or_sweep");
                Frame(()=>{v.Hero(o1,"b",Vector3.zero,false);v.Monster(e2,"m",18,new Vector3(2,0,0),false);});   // reuse pooled monster view
                var r2=parent.GetComponentsInChildren<Renderer>(false).Where(r=>r.transform.root==root.transform&&r.sharedMaterial==hit).Count();
                if(v.FlashingOf(e2)||r2!=0)fails.Add("pooled_view_kept_hit_material");
                Frame(()=>{v.Hero(o1,"b",new Vector3(.5f,0,0),false);v.Monster(e2,"m",18,new Vector3(3,0,0),false);});   // moved +x -> turns toward +x
                float y1=v.YawOf(e2);
                Frame(()=>{v.Hero(o1,"b",new Vector3(.5f,0,0),false);v.Monster(e2,"m",18,new Vector3(30,0,0),false);});   // teleport -> facing reset
                if(Mathf.Abs(Mathf.DeltaAngle(v.YawOf(e2),180))>.01f)fails.Add("teleport_facing_not_reset:"+v.YawOf(e2)+"/before="+y1);
                v.Clear();if(v.LiveCount!=0)fails.Add("clear");
                Frame(()=>{v.Hero(o2,"a",Vector3.zero,false);});if(v.KeyOf(o2)!="H:a"||v.LiveCount!=1)fails.Add("reuse_after_clear");
                if(v.Hero(o2,"missing",Vector3.zero,false))fails.Add("missing_entry_should_fallback");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            return fails.Count==0?"PASS":"FAIL:"+string.Join(",",fails);
        }
        // 045-I1 (static): every occluder in the scene must use a dither-fade material; also report the tallest legacy occluder
        // (legacy blockout prisms are <= 0.64 high and cannot hide the focus point at 55 deg, so a Play occlusion is not producible).
        public static string OccluderMaterials()
        {
            var g=Game;var vis=g.MapSession.Visibility;var bad=new List<string>();float legacyTop=0;int total=0;
            foreach(var r in vis.Blockers)
            {
                total++;if(!r.sharedMaterials.All(m=>m&&m.HasProperty("_DitherFade")&&m.GetFloat("_DitherFade")>.5f))bad.Add(r.name);
                if(!r.transform.IsChildOf(g.MapSession.transform.Find("KeyArt045_VisualOnly")))legacyTop=Mathf.Max(legacyTop,r.bounds.max.y);
            }
            return "total="+total+"|nonDither="+bad.Count+"|"+string.Join(",",bad.Take(5))+"|legacyTop="+legacyTop.ToString("F2",System.Globalization.CultureInfo.InvariantCulture);
        }
        // ---- 045 stage 2 (S2D5) ----
        // every tall (shadow-casting) stage-2 renderer must be an occluder; negative control: drop one from the list -> 1 missing
        public static string TallOccluders()
        {
            var g=Game;var root=g.MapSession.transform.Find("KeyArt045_S2_VisualOnly");if(!root)return "NO_S2";
            var tall=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.Off).ToArray();
            var blockers=g.MapSession.Visibility.Blockers;int missing=tall.Count(r=>!blockers.Contains(r));
            var minusOne=blockers.Where(r=>r!=tall[0]).ToArray();int control=tall.Count(r=>!minusOne.Contains(r));
            return "tall="+tall.Length+"|missing="+missing+"|control="+control;
        }
        // a walkable party centre from which a specific stage-2 tree hides at least one hero focus point (parallel-ray test)
        public static string TreeOcclusionSpot()
        {
            var g=Game;var root=g.MapSession.transform.Find("KeyArt045_S2_VisualOnly");var map=g.MapSession.Geometry;var cam=g.MapSession.ViewCamera;
            var pres=g.MapSession.GetComponent<DSMapPresentation>();var lift=pres&&pres.Actors?pres.Actors.FocusLift:.28f;var fwd=Quaternion.Euler(g.MapSession.Elevation,0,0)*Vector3.forward;var up=Quaternion.Euler(g.MapSession.Elevation,0,0)*Vector3.up;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.transform.parent&&r.transform.parent.name.StartsWith("Tree_")||r.name.StartsWith("Tree_")))
            {
                var b=r.bounds;
                for(float dz=.2f;dz<=3f;dz+=.1f)
                {
                    var p=new Vector3(b.center.x,0,b.max.z+dz);var c=new Vector2(p.x/.02f,-p.z/.02f);
                    if(!DSMapPartyRules.CanStart(map,c,g.Team.Count))continue;
                    bool hidden=false;
                    for(int i=0;i<g.Team.Count&&!hidden;i++){var f=g.MapSession.World(c+DSGame.FormationOffset(g.Formation,i,g.Team.Count))+Vector3.up*.01f+up*lift;hidden=b.IntersectRay(new Ray(f-fwd*100,fwd),out float d)&&d<99.99f;}
                    if(hidden)return r.name+"|"+c.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+c.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            return "NONE";
        }
        // occluders currently swapped to DSMapVisibility's runtime copies (materials that are not assets)
        public static string FadedNames(){var g=Game;return string.Join(",",g.MapSession.Visibility.Blockers.Where(r=>r&&r.sharedMaterials.Any(m=>m&&!AssetDatabase.Contains(m))).Select(r=>r.name));}
        // 045-I1: a walkable point just north of a tall legacy obstacle outside the art area (rings cannot occlude at 55 deg).
        public static string LegacyOcclusionSpot()
        {
            var g=Game;var data=g.MapSession.Data;var map=g.MapSession.Geometry;
            foreach(var o in data.objects.Where(o=>string.CompareOrdinal(o.id,"OB-006")>0&&o.height_logical>=32).OrderBy(o=>(data.Logical(o.points_px[0],4)-g.Center).sqrMagnitude))
            {
                var poly=o.points_px.Select(p=>data.Logical(p,4)).ToArray();float cx=poly.Average(p=>p.x),minY=poly.Min(p=>p.y);
                for(float d=10;d<=40;d+=6){var c=new Vector2(cx,minY-d);if(DSMapPartyRules.CanStart(map,c,g.Team.Count))return o.id+"|"+c.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+c.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
            }
            return "NONE";
        }
        public static string RemoveDriver(){var g=Game;var d=g?g.GetComponent<DSKeyArt045LoadDriver>():null;if(!d)return "NONE";UnityEngine.Object.DestroyImmediate(d);return "REMOVED";}
        // editor frame statistics (indicators only; not device performance)
        [Serializable] class StatsRecord {public int drawCalls,setPass,triangles;public float frameMs;}
        public static string Stats()=>JsonUtility.ToJson(new StatsRecord{drawCalls=UnityStats.drawCalls,setPass=UnityStats.setPassCalls,triangles=UnityStats.triangles,frameMs=Time.unscaledDeltaTime*1000});
    }
}
