#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
        // 조이스틱 입력과 같은 이동 명령으로 들판 가운데를 원으로 돈다(F2)
        Move = 1,
        // 보관함 전리품 자동 장착(F5가 채운다)
        Equip = 2,
        // 정비 화면이면 다음 웨이브를 누른다
        AutoNext = 4,
    }

    // 판정 진입점이 조이스틱 포인터 처리에 쓰는 포인터 번호
    private const int ProbePointerId = 0;

    // MarkPositions가 기억한 원정대 중심·대원 좌표·생존(F2 H절). 편집기 판정 전용 상태
    private static Vector2 _markCenter;
    private static readonly List<Vector2> _markPositionList = new();
    private static readonly List<bool> _markAliveList = new();

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

    // 진형 id(F2)
    public static string Formation => Resolve<FormationModel>().CurrentId.CurrentValue;
    public static string PendingFormation => Resolve<FormationModel>().PendingId.CurrentValue;
    // 원정대 중심(바닥 좌표)
    public static float PartyX => Resolve<PartyModel>().Center.x;
    public static float PartyY => Resolve<PartyModel>().Center.y;
    // MarkPositions 뒤 중심 변화
    public static float PartyDeltaX => PartyX - _markCenter.x;
    public static float PartyDeltaY => PartyY - _markCenter.y;
    public static float PartyMovedSinceMark => (Resolve<PartyModel>().Center - _markCenter).magnitude;
    public static bool IsJoystickShown => Resolve<UI_JoystickView>().IsShown;

    // 살아 있는 대원과 (중심 + 지금 진형 자리)의 최대 거리
    public static float MaxSlotError
    {
        get
        {
            var partyModel = Resolve<PartyModel>();
            var formationService = Resolve<FormationService>();
            var memberList = partyModel.MemberList;
            var maxError = 0f;
            for (var i = 0; i < memberList.Count; i++)
            {
                if (memberList[i].IsAlive)
                {
                    var slotPosition = partyModel.Center + formationService.GetSlot(i);
                    maxError = Mathf.Max(maxError, Vector2.Distance(memberList[i].Position, slotPosition));
                }
            }

            return maxError;
        }
    }

    // 표시 때도 지금도 쓰러진 대원이 표시 뒤 움직인 최대 거리. 없으면 0
    public static float MaxDeadMoved
    {
        get
        {
            var memberList = Resolve<PartyModel>().MemberList;
            var maxMoved = 0f;
            for (var i = 0; i < memberList.Count && i < _markPositionList.Count; i++)
            {
                if (!_markAliveList[i] && !memberList[i].IsAlive)
                {
                    maxMoved = Mathf.Max(maxMoved, Vector2.Distance(memberList[i].Position, _markPositionList[i]));
                }
            }

            return maxMoved;
        }
    }

    // 카메라 중심과 IsoProjection(원정대 중심)의 월드 거리
    public static float CameraError
    {
        get
        {
            var expected = IsoProjection.FloorToWorld(Resolve<PartyModel>().Center, Resolve<RunRulesData>().UnitScale);
            return Vector2.Distance(Resolve<Camera>().transform.position, expected);
        }
    }

    public static void PressDepart() => Resolve<LobbyPresenter>().OnDepartClicked();

    public static void PressNextWave() => Resolve<PrepPresenter>().OnNextWaveClicked();

    public static void PressToLobby() => Resolve<ResultPresenter>().OnToLobbyClicked();

    // 다음 판 설정(게임의 시작 입력). 로비 출발이 이 값을 쓴다
    public static void UseRunSettings(int seed, float enemyCountScale) => Resolve<RunSettingsModel>().Set(seed, enemyCountScale);

    // 입력 Presenter와 같은 이동 명령(화면 기준 x 오른쪽, y 위)
    public static void SetMove(float x, float y) => Resolve<PartyMoveService>().SetMoveInput(new Vector2(x, y));

    // 조이스틱 포인터(화면 0~1 비율, 왼쪽 아래 0,0). EventSystem처럼 UI_JoystickView의 포인터 처리 메서드를 부른다
    public static void JoystickDown(float x, float y) => Resolve<UI_JoystickView>().OnPointerDown(CreatePointer(x, y));

    public static void JoystickDrag(float x, float y) => Resolve<UI_JoystickView>().OnDrag(CreatePointer(x, y));

    public static void JoystickUp()
    {
        var view = Resolve<UI_JoystickView>();
        view.OnPointerUp(CreatePointer(view.CurrentPosition.x / UnityEngine.Screen.width,
            view.CurrentPosition.y / UnityEngine.Screen.height));
    }

    public static void PressFormation(string id) => Resolve<FormationPresenter>().OnFormationClicked(id);

    // 지금 중심·대원 좌표·생존을 기억한다(PartyDelta*, PartyMovedSinceMark, MaxDeadMoved의 기준)
    public static void MarkPositions()
    {
        _markCenter = Resolve<PartyModel>().Center;
        _markPositionList.Clear();
        _markAliveList.Clear();
        foreach (var member in Resolve<PartyModel>().MemberList)
        {
            _markPositionList.Add(member.Position);
            _markAliveList.Add(member.IsAlive);
        }
    }

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
            // Move가 빠지면 0을 한 번 보낸다. 같은 식에서 바로 StartBot을 부르면 새 봇을 만들도록 즉시 지운다
            bot.SetMode(BotMode.None);
            Object.DestroyImmediate(bot.gameObject);
        }
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

    private static PointerEventData CreatePointer(float x, float y)
    {
        return new PointerEventData(EventSystem.current)
        {
            pointerId = ProbePointerId,
            position = new Vector2(x * UnityEngine.Screen.width, y * UnityEngine.Screen.height),
        };
    }
}

