using System.Linq;
using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSGame
    {
        public DSMapSession MapSession;
        public string MapRejection {get;private set;}="";
        bool MapAllows(int formation,int revive=-1)
        {
            if(!MapSession)return true;
            bool safe=DSMapPartyRules.CanTransition(MapSession.Geometry,Center,Formation,formation,Team.ToArray(),revive);
            if(!safe)RejectMap();else MapRejection="";
            return safe;
        }
        void RejectMap()
        {MapRejection="MAP_PARTY_POSE_BLOCKED";UI?.Toast("장애물과 겹치는 진형입니다. 넓은 곳으로 이동한 뒤 다시 시도하세요.");}
        void MapOnlyStep(float dt)
        {
            var motion=PlanPartyMotion(dt);Center=motion.Center;
            for(int i=0;i<Team.Count;i++)if(Team[i].hp>0){Team[i].pos=motion.Actual[i];Team[i].aliveTime+=dt;Team[i].flash=Mathf.Max(0,Team[i].flash-dt);}
            Elapsed+=dt;TotalTime+=dt;
            if(Elapsed>=Duration)FinishWave();
        }
    }
}
