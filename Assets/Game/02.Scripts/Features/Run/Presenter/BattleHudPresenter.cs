using System;
using R3;
using VContainer.Unity;

// 전투 화면 표시줄(V2): 상단 값과 하단 캐릭터별 칸을 매 프레임 갱신하고, 배속·일시정지 버튼을 규칙에 넘긴다
public class BattleHudPresenter : IInitializable, ILateTickable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly RunService _runService;
    private readonly RunRulesData _runRules;
    private readonly WaveRepository _waveRepository;
    private readonly PartyModel _partyModel;
    private readonly MonsterService _monsterService;
    private readonly CharacterRepository _characterRepository;
    private readonly SpriteAtlasService _spriteAtlasService;
    private readonly UI_BattleHudView _battleHudView;
    private readonly CompositeDisposable _disposables = new();

    public BattleHudPresenter(RunModel runModel, RunService runService, RunRulesData runRules, WaveRepository waveRepository,
        PartyModel partyModel, MonsterService monsterService, CharacterRepository characterRepository,
        SpriteAtlasService spriteAtlasService, UI_BattleHudView battleHudView)
    {
        _runModel = runModel;
        _runService = runService;
        _runRules = runRules;
        _waveRepository = waveRepository;
        _partyModel = partyModel;
        _monsterService = monsterService;
        _characterRepository = characterRepository;
        _spriteAtlasService = spriteAtlasService;
        _battleHudView = battleHudView;
    }

    public void Initialize()
    {
        if (_characterRepository.CharacterDataList.Count > _battleHudView.MemberStatusViews.Count)
        {
            GameLogger.LogError($"[BattleHudPresenter] characters {_characterRepository.CharacterDataList.Count} > status views {_battleHudView.MemberStatusViews.Count}");
        }

        _battleHudView.SpeedButton.OnClickAsObservable().Subscribe(_ => OnSpeedClicked()).AddTo(_disposables);
        _battleHudView.PauseButton.OnClickAsObservable().Subscribe(_ => OnPauseClicked()).AddTo(_disposables);
    }

    public void LateTick()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            return;
        }

        _battleHudView.UpdateUI(_runModel.Wave.CurrentValue, _waveRepository.WaveDataList.Count, _runModel.Kills.CurrentValue,
            _monsterService.AliveCount, _runModel.RemainingSeconds.CurrentValue, _runModel.IsBurningTime.CurrentValue,
            _runRules.SpeedOptions[_runModel.SpeedIndex.CurrentValue], _runModel.IsPaused.CurrentValue);
        UpdateMemberStatusViews();
    }

    public void Dispose() => _disposables.Dispose();

    public void OnSpeedClicked() => _runService.ToggleSpeed();

    public void OnPauseClicked() => _runService.TogglePause();

    private void UpdateMemberStatusViews()
    {
        var memberList = _partyModel.MemberList;
        var statusViews = _battleHudView.MemberStatusViews;
        for (var i = 0; i < statusViews.Count; i++)
        {
            var hasMember = i < memberList.Count;
            statusViews[i].SetVisible(hasMember);
            if (!hasMember)
            {
                continue;
            }

            var member = memberList[i];
            var data = _characterRepository.Get(member.CharacterId);
            var portraitSprite = string.IsNullOrEmpty(data.Sprite) ? null
                : _spriteAtlasService.Get(data.Sprite + SpriteAtlasService.FrontSuffix);
            statusViews[i].UpdateUI(data.Name, member.Hp, member.MaxHp, member.IsAlive, portraitSprite);
        }
    }
}