// 판정용 자동 플레이. Move(F2)·AutoNext를 한다. 없는 동작(Equip)이 들어오면 경고를 남기고 무시한다.
public class PlayableBot : MonoBehaviour
{
    private const PlayableProbe.BotMode SupportedModes = PlayableProbe.BotMode.Move | PlayableProbe.BotMode.AutoNext;
    // 들판 가운데(원점, FieldView.Build)를 도는 원의 반지름(바닥 단위, F2 H절)
    private const float MoveCircleRadius = 600f;
    // 가운데에서 이 거리 미만이면 오른쪽으로 간다
    private const float MoveCenterDeadZone = 1f;
    // 바닥 방향 → 화면 방향(아이소 바닥은 세로가 절반으로 그려진다)
    private const float FloorToScreenVertical = 0.5f;

    private PlayableProbe.BotMode _mode;

    public void SetMode(PlayableProbe.BotMode mode)
    {
        var unsupported = mode & ~SupportedModes;
        if (unsupported != PlayableProbe.BotMode.None)
        {
            GameLogger.LogWarning($"[PlayableBot] Not implemented yet, ignored: {unsupported}");
        }

        var isMoveDropped = (_mode & PlayableProbe.BotMode.Move) != 0 && (mode & PlayableProbe.BotMode.Move) == 0;
        _mode = mode & SupportedModes;
        if (isMoveDropped)
        {
            PlayableProbe.SetMove(0f, 0f);
        }
    }

    private void Update()
    {
        var screen = PlayableProbe.Screen;
        if ((_mode & PlayableProbe.BotMode.Move) != 0 && screen == nameof(RunPhase.Battle))
        {
            SendCircleMove();
        }

        if ((_mode & PlayableProbe.BotMode.AutoNext) != 0 && screen == nameof(RunPhase.Prep))
        {
            PlayableProbe.PressNextWave();
        }
    }

    // 시계 방향 접선에 원 쪽으로 돌아가는 성분을 섞은 바닥 방향을 화면 방향으로 바꿔 세기 1로 넣는다
    private static void SendCircleMove()
    {
        var center = new Vector2(PlayableProbe.PartyX, PlayableProbe.PartyY);
        var distance = center.magnitude;
        var floorDirection = Vector2.right;
        if (distance >= MoveCenterDeadZone)
        {
            var outward = center / distance;
            var clockwise = new Vector2(outward.y, -outward.x);
            var correction = Mathf.Clamp((distance - MoveCircleRadius) / MoveCircleRadius, -1f, 1f);
            floorDirection = (clockwise - outward * correction).normalized;
        }

        var screenDirection = new Vector2(floorDirection.x, floorDirection.y * FloorToScreenVertical).normalized;
        PlayableProbe.SetMove(screenDirection.x, screenDirection.y);
    }
}
#endif
