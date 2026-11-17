using UnityEngine;

// 몬스터 5종의 생성·이동·공격(F4 기획서 A·B절). RunService.Step의 ③ 생성, ④ 처치 정리, ⑤ 이동·공격 자리를 그대로 두고
// 안쪽은 생성·재배치·행동·적 투사체 서비스에 맡긴다.
public class MonsterService
{
    private readonly MonsterModel _monsterModel;
    private readonly MonsterSpawnService _monsterSpawnService;
    private readonly MonsterRecycleService _monsterRecycleService;
    private readonly MonsterBehaviorService _monsterBehaviorService;
    private readonly EnemyShotService _enemyShotService;
    private readonly CombatFeedbackModel _combatFeedbackModel;

    // 살아 있는 몬스터 수(HUD 위협). 적 투사체는 세지 않는다
    public int AliveCount
    {
        get
        {
            var count = 0;
            foreach (var monster in _monsterModel.MonsterList)
            {
                if (monster.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public MonsterService(MonsterModel monsterModel, MonsterSpawnService monsterSpawnService,
        MonsterRecycleService monsterRecycleService, MonsterBehaviorService monsterBehaviorService, EnemyShotService enemyShotService,
        CombatFeedbackModel combatFeedbackModel)
    {
        _monsterModel = monsterModel;
        _monsterSpawnService = monsterSpawnService;
        _monsterRecycleService = monsterRecycleService;
        _monsterBehaviorService = monsterBehaviorService;
        _enemyShotService = enemyShotService;
        _combatFeedbackModel = combatFeedbackModel;
    }

    // 카메라가 보는 바닥 영역의 반 크기(바닥 단위). 생성 거리 = 이 대각선 + 생성 여유, 재배치 상자도 이 값
    public void SetViewHalfExtent(Vector2 floorHalfExtent) => _monsterModel.ViewHalfExtent = floorHalfExtent;

    // 몬스터 번호는 판이 바뀌어도 이어서 늘린다(같은 프레임에 로비 → 출발하면 지난 판 적의 View가 등록부에 남아 있을 수 있다)
    public void BeginRun(int seed)
    {
        _monsterSpawnService.BeginRun(seed);
        _monsterModel.Clear();
    }

    public void BeginWave(WaveData waveData, float enemyCountScale)
    {
        _monsterModel.Clear();
        _monsterSpawnService.BeginWave(waveData, enemyCountScale);
        _monsterRecycleService.BeginWave();
    }

    // 몬스터·적 투사체를 비운다. 이번 웨이브 셈은 남긴다(웨이브 끝 기록이 읽는다)
    public void ClearMonsters() => _monsterModel.Clear();

    // 정규 생성과 버닝 타임 추가 생성. elapsedSeconds는 이번 걸음까지 지난 웨이브 시간
    public void TickSpawn(float deltaSeconds, float elapsedSeconds, bool isBurningTime, float burningStartSeconds)
    {
        _monsterSpawnService.Tick(deltaSeconds, elapsedSeconds, isBurningTime, burningStartSeconds);
    }

    // 원정대 이동 방향 → 재배치 → 산 몬스터 행동 → 적 투사체 → 폭발로 사라진 몬스터·사라진 투사체 정리(A절 걸음 순서).
    // 여기서 빠지는 몬스터는 처치 수에 들지 않는다(무기로 쓰러진 적은 ④ RemoveDead에서 이미 빠졌다)
    public void TickMove(float deltaSeconds)
    {
        _monsterRecycleService.UpdatePartyHeading();
        _monsterRecycleService.Tick(deltaSeconds);
        _monsterBehaviorService.Tick(deltaSeconds);
        _enemyShotService.Tick(deltaSeconds);
        _monsterModel.RemoveDead();
        _monsterModel.RemoveDeadShots();
    }

    // 무기·파동·폭발이 적에게 주는 피해(F3가 부르는 곳). 이미 쓰러진 적은 무시하고 기록도 남기지 않는다.
    // 끝에 피드백 기록 한 줄(F6 A절: 준 값, 맞은 순간의 바닥 좌표, 숫자 자리를 정할 반지름)
    public void ApplyDamage(Monster monster, float amount)
    {
        if (!monster.IsAlive)
        {
            return;
        }

        monster.Hp -= amount;
        _combatFeedbackModel.AddMonsterHit(monster.Id, amount, monster.Position, monster.Data);
    }

    // 적 반지름(투사체 명중). 몬스터 종류별 값(F4)
    public float GetRadius(Monster monster) => monster.Data.Radius;

    // 쓰러진 적을 목록에서 빼고 뺀 수(처치 수)를 돌려준다
    public int RemoveDead() => _monsterModel.RemoveDead();
}
