using System.Collections.Generic;

// 자동 장착 봇의 계획(기획서 E절, 판정용). 상태를 바꾸지 않고 복사본 위에서 합성 → 점수 최대 빈 칸 놓기를 정해 단계 목록으로 돌려준다.
// 계획은 부활이 끝난 상태를 전제로 한다: 봇은 GetReviveMemberList의 대원을 먼저 부활시킨 뒤 Plan을 부른다.
// 실행은 봇이 BagPresenter 처리 메서드로 한다(사람이 누르는 길과 같다)
public class AutoEquipPlanner
{
    private readonly BagModel _bagModel;
    private readonly BagService _bagService;
    private readonly BagPreviewService _bagPreviewService;
    private readonly PartyModel _partyModel;
    private readonly GradeRepository _gradeRepository;
    private readonly BagRulesData _bagRules;
    private readonly CombatRulesData _combatRules;

    public AutoEquipPlanner(BagModel bagModel, BagService bagService, BagPreviewService bagPreviewService, PartyModel partyModel,
        GradeRepository gradeRepository, BagRulesRepository bagRulesRepository, CombatRulesRepository combatRulesRepository)
    {
        _bagModel = bagModel;
        _bagService = bagService;
        _bagPreviewService = bagPreviewService;
        _partyModel = partyModel;
        _gradeRepository = gradeRepository;
        _bagRules = bagRulesRepository.Rules;
        _combatRules = combatRulesRepository.Rules;
    }

    // 1. 부활할 대원(쓰러진 대원, 번호 순)
    public List<int> GetReviveMemberList()
    {
        var memberList = new List<int>();
        for (var i = 0; i < _partyModel.MemberList.Count; i++)
        {
            if (!_partyModel.MemberList[i].IsAlive)
            {
                memberList.Add(i);
            }
        }

        return memberList;
    }

    // 2. 합성 → 3. 놓기. 꺼내기·바꾸기는 하지 않는다
    public List<BagPlanStep> Plan()
    {
        var stepList = new List<BagPlanStep>();
        using var copy = _bagModel.CreateCopy();
        PlanMerges(copy, stepList);
        PlanDrops(copy, stepList);
        return stepList;
    }

    // 원정대 점수 = 살아 있는 대원마다 [발동 아이템마다 피해 또는 회복량 ÷ 주기 합 + hpWeight × 최대 HP ÷ (1 − 유효 방어)
    // + shieldWeight × 보호막 상한 + 자기 회복 ÷ 회복 주기]의 합. 회복 주기 = healPeriod ÷ (1 + 가속)(F3 CombatService와 같음)
    public float CalculateScore(BagModel model)
    {
        var sheetList = _bagPreviewService.CalculateSheet(model);
        var score = 0f;
        for (var member = 0; member < sheetList.Count; member++)
        {
            if (!_partyModel.MemberList[member].IsAlive)
            {
                continue;
            }

            var sheet = sheetList[member];
            foreach (var action in sheet.ActionList)
            {
                score += action.Power / action.Period;
            }

            var stats = sheet.Stats;
            score += _bagRules.AutoEquip.HpWeight * stats.MaxHp / (1f - stats.Defense);
            score += _bagRules.AutoEquip.ShieldWeight * stats.Shield;
            score += stats.Heal / (_combatRules.HealPeriod / (1f + stats.Haste));
        }

        return score;
    }

    // 보관함 앞에서부터, 대상 = 살아 있는 대원 가방(대원 순, 목록 순) → 보관함의 다른 아이템(앞에서부터). 되면 처음부터 다시
    private void PlanMerges(BagModel copy, List<BagPlanStep> stepList)
    {
        var isMerged = true;
        while (isMerged)
        {
            isMerged = false;
            foreach (var source in copy.StashList)
            {
                var target = FindMergeTarget(copy, source);
                if (target == null)
                {
                    continue;
                }

                if (_bagService.TryMerge(copy, source.Uid, target.Uid, out var reason))
                {
                    stepList.Add(BagPlanStep.Merge(source.Uid, target.Uid));
                    isMerged = true;
                    break;
                }

                GameLogger.LogWarning($"[AutoEquipPlanner] Merge {source.Uid} → {target.Uid} failed: {reason}");
            }
        }
    }

