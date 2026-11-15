#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using R3;
using UnityEngine;

// 판정용 웨이브 기록기(F4 기획서 E절). PlayableProbe.StartWaveMetrics·UseRealTimeAtWave가 만든다. 게임 코드는 이 클래스를 모른다.
// RunModel.Phase가 Battle에서 바뀌는 순간(구독) 그 웨이브의 셈을 MonsterModel에서 읽어 적고 파일 전체를 다시 쓴다.
// 판정기 tools/wave_targets.py --metrics가 이 파일을 읽는다.
public class PlayableMetricsRecorder : MonoBehaviour
{
    private const float BurningPercentile = 0.95f;

    private readonly MetricsFile _metricsFile = new();

    private IDisposable _phaseSubscription;
    private RunPhase _lastPhase = RunPhase.Lobby;
    private int _lastRunCount = -1;
    private string _path;
    private int _realTimeWave;
    private RunRecord _currentRun;

    public bool IsSpawnCountAsData => LastRun != null && LastRun.IsSpawnCountAsData;
    public float BurningFrameMsP95 => LastRun != null ? LastRun.Burning.P95Ms : -1f;
    public int MaxAliveEnemies => LastRun != null ? LastRun.MaxAliveEnemies : 0;

    private RunRecord LastRun => _metricsFile.RunList.Count > 0 ? _metricsFile.RunList[_metricsFile.RunList.Count - 1] : null;

    private void Update()
    {
        if (_currentRun == null || _lastPhase != RunPhase.Battle)
        {
            return;
        }

        var aliveCount = PlayableProbe.Resolve<MonsterService>().AliveCount;
        _currentRun.NoteAlive(aliveCount);
        var runModel = PlayableProbe.Resolve<RunModel>();
        var isLastWave = runModel.Wave.CurrentValue == PlayableProbe.Resolve<WaveRepository>().WaveDataList.Count;
        // M7은 사람이 보는 1배 시간의 프레임만 잰다
        if (isLastWave && runModel.IsBurningTime.CurrentValue && Mathf.Approximately(Time.timeScale, 1f))
        {
            _currentRun.Burning.AddSample(Time.unscaledDeltaTime * 1000f, aliveCount);
        }
    }

    private void OnDestroy() => _phaseSubscription?.Dispose();

    // 이후 모든 판을 path에 기록한다(이전 기록은 버린다)
    public void StartRecording(string path)
    {
        _path = path;
        _metricsFile.RunList.Clear();
        _currentRun = null;
        _lastRunCount = -1;
        EnsureSubscribed();
    }

    public void UseRealTimeAtWave(int wave)
    {
        _realTimeWave = wave;
        EnsureSubscribed();
    }

    public int SpawnedCountOf(int wave)
    {
        var record = LastRun?.Find(wave);
        return record != null ? record.Spawned : -1;
    }

    public bool IsSpawnCountNear(int first, int last, float tolerance)
    {
        var run = LastRun;
        var waveCount = PlayableProbe.Resolve<WaveRepository>().WaveDataList.Count;
        if (run == null || run.FirstWave != 1 || run.WaveList.Count != waveCount)
        {
            return false;
        }

        foreach (var record in run.WaveList)
        {
            var target = Mathf.Round(first + (last - first) * (record.Wave - 1) / (float)Mathf.Max(1, waveCount - 1));
            if (!record.ReachedEnd || Mathf.Abs(record.Spawned - target) > tolerance * target)
            {
                return false;
            }
        }

        return true;
    }

    public int FirstWaveOf(string behaviorName)
    {
        if (!Enum.TryParse(behaviorName, true, out MonsterBehavior behavior))
        {
            GameLogger.LogWarning($"[PlayableMetricsRecorder] Unknown behavior {behaviorName}");
            return 0;
        }

        for (var i = _metricsFile.RunList.Count - 1; i >= 0; i--)
        {
            var run = _metricsFile.RunList[i];
            if (run.FirstWave != 1)
            {
                continue;
            }

            foreach (var record in run.WaveList)
            {
                if (record.CountOf(behavior) > 0)
                {
                    return record.Wave;
                }
            }

            return 0;
        }

        return 0;
    }

