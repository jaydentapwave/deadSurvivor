using UnityEngine;

// 임시 적 한 종류의 생성·이동·접촉 공격(기획서 E절, 예전 DSGame.Combat의 Spawn·Burning·UpdateEnemy). F4가 바꾼다.
public class MonsterService
{
    // 생성 간격 = (웨이브 시간 − 4) ÷ (수 − 1): 마지막 적이 웨이브 끝 4초 전에 나온다(예전 규칙)
    private const float SpawnEndMarginSeconds = 4f;
    // 버닝 추가 생성 방향의 흔들림(라디안, 예전 규칙)
    private const float BurningAngleJitter = 0.3f;
    // 접촉 거리에 멈춘 적의 거리 비교 여유(바닥 단위). 실수 오차로 접촉 직전에 계속 멈추지 않게
    private const float ContactTolerance = 0.001f;

    private readonly MonsterModel _monsterModel;
    private readonly PartyModel _partyModel;
    private readonly PartyService _partyService;
    private readonly PlaceholderCombatData _combatData;
    private readonly RunRulesData _runRules;

    private System.Random _random = new(0);
    private Vector2 _viewHalfExtent;
    private int _nextMonsterId;
    private int _spawnTarget;
    private int _spawnedCount;
    private float _spawnClock;
    private float _spawnInterval;
    private int _burningSpawnedCount;
    private float _burningAngle;

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

    public MonsterService(MonsterModel monsterModel, PartyModel partyModel, PartyService partyService,
        PlaceholderCombatData combatData, RunRulesData runRules)
    {
        _monsterModel = monsterModel;
        _partyModel = partyModel;
        _partyService = partyService;
        _combatData = combatData;
        _runRules = runRules;
    }

    // 카메라가 보는 바닥 영역의 반 크기(바닥 단위). 생성 거리 = 이 대각선 + spawnMargin
    public void SetViewHalfExtent(Vector2 floorHalfExtent) => _viewHalfExtent = floorHalfExtent;

    // 적 번호는 판이 바뀌어도 이어서 늘린다(같은 프레임에 로비 → 출발하면 지난 판 적의 View가 등록부에 남아 있을 수 있다)
    public void BeginRun(int seed)
    {
        _random = new System.Random(seed);
        _monsterModel.Clear();
    }

    public void BeginWave(float waveSeconds, float enemyCountScale)
    {
        _monsterModel.Clear();
        _spawnTarget = Mathf.RoundToInt(_combatData.SpawnCount * enemyCountScale);
        _spawnedCount = 0;
        _spawnClock = 0f;
        _spawnInterval = Mathf.Max(0f, waveSeconds - SpawnEndMarginSeconds) / Mathf.Max(1, _spawnTarget - 1);
        _burningSpawnedCount = 0;
        _burningAngle = NextAngle();
    }

    public void ClearMonsters() => _monsterModel.Clear();

    // 일반 생성과 버닝 타임 추가 생성. elapsedSeconds는 이번 걸음까지 지난 웨이브 시간
    public void TickSpawn(float deltaSeconds, float elapsedSeconds, bool isBurningTime, float burningStartSeconds)
    {
        _spawnClock -= deltaSeconds;
        while (_spawnedCount < _spawnTarget && _spawnClock <= 0f)
        {
            Spawn(NextAngle());
            _spawnedCount++;
            _spawnClock += _spawnInterval;
        }

        if (!isBurningTime || _spawnTarget == 0)
        {
            return;
        }

        // 창 동안 생성 간격 × burningIntervalScale마다 반대쪽 두 방향에서 번갈아 1마리씩
        var interval = Mathf.Max(0.01f, _spawnInterval * _combatData.BurningIntervalScale);
        var maxCount = Mathf.CeilToInt(_runRules.BurningSeconds / interval);
        var dueCount = Mathf.Min(maxCount, Mathf.FloorToInt((elapsedSeconds - burningStartSeconds) / interval) + 1);
        while (_burningSpawnedCount < dueCount)
        {
            var side = (_burningSpawnedCount % 2) * Mathf.PI;
            var jitter = ((float)_random.NextDouble() - 0.5f) * BurningAngleJitter;
            Spawn(_burningAngle + side + jitter);
            _burningSpawnedCount++;
        }
    }

    // 가장 가까운 산 대원에게 직진, 접촉 거리면 attackInterval마다 접촉 피해
    public void TickMove(float deltaSeconds)
    {
        var enemy = _combatData.Enemy;
        var contactDistance = enemy.Radius + enemy.ContactGap;
        foreach (var monster in _monsterModel.MonsterList)
        {
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

            var toTarget = target.Position - monster.Position;
            var distance = toTarget.magnitude;
            if (distance > contactDistance + ContactTolerance)
            {
                var travel = Mathf.Min(enemy.Speed * deltaSeconds, distance - contactDistance);
                monster.Position += toTarget / distance * travel;
            }
            else if (monster.AttackClock <= 0f)
            {
                _partyService.ApplyDamage(target, enemy.Damage);
                monster.AttackClock = enemy.AttackInterval;
            }
        }
    }

    // 쓰러진 적을 목록에서 빼고 뺀 수(처치 수)를 돌려준다
    public int RemoveDead() => _monsterModel.RemoveDead();

    private void Spawn(float angle)
    {
        var distance = _viewHalfExtent.magnitude + _combatData.SpawnMargin;
        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        _nextMonsterId++;
        _monsterModel.Add(new Monster(_nextMonsterId, _partyModel.Center + direction * distance, _combatData.Enemy.Hp));
    }

    private float NextAngle() => (float)_random.NextDouble() * Mathf.PI * 2f;
}
