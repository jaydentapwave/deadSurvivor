#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeadSurvivor
{
    [Serializable] public class DSMotionPolygon { public Vector2[] points; }
    [Serializable] public class DSMotionInput { public Vector2 input; public float frame_dt; }
    [Serializable] public class DSMotionFixtureSpec
    {
        public string id, mode, expected_error;
        public int count, formation, bypass_step=-1;
        public Vector2 center;
        public Vector2[] actual, boundary;
        public DSMotionPolygon[] obstacles;
        public bool[] alive;
        public float[] radii;
        public float center_radius, epsilon, speed, elapsed;
        public DSMotionInput[] inputs;
    }
    [Serializable] public class DSMotionSnapshot
    {
        public Vector2 center;
        public Vector2[] actual, reserved;
        public float elapsed, total;
    }
    [Serializable] public class DSMotionStep
    {
        public int step_id, frame_id, instance_id, simulation_calls, combat_calls;
        public float actual_frame_dt, supplied_frame_dt, requested_dt, applied_dt, alpha;
        public bool held, bypass;
        public Vector2 input;
        public DSMotionSnapshot before, after;
    }
    [Serializable] public class DSMotionFixtureResult
    {
        public string scope="MOVEMENT_FIXTURE_ONLY", run_id, nonce, case_id, request_sha256, error="";
        public int instance_id, device_id;
        public bool complete;
        public DSMotionFixtureSpec spec;
        public List<DSMotionStep> steps=new();
    }

    // Editor-only fixed movement experiment. It is deliberately not a general map session.
    public sealed class DSMovementFixture : IDisposable
    {
        public readonly DSMotionFixtureSpec Spec;
        public readonly DSMotionFixtureResult Result;
        readonly DSMapGeometry geometry;
        readonly Action<DSMovementFixture> finished;
        readonly Keyboard ownedKeyboard;
        readonly int instance;
        readonly float[] hp;
        readonly Survivor[] team;
        DSMotionSnapshot previous;
        DSMotionProposal lastProposal;
        int lastFrame=-1, calls, combatCalls, warmup;
        float applied;
        bool terminal;
        public bool Terminal=>terminal;

        public DSMovementFixture(DSGame g, DSMotionFixtureSpec spec, string run, string nonce, Keyboard keyboard, Action<DSMovementFixture> done)
        {
            Spec=spec;finished=done;ownedKeyboard=keyboard;instance=g.GetInstanceID();
            geometry=new DSMapGeometry(spec.boundary,spec.obstacles.Select(o=>o.points).ToArray(),spec.epsilon);
            team=g.Team.ToArray();hp=team.Select(c=>c.hp).ToArray();
            Result=new DSMotionFixtureResult { run_id=run,nonce=nonce,case_id=spec.id,instance_id=instance,device_id=keyboard?.deviceId ?? 0,spec=spec };
        }
        public DSMotionSnapshot Snapshot(DSGame g) => new DSMotionSnapshot {
            center=g.Center,actual=g.Team.Select(c=>c.pos).ToArray(),reserved=g.Team.Select((c,i)=>g.Center+g.Offset(i)).ToArray(),elapsed=g.Elapsed,total=g.TotalTime };
        public void ValidateInitial(DSGame g)
        {
            if(Spec.inputs==null || Spec.inputs.Length==0 || Spec.inputs.Length>10000 || Spec.count!=team.Length ||
                Spec.radii.Length!=team.Length || Spec.alive.Length!=team.Length || Spec.speed<=0 || Spec.speed>20 || !DSMapGeometry.Finite(Spec.speed) ||
                Spec.mode!="FIXED_REPLAY" && Spec.mode!="DEVICE_SMOKE") throw new ArgumentException("INVALID_FIXTURE_SPEC");
            foreach(var row in Spec.inputs)
                if(!DSMapGeometry.ValidPoint(row.input) || row.input.sqrMagnitude>1.000001f || !DSMapGeometry.Finite(row.frame_dt) || row.frame_dt<0 || row.frame_dt>.25f)
                    throw new ArgumentException("INVALID_FIXTURE_INPUT");
            previous=Snapshot(g);
            DSMapMotion.Propose(geometry,g.Center,previous.actual,g.Team.Select((c,i)=>g.Offset(i)).ToArray(),Spec.alive,Spec.radii,Spec.center_radius,Vector2.zero,0);
        }
        public DSMotionProposal Propose(DSGame g,float dt)
        {
            if(Result.steps.Count==Spec.bypass_step)
            {
                var c=g.Center+g.MoveInput*140*dt;
                return new DSMotionProposal(c,g.Team.Select((s,i)=>s.hp<=0?s.pos:Vector2.Lerp(s.pos,c+g.Offset(i),Mathf.Min(1,12*dt))).ToArray(),1,false);
            }
            return DSMapMotion.Propose(geometry,g.Center,g.Team.Select(c=>c.pos).ToArray(),g.Team.Select((c,i)=>g.Offset(i)).ToArray(),Spec.alive,Spec.radii,Spec.center_radius,g.MoveInput,dt);
        }
        public void Applied(float dt,DSMotionProposal motion) { calls++;applied=dt;lastProposal=motion; }
        public void CombatCall() { combatCalls++;Fail("COMBAT_LEAK");throw new InvalidOperationException("COMBAT_LEAK"); }
        public void Fail(string reason)
        {
            if(terminal) return;
            Result.error=reason;terminal=true;finished(this);
        }
        public void AdvanceFrame(DSGame g)
        {
            if(terminal) return;
            try
            {
                if(Time.frameCount<=lastFrame) throw new InvalidOperationException("UNEXPECTED_FRAME");
                lastFrame=Time.frameCount;
                if(g.GetInstanceID()!=instance || g.State!="battle" || g.Paused || g.Formation!=Spec.formation || g.PendingFormation!=Spec.formation ||
                    g.Selected.Count!=Spec.count || g.Team.Count!=team.Length || !g.Team.SequenceEqual(team) || !g.Team.Select(c=>c.hp).SequenceEqual(hp) ||
                    g.Config.speed!=Spec.speed || g.Enemies.Count!=0 || g.Shots.Count!=0 || g.Effects.Count!=0)
                    throw new InvalidOperationException("STATE_CONTAMINATION");
                var before=Snapshot(g);
                if(before.center!=previous.center || !before.actual.SequenceEqual(previous.actual) || before.elapsed!=previous.elapsed || before.total!=previous.total)
                    throw new InvalidOperationException("OBSERVATION_GAP");
                var row=Spec.inputs[Result.steps.Count];
                Vector2 input;
                if(Spec.mode=="DEVICE_SMOKE")
                {
                    if(Keyboard.current!=ownedKeyboard || g.UI.JoystickActive) throw new InvalidOperationException("INPUT_OWNER_MISMATCH");
                    input=g.ReadMovementInput();
                    if(input!=row.input && Result.steps.Count==0 && warmup++<10) return;
                    if(input!=row.input) throw new InvalidOperationException("DEVICE_INPUT_MISMATCH");
                }
                else
                {
                    if(Keyboard.current!=null && Keyboard.current.anyKey.isPressed || Mouse.current!=null &&
                        (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed) || g.UI.JoystickActive)
                        throw new InvalidOperationException("EXTERNAL_INPUT");
                    input=row.input;
                }
                g.MoveInput=Vector2.ClampMagnitude(input,1);
                float supplied=Spec.mode=="DEVICE_SMOKE"?Time.unscaledDeltaTime:row.frame_dt;
                float requested=Mathf.Min(supplied,.05f)*g.Config.speed;
                calls=0;applied=0;lastProposal=null;
                g.SimulateFixtureFrame(requested);
                if(calls!=1 || lastProposal==null) throw new InvalidOperationException("OBSERVATION_GAP");
                var after=Snapshot(g);
                Result.steps.Add(new DSMotionStep {step_id=Result.steps.Count,frame_id=Time.frameCount,instance_id=instance,
                    simulation_calls=calls,combat_calls=combatCalls,actual_frame_dt=Time.unscaledDeltaTime,supplied_frame_dt=supplied,
                    requested_dt=requested,applied_dt=applied,input=input,alpha=lastProposal.Alpha,held=lastProposal.Held,
                    bypass=Result.steps.Count==Spec.bypass_step,before=before,after=after});
                previous=after;
                if(Result.steps.Count==Spec.inputs.Length) {Result.complete=true;terminal=true;finished(this);}
            }
            catch(Exception e) {Fail(e.Message);}
        }
        public void Dispose() { if(!terminal) Fail("CANCELLED"); }
    }
}
#endif
