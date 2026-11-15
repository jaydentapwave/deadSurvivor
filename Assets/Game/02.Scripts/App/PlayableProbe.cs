#if UNITY_EDITOR
using System;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

// 플레이 가능 판정용 진입점(기획서 H절, .ai/rules/playable-check.md). 판정 경로(tools/playable/*.json)의 식이 부른다.
// 장면 LifetimeScope 컨테이너에서 모델·Presenter를 꺼내 읽고, 버튼은 Presenter의 버튼 처리 메서드(onClick과 같은 길)를 부른다.
// 승패·단계를 써 넣지 않는다. 게임 코드는 이 클래스를 모른다.
public static class PlayableProbe
{
    [Flags]
    public enum BotMode
    {
        None = 0,
        // 조이스틱 입력과 같은 이동 명령(F2가 채운다)
        Move = 1,
        // 보관함 전리품 자동 장착(F5가 채운다)
        Equip = 2,
        // 정비 화면이면 다음 웨이브를 누른다
        AutoNext = 4,
    }

    // 장면이 아직 없으면 "None"
    public static string Screen
    {
        get
        {
            var scope = Object.FindAnyObjectByType<GameSceneLifetimeScope>();
            return scope == null || scope.Container == null ? "None" : scope.Container.Resolve<RunModel>().Phase.CurrentValue.ToString();
        }
    }

    public static int CardCount => Resolve<LobbyPresenter>().CardCount;
    public static bool CanDepart => Resolve<LobbyPresenter>().CanDepart;
    public static int Wave => Resolve<RunModel>().Wave.CurrentValue;
    public static int CompletedWave => Resolve<RunModel>().CompletedWave.CurrentValue;
    public static float WaveSeconds => Resolve<RunModel>().WaveSeconds.CurrentValue;
    public static float RemainingSeconds => Resolve<RunModel>().RemainingSeconds.CurrentValue;
    public static bool IsBurningTime => Resolve<RunModel>().IsBurningTime.CurrentValue;
    public static int AliveCount => Resolve<PartyService>().AliveCount;
    public static int Kills => Resolve<RunModel>().Kills.CurrentValue;
    public static int EnemyCount => Resolve<MonsterService>().AliveCount;
    public static string Outcome => Resolve<RunModel>().Outcome.CurrentValue.ToString();
    public static int WavesSurvived => Resolve<RunModel>().WavesSurvived.CurrentValue;
    public static float TotalBattleSeconds => Resolve<RunModel>().TotalBattleSeconds.CurrentValue;
    // Play 뒤 시작한 판 수
    public static int RunCount => Resolve<RunModel>().RunCount.CurrentValue;

    // 원정대가 데이터의 캐릭터 수만큼 있고 각자 장착 목록이 시작 아이템 하나뿐인가
    public static bool HasOnlyStartItems
    {
        get
        {
            var characterRepository = Resolve<CharacterRepository>();
            var memberList = Resolve<PartyModel>().MemberList;
            if (memberList.Count != characterRepository.CharacterDataList.Count)
            {
                return false;
            }

            foreach (var member in memberList)
            {
                var items = member.EquippedItemIdList;
                if (items.Count != 1 || items[0] != characterRepository.Get(member.CharacterId).StartItem)
                {
                    return false;
                }
            }

            return true;
        }
    }

    // F4 — 몬스터 5종·웨이브 목표값(F4 기획서 E절). 기록은 PlayableMetricsRecorder가 맡는다
    // 지금 살아 있는 몬스터의 행동 종류 수
    public static int AliveBehaviorKinds
    {
        get
        {
            var kindSet = new System.Collections.Generic.HashSet<MonsterBehavior>();
            foreach (var monster in Resolve<MonsterModel>().MonsterList)
            {
                if (monster.IsAlive)
                {
                    kindSet.Add(monster.Behavior);
                }
            }

            return kindSet.Count;
        }
    }

