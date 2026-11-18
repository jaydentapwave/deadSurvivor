using System.Collections.Generic;
using VContainer.Unity;

// 적 투사체 모델 → 그림(기획서 클라 변경). 날아가는 투사체마다 그림 하나, 사라진 투사체의 그림은 풀로.
public class EnemyShotPresenter : ITickable
{
    private readonly MonsterModel _monsterModel;
    private readonly RunRulesData _runRules;
    private readonly ViewRulesData _viewRules;
    private readonly EnemyShotViewFactory _enemyShotViewFactory;
    private readonly EnemyShotViewRegistry _enemyShotViewRegistry;

    private readonly HashSet<int> _aliveShotIdSet = new();
    private readonly List<int> _releaseIdList = new();

    public EnemyShotPresenter(MonsterModel monsterModel, RunRulesData runRules, EnemyShotViewFactory enemyShotViewFactory,
        EnemyShotViewRegistry enemyShotViewRegistry, ViewRulesData viewRules)
    {
        _monsterModel = monsterModel;
        _runRules = runRules;
        _viewRules = viewRules;
        _enemyShotViewFactory = enemyShotViewFactory;
        _enemyShotViewRegistry = enemyShotViewRegistry;
    }

    public void Tick()
    {
        _aliveShotIdSet.Clear();
        foreach (var shot in _monsterModel.EnemyShotList)
        {
            if (!shot.IsAlive)
            {
                continue;
            }

            _aliveShotIdSet.Add(shot.Id);
            if (!_enemyShotViewRegistry.TryGet(shot.Id, out var view))
            {
                view = _enemyShotViewFactory.Create(shot);
            }

            view.UpdateView(IsoProjection.FloorToWorldSnapped(shot.Position, _runRules.UnitScale, _viewRules.SinTilt), shot.Travel / shot.Speed);
        }

        _releaseIdList.Clear();
        foreach (var id in _enemyShotViewRegistry.ShotIds)
        {
            if (!_aliveShotIdSet.Contains(id))
            {
                _releaseIdList.Add(id);
            }
        }

        foreach (var id in _releaseIdList)
        {
            _enemyShotViewFactory.Release(id);
        }
    }
}
