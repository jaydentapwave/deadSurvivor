using System;
using R3;
using VContainer.Unity;

// 정비 화면 부활(기획서 F절 V3): 정비가 열릴 때와 부활 뒤 쓰러진 대원 자리에만 부활 버튼을 보인다
public class RevivePresenter : IInitializable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly PartyModel _partyModel;
    private readonly CombatModel _combatModel;
    private readonly ReviveService _reviveService;
    private readonly UI_PrepView _prepView;
    private readonly CompositeDisposable _disposables = new();

    public RevivePresenter(RunModel runModel, PartyModel partyModel, CombatModel combatModel, ReviveService reviveService,
        UI_PrepView prepView)
    {
        _runModel = runModel;
        _partyModel = partyModel;
        _combatModel = combatModel;
        _reviveService = reviveService;
        _prepView = prepView;
    }

    public void Initialize()
    {
        var reviveViews = _prepView.ReviveViews;
        for (var i = 0; i < reviveViews.Count; i++)
        {
            var memberIndex = i;
            reviveViews[i].ReviveButton.OnClickAsObservable().Subscribe(_ => OnReviveClicked(memberIndex)).AddTo(_disposables);
        }

        _runModel.Phase.Where(phase => phase == RunPhase.Prep).Subscribe(_ => OnPrepEntered()).AddTo(_disposables);
        _combatModel.ReviveCount.Subscribe(_ => OnReviveCountChanged()).AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();

    public void OnReviveClicked(int memberIndex) => _reviveService.Revive(memberIndex);

    private void OnPrepEntered() => UpdateReviveViews();

    private void OnReviveCountChanged() => UpdateReviveViews();

    private void UpdateReviveViews()
    {
        var reviveViews = _prepView.ReviveViews;
        var memberList = _partyModel.MemberList;
        for (var i = 0; i < reviveViews.Count; i++)
        {
            reviveViews[i].UpdateUI(i < memberList.Count && !memberList[i].IsAlive);
        }
    }
}