    private void EnsureSubscribed()
    {
        if (_phaseSubscription != null)
        {
            return;
        }

        _phaseSubscription = PlayableProbe.Resolve<RunModel>().Phase.Subscribe(OnPhaseChanged);
    }

    private void OnPhaseChanged(RunPhase phase)
    {
        var runModel = PlayableProbe.Resolve<RunModel>();
        if (phase == RunPhase.Battle)
        {
            OnBattleEntered(runModel);
        }
        else if (_lastPhase == RunPhase.Battle)
        {
            OnBattleLeft(runModel, phase);
        }

        _lastPhase = phase;
    }

    private void OnBattleEntered(RunModel runModel)
    {
        var wave = runModel.Wave.CurrentValue;
        if (_realTimeWave != 0 && wave == _realTimeWave)
        {
            _realTimeWave = 0;
            Time.timeScale = 1f;
            Application.runInBackground = true;
        }

        if (_path == null || runModel.RunCount.CurrentValue == _lastRunCount)
        {
            return;
        }

        // 새 판. 시드·배율은 판을 시작한 설정(시드 0 = 시작 시각에서 뽑음)
        _lastRunCount = runModel.RunCount.CurrentValue;
        var settings = PlayableProbe.Resolve<RunSettingsModel>();
        _currentRun = new RunRecord(settings.Seed, settings.EnemyCountScale, wave);
        _metricsFile.RunList.Add(_currentRun);
    }

    // 웨이브 끝(정비) 또는 판 끝(결과)
    private void OnBattleLeft(RunModel runModel, RunPhase phase)
    {
        if (_currentRun == null)
        {
            return;
        }

        var wave = runModel.Wave.CurrentValue;
        var waveData = PlayableProbe.Resolve<WaveRepository>().WaveDataList[wave - 1];
        var isReachedEnd = phase == RunPhase.Prep || runModel.Outcome.CurrentValue == RunOutcome.Win;
        _currentRun.AddWave(new WaveRecord(wave, isReachedEnd, PlayableProbe.Resolve<MonsterModel>(),
            Mathf.RoundToInt(waveData.SpawnCount * _currentRun.EnemyCountScale)));
        var bot = FindAnyObjectByType<PlayableBot>();
        _currentRun.AddBotModes(bot != null ? bot.Mode : PlayableProbe.BotMode.None);
        if (phase == RunPhase.Result)
        {
            _currentRun.Finish(runModel.Outcome.CurrentValue, runModel.WavesSurvived.CurrentValue);
            _currentRun = null;
        }

        Write();
    }