    // 지금 날아가는 적 투사체 수
    public static int EnemyShotCount
    {
        get
        {
            var count = 0;
            foreach (var shot in Resolve<MonsterModel>().EnemyShotList)
            {
                if (shot.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public static bool IsSpawnCountAsData => Recorder != null && Recorder.IsSpawnCountAsData;
    public static float BurningFrameMsP95 => Recorder != null ? Recorder.BurningFrameMsP95 : -1f;
    public static int MaxAliveEnemies => Recorder != null ? Recorder.MaxAliveEnemies : 0;

    // 기록기를 만들고 이후 모든 판을 기록한다. 웨이브 끝·판 끝마다 path에 파일 전체를 다시 쓴다
    public static void StartWaveMetrics(string path) => GetOrCreateRecorder().StartRecording(path);

    // 다음에 그 웨이브 전투가 시작되면 Time.timeScale = 1, Application.runInBackground = true(한 번만)
    public static void UseRealTimeAtWave(int wave) => GetOrCreateRecorder().UseRealTimeAtWave(wave);

    // 마지막 기록 판의 그 웨이브 정규 생성 수(기록 없으면 −1)
    public static int SpawnedCountOf(int wave) => Recorder != null ? Recorder.SpawnedCountOf(wave) : -1;

    // 마지막 기록 판의 모든 웨이브가 끝까지 갔고 각 생성 수가 first → last 직선 목표 ± tolerance 안(M2)
    public static bool IsSpawnCountNear(int first, int last, float tolerance) =>
        Recorder != null && Recorder.IsSpawnCountNear(first, last, tolerance);

    // 웨이브 1부터 기록한 마지막 판에서 그 행동(contact·projectile·charge·armored·bomb)이 처음 나온 웨이브, 없으면 0(M3)
    public static int FirstWaveOf(string behavior) =>
        Recorder != null ? Recorder.FirstWaveOf(behavior) : 0;

    public static void PressDepart() => Resolve<LobbyPresenter>().OnDepartClicked();

    public static void PressNextWave() => Resolve<PrepPresenter>().OnNextWaveClicked();

    public static void PressToLobby() => Resolve<ResultPresenter>().OnToLobbyClicked();

    // 다음 판 설정(게임의 시작 입력). 로비 출발이 이 값을 쓴다
    public static void UseRunSettings(int seed, float enemyCountScale) => Resolve<RunSettingsModel>().Set(seed, enemyCountScale);

    public static void StartBot(BotMode mode)
    {
        var bot = Object.FindAnyObjectByType<PlayableBot>();
        if (bot == null)
        {
            bot = new GameObject(nameof(PlayableBot)).AddComponent<PlayableBot>();
        }

        bot.SetMode(mode);
    }

    public static void StopBot()
    {
        var bot = Object.FindAnyObjectByType<PlayableBot>();
        if (bot != null)
        {
            Object.Destroy(bot.gameObject);
        }
    }

    private static PlayableMetricsRecorder Recorder => Object.FindAnyObjectByType<PlayableMetricsRecorder>();

    private static PlayableMetricsRecorder GetOrCreateRecorder()
    {
        var recorder = Recorder;
        return recorder != null ? recorder : new GameObject(nameof(PlayableMetricsRecorder)).AddComponent<PlayableMetricsRecorder>();
    }

    internal static T Resolve<T>()
    {
        var scope = Object.FindAnyObjectByType<GameSceneLifetimeScope>();
        if (scope == null || scope.Container == null)
        {
            throw new InvalidOperationException("[PlayableProbe] GameSceneLifetimeScope not found (Play Main.unity first)");
        }

        return scope.Container.Resolve<T>();
    }
}

// 판정용 자동 플레이. F1은 AutoNext만 한다. 없는 동작(Move·Equip)이 들어오면 경고를 남기고 무시한다.
public class PlayableBot : MonoBehaviour
{
    private const PlayableProbe.BotMode SupportedModes = PlayableProbe.BotMode.AutoNext;

    public PlayableProbe.BotMode Mode => _mode;

    private PlayableProbe.BotMode _mode;

    public void SetMode(PlayableProbe.BotMode mode)
    {
        var unsupported = mode & ~SupportedModes;
        if (unsupported != PlayableProbe.BotMode.None)
        {
            GameLogger.LogWarning($"[PlayableBot] Not implemented yet, ignored: {unsupported}");
        }

        _mode = mode & SupportedModes;
    }

    private void Update()
    {
        if ((_mode & PlayableProbe.BotMode.AutoNext) == 0)
        {
            return;
        }

        if (PlayableProbe.Screen == nameof(RunPhase.Prep))
        {
            PlayableProbe.PressNextWave();
        }
    }
}
#endif
