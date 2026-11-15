using UnityEngine;
using VContainer.Unity;

// 매 프레임 시간을 한 판 규칙에 넘긴다(배속·일시정지·고정 간격 쪼개기는 RunService.AdvanceFrame).
// 판이 끝나면 다른 시스템에 알리는 사실로 RunEndedEvent를 낸다(F1에는 받는 쪽 없음).
public class BattlePresenter : ITickable
{
    private readonly RunModel _runModel;
    private readonly RunService _runService;
    private readonly EventBus _eventBus;

    public BattlePresenter(RunModel runModel, RunService runService, EventBus eventBus)
    {
        _runModel = runModel;
        _runService = runService;
        _eventBus = eventBus;
    }

    public void Tick()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            return;
        }

        _runService.AdvanceFrame(Time.deltaTime);
        if (_runModel.Phase.CurrentValue == RunPhase.Result)
        {
            _eventBus.Publish(new GameEvent.RunEndedEvent { Outcome = _runModel.Outcome.CurrentValue });
        }
    }
}
