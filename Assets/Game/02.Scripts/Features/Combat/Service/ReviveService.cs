// 정비 화면의 무료 부활(기획서 E절). 조건만 보고 상태 초기화는 CombatService.ReviveMember가 한다
public class ReviveService
{
    private readonly RunModel _runModel;
    private readonly PartyModel _partyModel;
    private readonly CombatService _combatService;

    public ReviveService(RunModel runModel, PartyModel partyModel, CombatService combatService)
    {
        _runModel = runModel;
        _partyModel = partyModel;
        _combatService = combatService;
    }

    // 정비 화면이고 그 대원이 쓰러져 있다
    public bool CanRevive(int memberIndex)
    {
        var memberList = _partyModel.MemberList;
        return _runModel.Phase.CurrentValue == RunPhase.Prep && memberIndex >= 0 && memberIndex < memberList.Count
            && !memberList[memberIndex].IsAlive;
    }

    // 비용·횟수 제한 없음. 안 되면 경고를 남기고 상태를 바꾸지 않는다
    public bool Revive(int memberIndex)
    {
        if (!CanRevive(memberIndex))
        {
            GameLogger.LogWarning($"[ReviveService] Cannot revive member {memberIndex} in {_runModel.Phase.CurrentValue}");
            return false;
        }

        _combatService.ReviveMember(memberIndex);
        return true;
    }
}
