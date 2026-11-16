using System;
using System.Collections.Generic;

// 산 몬스터를 목록 순서로 행동시킨다(기획서 A절 공통 규칙). 행동별 규칙은 IMonsterBehavior 구현이 맡는다.
public class MonsterBehaviorService
{
    private readonly MonsterModel _monsterModel;
    private readonly PartyService _partyService;
    // MonsterBehavior 값 → 행동 객체
    private readonly IMonsterBehavior[] _behaviorTable;

    public MonsterBehaviorService(MonsterModel monsterModel, PartyService partyService, IReadOnlyList<IMonsterBehavior> behaviorList)
    {
        _monsterModel = monsterModel;
        _partyService = partyService;
        var kinds = (MonsterBehavior[])Enum.GetValues(typeof(MonsterBehavior));
        _behaviorTable = new IMonsterBehavior[kinds.Length];
        foreach (var kind in kinds)
        {
            foreach (var behavior in behaviorList)
            {
                if (behavior.CanHandle(kind))
                {
                    _behaviorTable[(int)kind] = behavior;
                    break;
                }
            }

            if (_behaviorTable[(int)kind] == null)
            {
                GameLogger.LogError($"[MonsterBehaviorService] No behavior for {kind}");
            }
        }
    }

    // 공격 대기는 매 걸음 준다. 산 대원이 없으면 멈춘다. 행동 중 목록이 늘지 않으므로 인덱스로 돈다
    public void Tick(float deltaSeconds)
    {
        var monsterList = _monsterModel.MonsterList;
        for (var i = 0; i < monsterList.Count; i++)
        {
            var monster = monsterList[i];
            if (!monster.IsAlive)
            {
                continue;
            }

            monster.AttackClock -= deltaSeconds;
            var target = _partyService.FindNearestAlive(monster.Position);
            if (target == null)
            {
                continue;
            }

            _behaviorTable[(int)monster.Behavior].Tick(monster, target, deltaSeconds);
        }
    }
}