    private void Write()
    {
        foreach (var run in _metricsFile.RunList)
        {
            run.Burning.Summarize(BurningPercentile);
        }

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonUtility.ToJson(_metricsFile, true));
    }

    [Serializable]
    private class MetricsFile
    {
        [SerializeField] private List<RunRecord> runs = new();

        public List<RunRecord> RunList => runs;
    }

    [Serializable]
    private class RunRecord
    {
        [SerializeField] private int seed;
        [SerializeField] private float enemyCountScale;
        [SerializeField] private int firstWave;
        [SerializeField] private int botModes;
        [SerializeField] private string outcome = nameof(RunOutcome.None);
        [SerializeField] private int wavesSurvived;
        [SerializeField] private int maxAliveEnemies;
        [SerializeField] private List<WaveRecord> waves = new();
        [SerializeField] private BurningRecord burning = new();

        public float EnemyCountScale => enemyCountScale;
        public int FirstWave => firstWave;
        public int MaxAliveEnemies => maxAliveEnemies;
        public IReadOnlyList<WaveRecord> WaveList => waves;
        public BurningRecord Burning => burning;

        // 끝까지 간 웨이브마다 정규 생성 수 = round(웨이브 적 수 × 배율)
        public bool IsSpawnCountAsData
        {
            get
            {
                foreach (var record in waves)
                {
                    if (record.ReachedEnd && record.Spawned != record.Expected)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public RunRecord(int seed, float enemyCountScale, int firstWave)
        {
            this.seed = seed;
            this.enemyCountScale = enemyCountScale;
            this.firstWave = firstWave;
        }

        public void NoteAlive(int aliveCount) => maxAliveEnemies = Mathf.Max(maxAliveEnemies, aliveCount);

        public void AddWave(WaveRecord record) => waves.Add(record);

        public void AddBotModes(PlayableProbe.BotMode mode) => botModes |= (int)mode;

        public void Finish(RunOutcome runOutcome, int survived)
        {
            outcome = runOutcome.ToString();
            wavesSurvived = survived;
        }

        public WaveRecord Find(int wave)
        {
            foreach (var record in waves)
            {
                if (record.Wave == wave)
                {
                    return record;
                }
            }

            return null;
        }
    }

    [Serializable]
    private class WaveRecord
    {
        [SerializeField] private int wave;
        [SerializeField] private bool reachedEnd;
        [SerializeField] private int spawned;
        [SerializeField] private int expected;
        [SerializeField] private int burningSpawned;
        [SerializeField] private int contact;
        [SerializeField] private int projectile;
        [SerializeField] private int charge;
        [SerializeField] private int armored;
        [SerializeField] private int bomb;

        public int Wave => wave;
        public bool ReachedEnd => reachedEnd;
        public int Spawned => spawned;
        public int Expected => expected;

        // 행동별 수는 정규 생성만(M3 데이터 판정 = typeWeights와 같은 기준)
        public WaveRecord(int wave, bool reachedEnd, MonsterModel monsterModel, int expected)
        {
            this.wave = wave;
            this.reachedEnd = reachedEnd;
            this.expected = expected;
            spawned = monsterModel.SpawnedCount;
            burningSpawned = monsterModel.BurningSpawnedCount;
            contact = monsterModel.SpawnCountOf(MonsterBehavior.Contact);
            projectile = monsterModel.SpawnCountOf(MonsterBehavior.Projectile);
            charge = monsterModel.SpawnCountOf(MonsterBehavior.Charge);
            armored = monsterModel.SpawnCountOf(MonsterBehavior.Armored);
            bomb = monsterModel.SpawnCountOf(MonsterBehavior.Bomb);
        }

        public int CountOf(MonsterBehavior behavior)
        {
            switch (behavior)
            {
                case MonsterBehavior.Contact:
                    return contact;
                case MonsterBehavior.Projectile:
                    return projectile;
                case MonsterBehavior.Charge:
                    return charge;
                case MonsterBehavior.Armored:
                    return armored;
                default:
                    return bomb;
            }
        }
    }

    // 마지막 웨이브 버닝 타임(1배 시간)의 프레임 시간 표본
    [Serializable]
    private class BurningRecord
    {
        [SerializeField] private float p95Ms = -1f;
        [SerializeField] private float meanMs = -1f;
        [SerializeField] private float maxMs = -1f;
        [SerializeField] private int samples;
        [SerializeField] private int maxAlive;

        [NonSerialized] private readonly List<float> _frameMsList = new();

        public float P95Ms
        {
            get
            {
                Summarize(BurningPercentile);
                return p95Ms;
            }
        }

        public void AddSample(float frameMs, int aliveCount)
        {
            _frameMsList.Add(frameMs);
            maxAlive = Mathf.Max(maxAlive, aliveCount);
        }

        // 백분위는 가장 가까운 순위(ceil(n × p)번째). 표본이 없으면 −1
        public void Summarize(float percentile)
        {
            samples = _frameMsList.Count;
            if (samples == 0)
            {
                return;
            }

            var sortedList = new List<float>(_frameMsList);
            sortedList.Sort();
            var sum = 0f;
            foreach (var frameMs in sortedList)
            {
                sum += frameMs;
            }

            p95Ms = sortedList[Mathf.Clamp(Mathf.CeilToInt(samples * percentile) - 1, 0, samples - 1)];
            meanMs = sum / samples;
            maxMs = sortedList[samples - 1];
        }
    }
}
#endif
