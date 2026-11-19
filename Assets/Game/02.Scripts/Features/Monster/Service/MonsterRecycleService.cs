using System.Collections.Generic;
using UnityEngine;

// 멀어진 적 재배치와 원정대 이동 방향(기획서 B절, 예전 DSGame.Combat.Recycle).
public class MonsterRecycleService
{
    // 원정대 중심 변화가 이보다 짧으면 멈춘 것으로 본다(바닥 단위)
    private const float MinHeadingLength = 1e-4f;
    // 상자 가장자리 계산에서 0으로 나누지 않게(예전 규칙)
    private const float MinAxis = 1e-8f;

    private readonly MonsterModel _monsterModel;
    private readonly PartyModel _partyModel;
    private readonly MonsterRepository _monsterRepository;
    private readonly MonsterSpawnService _monsterSpawnService;
    private readonly float _fieldLimit;

    // 밖 시간이 긴 순(같으면 목록 순)으로 줄 세운 재배치 후보
    private readonly List<Monster> _eligibleList = new();

    public MonsterRecycleService(MonsterModel monsterModel, PartyModel partyModel, MonsterRepository monsterRepository,
        MonsterSpawnService monsterSpawnService, RunRulesData runRules)
    {
        _monsterModel = monsterModel;
        _partyModel = partyModel;
        _monsterRepository = monsterRepository;
        _monsterSpawnService = monsterSpawnService;
        _fieldLimit = runRules.FieldCells / (2f * runRules.UnitScale) - monsterRepository.SpawnRules.FieldEdgeMargin;
    }

    public void BeginWave()
    {
        _monsterModel.RecycleClock = 0f;
        _monsterModel.LastPartyCenter = _partyModel.Center;
        _monsterModel.PartyHeading = Vector2.zero;
    }

    // 지난 걸음 대비 원정대 중심 변화. 짧으면 0(멈춤)
    public void UpdatePartyHeading()
    {
        var delta = _partyModel.Center - _monsterModel.LastPartyCenter;
        _monsterModel.PartyHeading = delta.magnitude < MinHeadingLength ? Vector2.zero : delta;
        _monsterModel.LastPartyCenter = _partyModel.Center;
    }

    public void Tick(float deltaSeconds)
    {
        var recycle = _monsterRepository.SpawnRules.Recycle;
        var halfExtent = _monsterModel.ViewHalfExtent;
        var center = _partyModel.Center;
        _monsterModel.RecycleClock = Mathf.Max(0f, _monsterModel.RecycleClock - deltaSeconds);
        _eligibleList.Clear();
        foreach (var monster in _monsterModel.MonsterList)
        {
            var offset = monster.Position - center;
            var isOutside = Mathf.Abs(offset.x) > halfExtent.x + recycle.OutsideMargin
                || Mathf.Abs(offset.y) > halfExtent.y + recycle.OutsideMargin;
            var isBehind = !_monsterModel.IsPartyMoving || Vector2.Dot(offset, _monsterModel.PartyHeading) < 0f;
            if (!monster.IsAlive || monster.Phase != MonsterPhase.Approach || !isOutside || !isBehind)
            {
                monster.OutsideSeconds = 0f;
                continue;
            }

            monster.OutsideSeconds += deltaSeconds;
            if (monster.OutsideSeconds >= recycle.HoldSeconds)
            {
                InsertByOutsideSeconds(monster);
            }
        }

        // 대기는 실제로 옮긴 뒤에만 건다(예전 규칙)
        if (_monsterModel.RecycleClock > 0f || _eligibleList.Count == 0)
        {
            return;
        }

        var count = Mathf.Min(recycle.MaxPerCheck, _eligibleList.Count);
        for (var i = 0; i < count; i++)
        {
            var monster = _eligibleList[i];
            var angle = _monsterSpawnService.PickHeadingAngle();
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var distance = Mathf.Min((halfExtent.x + recycle.PlaceMargin) / Mathf.Max(MinAxis, Mathf.Abs(direction.x)),
                (halfExtent.y + recycle.PlaceMargin) / Mathf.Max(MinAxis, Mathf.Abs(direction.y)));
            monster.Position = PartyMoveMath.ClampToDiamond(center + direction * distance, _fieldLimit);
            monster.PlacementVersion++;
            monster.OutsideSeconds = 0f;
            monster.AttackClock = Mathf.Max(recycle.MinAttackDelay, monster.AttackClock);
            monster.FlockDirection = Vector2.zero;
        }

        _monsterModel.RecycleClock = recycle.CheckInterval;
    }

    // 같은 밖 시간이면 먼저 들어온(목록 앞) 것이 앞에 남는다
    private void InsertByOutsideSeconds(Monster monster)
    {
        var index = _eligibleList.Count;
        while (index > 0 && _eligibleList[index - 1].OutsideSeconds < monster.OutsideSeconds)
        {
            index--;
        }

        _eligibleList.Insert(index, monster);
    }
}
