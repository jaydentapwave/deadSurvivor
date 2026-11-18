// 몬스터 행동 한 종류(기획서 A절 표). MonsterBehaviorService가 산 몬스터마다 공격 대기를 줄이고 표적을 찾은 뒤 부른다.
public interface IMonsterBehavior
{
    bool CanHandle(MonsterBehavior behavior);

    // target = 가장 가까운 산 대원(null이 아님)
    void Tick(Monster monster, PartyMember target, float deltaSeconds);
}