    private BagItem FindMergeTarget(BagModel copy, BagItem source)
    {
        var lastGrade = _gradeRepository.LastGrade;
        for (var member = 0; member < copy.MemberCount && member < _partyModel.MemberList.Count; member++)
        {
            if (!_partyModel.MemberList[member].IsAlive)
            {
                continue;
            }

            foreach (var item in copy.GetBag(member))
            {
                if (BagRules.CanMerge(source, item, lastGrade))
                {
                    return item;
                }
            }
        }

        foreach (var item in copy.StashList)
        {
            if (BagRules.CanMerge(source, item, lastGrade))
            {
                return item;
            }
        }

        return null;
    }

    // 보관함 앞에서부터: 살아 있는 대원 × 회전 0~3 × y × x 중 빈 칸 놓기가 되는 곳마다 점수, 가장 높은 곳(같으면 먼저 찾은 곳)이
    // 지금 점수보다 크면 놓는다. 아니면 보관함에 남긴다
    private void PlanDrops(BagModel copy, List<BagPlanStep> stepList)
    {
        var currentScore = CalculateScore(copy);
        var index = 0;
        while (index < copy.StashList.Count)
        {
            var uid = copy.StashList[index].Uid;
            var hasBest = false;
            var bestScore = float.MinValue;
            var best = default(BagPlanStep);
            for (var member = 0; member < copy.MemberCount && member < _partyModel.MemberList.Count; member++)
            {
                if (!_partyModel.MemberList[member].IsAlive)
                {
                    continue;
                }

                for (var rotation = 0; rotation < 4; rotation++)
                {
                    for (var y = 0; y < _bagRules.BagHeight; y++)
                    {
                        for (var x = 0; x < _bagRules.BagWidth; x++)
                        {
                            if (!_bagService.CanPlace(copy, uid, member, x, y, rotation))
                            {
                                continue;
                            }

                            using var trial = copy.CreateCopy();
                            _bagService.TryDrop(trial, uid, member, x, y, rotation, out _);
                            var score = CalculateScore(trial);
                            if (!hasBest || score > bestScore)
                            {
                                hasBest = true;
                                bestScore = score;
                                best = BagPlanStep.Drop(uid, member, x, y, rotation);
                            }
                        }
                    }
                }
            }

            if (hasBest && bestScore > currentScore)
            {
                _bagService.TryDrop(copy, uid, best.Member, best.X, best.Y, best.Rotation, out _);
                stepList.Add(best);
                currentScore = bestScore;
                continue;
            }

            index++;
        }
    }
}

public enum BagPlanStepKind
{
    Merge,
    Drop,
}

// 자동 장착 계획의 한 단계. Merge: Uid → TargetUid. Drop: Uid를 Member 가방 (X, Y)에 Rotation으로
public readonly struct BagPlanStep
{
    public BagPlanStepKind Kind { get; }
    public int Uid { get; }
    public int TargetUid { get; }
    public int Member { get; }
    public int X { get; }
    public int Y { get; }
    public int Rotation { get; }

    private BagPlanStep(BagPlanStepKind kind, int uid, int targetUid, int member, int x, int y, int rotation)
    {
        Kind = kind;
        Uid = uid;
        TargetUid = targetUid;
        Member = member;
        X = x;
        Y = y;
        Rotation = rotation;
    }

    public static BagPlanStep Merge(int uid, int targetUid) => new(BagPlanStepKind.Merge, uid, targetUid, -1, 0, 0, 0);

    public static BagPlanStep Drop(int uid, int member, int x, int y, int rotation) =>
        new(BagPlanStepKind.Drop, uid, -1, member, x, y, rotation);
}
