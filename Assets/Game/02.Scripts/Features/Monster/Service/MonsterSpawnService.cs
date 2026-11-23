using UnityEngine;

// 정규·버닝 생성, 가중치 뽑기, 생성 방향, 자폭형 무리(기획서 B절, 예전 DSGame.Combat의 Spawn·Burning·Heading).
// 생성 전용 난수 두 갈래(유형·자리)를 판 시드에서 만들어 판 끝까지 이어 쓴다. 유형 갈래는 움직임과 상관없이 같은 순서로 쓰인다.
public class MonsterSpawnService
{
    private const float HalfPi = Mathf.PI * 0.5f;
    // 버닝 낼 수 식에서 창 끝을 창 안으로 당기는 몫(예전 규칙 start + 10 − 0.0001)
    private const float BurningWindowEndEpsilon = 1e-4f;

    private readonly MonsterModel _monsterModel;
    private readonly PartyModel _partyModel;
    private readonly MonsterRepository _monsterRepository;
    private readonly RunRulesData _runRules;

    private System.Random _typeRandom = new(0);
    private System.Random _positionRandom = new(0);
    private WaveData _waveData;
    private int _nextMonsterId;

    public MonsterSpawnService(MonsterModel monsterModel, PartyModel partyModel, MonsterRepository monsterRepository,
        RunRulesData runRules)
    {
        _monsterModel = monsterModel;
        _partyModel = partyModel;
        _monsterRepository = monsterRepository;
        _runRules = runRules;
    }

    public void BeginRun(int seed)
    {
        var seedRandom = new System.Random(seed);
        _typeRandom = new System.Random(seedRandom.Next());
        _positionRandom = new System.Random(seedRandom.Next());
    }

    // 생성 수 = round(웨이브 적 수 × 배율), 간격 = max(0, 웨이브 시간 − 끝 여유) ÷ max(1, 수 − 1). 시계 0이라 첫 걸음에 하나 나온다
    public void BeginWave(WaveData waveData, float enemyCountScale)
    {
        var rules = _monsterRepository.SpawnRules;
        _waveData = waveData;
        _monsterModel.ResetWaveCounts();
        _monsterModel.SpawnTarget = Mathf.RoundToInt(waveData.SpawnCount * enemyCountScale);
        _monsterModel.SpawnInterval = Mathf.Max(0f, waveData.Duration - rules.SpawnEndGap) / Mathf.Max(1, _monsterModel.SpawnTarget - 1);
        _monsterModel.SpawnClock = 0f;
        _monsterModel.BombGroupCount = 0;
        _monsterModel.BombGroupAngle = 0f;
        _monsterModel.BurningAngle = NextPositionRoll() * Mathf.PI * 2f;
    }

    // 정규 생성 → 버닝 타임 추가 생성. elapsedSeconds는 이번 걸음까지 지난 웨이브 시간
    public void Tick(float deltaSeconds, float elapsedSeconds, bool isBurningTime, float burningStartSeconds)
    {
        _monsterModel.SpawnClock -= deltaSeconds;
        while (_monsterModel.SpawnedCount < _monsterModel.SpawnTarget && _monsterModel.SpawnClock <= 0f)
        {
            SpawnRegular();
            _monsterModel.SpawnedCount++;
            _monsterModel.SpawnClock += _monsterModel.SpawnInterval;
        }

        // 창이 시작된 뒤에는 창 끝을 넘는 걸음에서도 남은 몫을 낸다(낼 수 식이 창 끝에서 멈춘다, 예전 Burning 규칙).
        // isBurningTime 깃발만 보면 창을 넘는 걸음의 마지막 몫을 놓친다
        var hasBurningStarted = isBurningTime || (_waveData.HasBurningTime && elapsedSeconds >= burningStartSeconds);
        if (!hasBurningStarted || _monsterModel.SpawnTarget == 0)
        {
            return;
        }

        var rules = _monsterRepository.SpawnRules;
        var burningSeconds = _runRules.BurningSeconds;
        var interval = Mathf.Max(0.01f, _monsterModel.SpawnInterval * rules.BurningIntervalScale);
        var maxCount = Mathf.CeilToInt(burningSeconds / interval);
        var windowElapsed = Mathf.Min(elapsedSeconds, burningStartSeconds + burningSeconds - BurningWindowEndEpsilon) - burningStartSeconds;
        var dueCount = Mathf.Min(maxCount, Mathf.FloorToInt(windowElapsed / interval) + 1);
        while (_monsterModel.BurningSpawnedCount < dueCount)
        {
            var data = _monsterRepository.Get(WeightedPick.Pick(rules.BurningTypeWeights, _typeRandom.NextDouble()));
            var side = (_monsterModel.BurningSpawnedCount % 2) * Mathf.PI;
            var spread = (NextPositionRoll() - 0.5f) * rules.BurningSpread;
            Spawn(data, _monsterModel.BurningAngle + side + spread);
            _monsterModel.BurningSpawnedCount++;
        }
    }

