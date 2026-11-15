using VContainer.Unity;

// 몬스터 상태 → 그림의 상태 표시(돌진 방향 선·폭발 반경 원, 기획서 F절). 그림을 만들고 지우는 일은 WorldPresenter가 한다.
public class MonsterStatePresenter : ITickable
{
    private readonly MonsterModel _monsterModel;
    private readonly MonsterViewRegistry _monsterViewRegistry;

    public MonsterStatePresenter(MonsterModel monsterModel, MonsterViewRegistry monsterViewRegistry)
    {
        _monsterModel = monsterModel;
        _monsterViewRegistry = monsterViewRegistry;
    }

    // 이번 프레임에 막 나온 몬스터는 그림이 아직 없을 수 있다(다음 프레임에 맞춘다)
    public void Tick()
    {
        foreach (var monster in _monsterModel.MonsterList)
        {
            if (monster.IsAlive && _monsterViewRegistry.TryGet(monster.Id, out var view))
            {
                view.UpdateState(monster.Phase, monster.DashDirection);
            }
        }
    }
}
