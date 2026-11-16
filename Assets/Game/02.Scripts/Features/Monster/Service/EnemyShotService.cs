// 적 투사체 이동·명중(기획서 A절, 예전 DSGame.Combat.Simulate의 enemy 투사체).
// 걸음마다 shotSpeed로 움직이고, 지나간 선분과 명중 거리 안의 첫 산 대원 하나에게 피해 후 사라진다. 최대 이동에 닿으면 사라진다.
public class EnemyShotService
{
    private readonly MonsterModel _monsterModel;
    private readonly PartyModel _partyModel;
    private readonly PartyService _partyService;

    public EnemyShotService(MonsterModel monsterModel, PartyModel partyModel, PartyService partyService)
    {
        _monsterModel = monsterModel;
        _partyModel = partyModel;
        _partyService = partyService;
    }

    public void Tick(float deltaSeconds)
    {
        foreach (var shot in _monsterModel.EnemyShotList)
        {
            if (!shot.IsAlive)
            {
                continue;
            }

            var step = shot.Speed * deltaSeconds;
            var from = shot.Position;
            shot.Position = from + shot.Direction * step;
            shot.Travel += step;
            foreach (var member in _partyModel.MemberList)
            {
                if (member.IsAlive && SegmentMath.Distance(from, shot.Position, member.Position) < shot.HitRadius)
                {
                    _partyService.ApplyDamage(member, shot.Damage);
                    shot.IsAlive = false;
                    break;
                }
            }

            if (shot.Travel >= shot.MaxTravel)
            {
                shot.IsAlive = false;
            }
        }
    }
}