    // 원정대가 멈춰 있으면 무작위 각, 움직이면 이동 방향의 앞·왼쪽·오른쪽·뒤를 비율로 골라 흔들림을 더한다(자리 난수)
    public float PickHeadingAngle()
    {
        if (!_monsterModel.IsPartyMoving)
        {
            return NextPositionRoll() * Mathf.PI * 2f;
        }

        var heading = _monsterRepository.SpawnRules.Heading;
        var roll = NextPositionRoll();
        float sector;
        if (roll < heading.Front)
        {
            sector = 0f;
        }
        else if (roll < heading.Front + heading.Left)
        {
            sector = HalfPi;
        }
        else if (roll < heading.Front + heading.Left + heading.Right)
        {
            sector = -HalfPi;
        }
        else
        {
            sector = Mathf.PI;
        }

        var partyHeading = _monsterModel.PartyHeading;
        return Mathf.Atan2(partyHeading.y, partyHeading.x) + sector + (NextPositionRoll() - 0.5f) * heading.Jitter;
    }

    // 유형은 웨이브 가중치(유형 난수). 자폭형은 groupSize마리마다 새 방향, 나머지는 그 방향 ± groupSpread / 2
    private void SpawnRegular()
    {
        var data = _monsterRepository.Get(WeightedPick.Pick(_waveData.TypeWeights, _typeRandom.NextDouble()));
        float angle;
        if (data.Behavior == MonsterBehavior.Bomb)
        {
            var bomb = data.Bomb;
            if (_monsterModel.BombGroupCount % bomb.GroupSize == 0)
            {
                _monsterModel.BombGroupAngle = PickHeadingAngle();
            }

            _monsterModel.BombGroupCount++;
            angle = _monsterModel.BombGroupAngle + (NextPositionRoll() - 0.5f) * bomb.GroupSpread;
        }
        else
        {
            angle = PickHeadingAngle();
        }

        Spawn(data, angle);
        _monsterModel.AddSpawnCount(data.Behavior);
    }

    // 원정대 중심에서 보이는 바닥 영역 대각선 절반 + 생성 여유. HP·피해 = 몬스터 값 × 웨이브 배수
    private void Spawn(MonsterData data, float angle)
    {
        var rules = _monsterRepository.SpawnRules;
        var distance = _monsterModel.ViewHalfExtent.magnitude + rules.SpawnMargin;
        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        // Limits and positions are gameplay floor units, not rendered metres.
        var limit = _runRules.FieldCells / (2f * _runRules.UnitScale) - rules.FieldEdgeMargin;
        var position = ResolvePlacement(direction, distance, rules.SpawnMargin, limit, out var constrained);
        AddMonster(data, position, _waveData.HpScale, _waveData.DamageScale).PlacementBoundaryConstrained = constrained;
    }

    public Vector2 ResolvePlacement(Vector2 direction, float distance, float margin, float limit, out bool constrained)
    {
        var model = _monsterModel;
        if (model.PlacementFrame.Height == 0)
            throw new System.InvalidOperationException("[MonsterSpawnService] Perspective placement must be initialized before spawn.");
        var scale = model.PlacementScale;
        var center = _partyModel.Center * scale;
        var focus = IsoProjection.FloorToWorld(_partyModel.Center, scale, model.PlacementTerrain);
        var old = model.PlacementFrame;
        var frame = new PerspectiveFrame(old.Position + focus - model.PlacementFocus, old.Rotation,
            old.FieldOfView, old.Aspect, old.Height, old.Near, old.Far);
        return PerspectivePlacementMath.Resolve(frame, model.PlacementTerrain, center, direction,
            distance * scale, limit * scale, margin * scale, out constrained) / scale;
    }

    public Monster SpawnEncounterGuard(Vector2 position)
    {
        var rules = _monsterRepository.SpawnRules;
        var limit = _runRules.FieldCells / (2f * _runRules.UnitScale) - rules.FieldEdgeMargin;
        var data = _monsterRepository.Get("slime");
        var monster = AddMonster(data, PartyMoveMath.ClampToDiamond(position, limit), 0.65f, 0.55f);
        monster.IsWaitingForEncounter = true;
        return monster;
    }

    private Monster AddMonster(MonsterData data, Vector2 position, float hpScale, float damageScale)
    {
        var monster = new Monster(++_nextMonsterId, data, position, data.Hp * hpScale, data.Damage * damageScale,
            _monsterRepository.SpawnRules.InitialAttackDelay);
        monster.PlacementVersion++;
        _monsterModel.Add(monster);
        return monster;
    }

    private float NextPositionRoll() => (float)_positionRandom.NextDouble();
}
