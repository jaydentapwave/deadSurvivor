// 투사체(기획서 A절): range × approachRatio보다 멀면 다가가고 range × retreatRatio보다 가까우면 같은 속도로 물러난다.
// range 안이고 대기가 끝났으면 표적 쪽 투사체 하나, 대기 = projectile.interval. 접촉 공격 없음.
public class ProjectileBehavior : IMonsterBehavior
{
    private readonly MonsterModel _monsterModel;

    // 판이 바뀌어도 이어서 늘린다(적 번호와 같은 이유: 그림 등록부에 지난 판 그림이 남아 있을 수 있다)
    private int _nextShotId;

    public ProjectileBehavior(MonsterModel monsterModel)
    {
        _monsterModel = monsterModel;
    }

    public bool CanHandle(MonsterBehavior behavior) => behavior == MonsterBehavior.Projectile;

    public void Tick(Monster monster, PartyMember target, float deltaSeconds)
    {
        var data = monster.Data;
        var projectile = data.Projectile;
        var toTarget = target.Position - monster.Position;
        var distance = toTarget.magnitude;
        // 방향은 움직이기 전 표적 쪽(예전 규칙)
        var direction = toTarget / distance;
        if (distance > projectile.Range * projectile.ApproachRatio)
        {
            monster.Position += direction * data.Speed * deltaSeconds;
        }
        else if (distance < projectile.Range * projectile.RetreatRatio)
        {
            monster.Position -= direction * data.Speed * deltaSeconds;
        }

        if (distance < projectile.Range && monster.AttackClock <= 0f)
        {
            _nextShotId++;
            _monsterModel.AddEnemyShot(new EnemyShot(_nextShotId, monster.Position, direction, projectile.ShotSpeed, monster.Damage,
                projectile.Range * projectile.ShotTravelScale, projectile.ShotHitRadius, projectile.ShotColor));
            monster.AttackClock = projectile.Interval;
        }
    }
}
