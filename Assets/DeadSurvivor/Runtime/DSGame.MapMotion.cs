using System;
using System.Linq;
using UnityEngine;

namespace DeadSurvivor
{
    public partial class DSGame
    {
        DSMotionProposal PlanPartyMotion(float dt)
        {
#if UNITY_EDITOR
            if(movementFixture!=null)
                return movementFixture.Propose(this,dt);
#endif
            if(MapSession)return DSMapMotion.Propose(MapSession.Geometry,Center,Team.Select(c=>c.pos).ToArray(),
                Enumerable.Range(0,Team.Count).Select(Offset).ToArray(),Team.Select(c=>c.hp>0).ToArray(),Enumerable.Repeat(8f,Team.Count).ToArray(),8,MoveInput,dt);
            Vector2 center=Center+MoveInput*140*dt;
            var actual=Team.Select((c,i)=>c.hp<=0 ? c.pos : Vector2.Lerp(c.pos,center+Offset(i),Mathf.Min(1,dt*12))).ToArray();
            return new DSMotionProposal(center,actual,1,false);
        }

        bool RejectMovementFixtureCommand(string command)
        {
#if UNITY_EDITOR
            if(movementFixture!=null)
            {
                movementFixture.Fail("EXTERNAL_COMMAND:"+command);
                return true;
            }
#endif
            return false;
        }
        void NoteMovementFixtureCombat()
        {
#if UNITY_EDITOR
            movementFixture?.CombatCall();
#endif
        }

#if UNITY_EDITOR
        DSMovementFixture movementFixture;
        bool fixtureFramePermit;
        public void BindMovementFixture(DSMovementFixture fixture)
        {
            if(enabled || movementFixture!=null || fixture==null || State!="battle" || Enemies.Count!=0 || Shots.Count!=0)
                throw new InvalidOperationException("INVALID_FIXTURE_BINDING");
            fixture.ValidateInitial(this);
            movementFixture=fixture;
        }
        internal void SimulateFixtureFrame(float dt)
        {
            if(fixtureFramePermit) throw new InvalidOperationException("UNEXPECTED_STEP");
            fixtureFramePermit=true;
            try { Simulate(dt); }
            finally { fixtureFramePermit=false; }
        }
        void SimulateFixture(float requested)
        {
            if(!fixtureFramePermit) throw new InvalidOperationException("UNOWNED_SIMULATE");
            if(!DSMapGeometry.Finite(requested) || requested<0 || requested>1) throw new ArgumentException("INVALID_DT");
            float dt=Mathf.Min(requested,Mathf.Max(0,Duration-Elapsed));
            var motion=PlanPartyMotion(dt);
            Center=motion.Center;
            for(int i=0;i<Team.Count;i++)
                if(Team[i].hp>0)
                {
                    Team[i].pos=motion.Actual[i];
                    Team[i].aliveTime+=dt;
                    Team[i].flash=Mathf.Max(0,Team[i].flash-dt);
                }
            Elapsed+=dt;TotalTime+=dt;
            movementFixture.Applied(dt,motion);
        }
#endif
    }
}
