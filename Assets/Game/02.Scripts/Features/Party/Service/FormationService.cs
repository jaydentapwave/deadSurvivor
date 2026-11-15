using UnityEngine;

// 진형 고르기와 적용(F2 기획서 B절). FormationModel을 쓰는 유일한 곳.
// 한 판 시작은 order가 가장 작은 진형, 정비에서만 대기 진형을 바꾸고, 다음 웨이브 시작에 지금 진형 = 대기 진형.
public class FormationService
{
    private readonly FormationModel _formationModel;
    private readonly FormationRepository _formationRepository;
    private readonly RunModel _runModel;

    private FormationData _currentFormation;

    public FormationService(FormationModel formationModel, FormationRepository formationRepository, RunModel runModel)
    {
        _formationModel = formationModel;
        _formationRepository = formationRepository;
        _runModel = runModel;
    }

    public void ResetForRun()
    {
        var first = _formationRepository.FormationDataList[0];
        _currentFormation = first;
        _formationModel.SetPendingId(first.Id);
        _formationModel.SetCurrentId(first.Id);
    }

    // 정비가 아니거나 없는 id면 경고 후 무시
    public void SelectPending(string id)
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Prep)
        {
            GameLogger.LogWarning($"[FormationService] SelectPending {id} in {_runModel.Phase.CurrentValue}");
            return;
        }

        if (!_formationRepository.TryGet(id, out _))
        {
            GameLogger.LogWarning($"[FormationService] No formation {id}");
            return;
        }

        _formationModel.SetPendingId(id);
    }

    // 웨이브 시작에 부른다
    public void ApplyPending()
    {
        var pendingId = _formationModel.PendingId.CurrentValue;
        _formationRepository.TryGet(pendingId, out _currentFormation);
        _formationModel.SetCurrentId(pendingId);
    }

    // 지금 진형의 index번째 자리(캐릭터 order 순, 4명 기준 고정 — 쓰러진 대원이 있어도 바뀌지 않는다)
    public Vector2 GetSlot(int index) => _currentFormation.Slots[index];
}
