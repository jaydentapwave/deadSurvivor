using System;
using System.Collections.Generic;
using System.Linq;

// 전리품 지급(기획서 B절, 예전 DSGame.Run FinishWave·RewardWeights·Weighted). 난수는 생성·전투와 따로 돈다(B2).
// 보관함에 넣는 일은 BagService.AddLoot가 한다
public class LootService
{
    private readonly BagModel _bagModel;
    private readonly BagService _bagService;
    private readonly PartyModel _partyModel;
    private readonly ItemRepository _itemRepository;
    private readonly LootRulesData _rules;
    private readonly List<string> _grantedIdList = new();

    private Random _random;

    public LootService(BagModel bagModel, BagService bagService, PartyModel partyModel, ItemRepository itemRepository,
        LootRulesRepository lootRulesRepository)
    {
        _bagModel = bagModel;
        _bagService = bagService;
        _partyModel = partyModel;
        _itemRepository = itemRepository;
        _rules = lootRulesRepository.Rules;
    }

    // 후보: lootWeight > 0인 모든 아이템, id 문자열 순(ordinal). 개수는 파일 수에서 온다
    public IReadOnlyList<ItemData> CandidateList =>
        _itemRepository.ItemDataList.Where(item => item.LootWeight > 0f).OrderBy(item => item.Id, StringComparer.Ordinal).ToList();

    // runSeed = 이번 판 실제 시드(F1 A11, 시드 0이면 RunService가 시각에서 뽑은 값)
    public void ResetRun(int runSeed) => _random = new Random(unchecked(runSeed + _rules.LootSeedOffset));

    // 마지막이 아닌 웨이브가 끝나 정비로 갈 때(B1): 가중치 표를 한 번 만들고 3~5개를 뽑아 보관함 끝에
    public void GrantWaveLoot(int completedWave)
    {
        var candidateList = CandidateList;
        if (candidateList.Count == 0)
        {
            GameLogger.LogWarning("[LootService] No loot candidates (all lootWeight 0)");
            return;
        }

        var weightList = candidateList.Select(item => GetWeight(item, completedWave)).ToList();
        var count = _random.Next(_rules.MinCount, _rules.MaxCount + 1);
        _grantedIdList.Clear();
        for (var i = 0; i < count; i++)
        {
            _grantedIdList.Add(candidateList[PickIndex(weightList, _random.NextDouble())].Id);
        }

        _bagService.AddLoot(_grantedIdList);
    }

    // 가중치 = lootWeight × 배율(B3 2). 살아 있는 대원만 본다
    public float GetWeight(ItemData item, int completedWave)
    {
        if (completedWave <= _rules.EarlyWaveMax && item.BonusEffect != ItemEffectKind.None && IsEarlyBonusTarget(item))
        {
            return item.LootWeight * _rules.EarlyBonusFactor;
        }

        if (completedWave >= _rules.MergeWaveMin && HasAliveCommonInBag(item.Id))
        {
            return item.LootWeight * _rules.MergeFactor;
        }

        return item.LootWeight;
    }

    // r = r01 × 가중치 합, 후보 순서대로 r −= 가중치, r < 0이 되는 후보(끝까지 가면 마지막)
    public int PickIndex(IReadOnlyList<float> weightList, double r01)
    {
        var total = 0d;
        foreach (var weight in weightList)
        {
            total += weight;
        }

        var r = r01 * total;
        for (var i = 0; i < weightList.Count; i++)
        {
            r -= weightList[i];
            if (r < 0d)
            {
                return i;
            }
        }

        return weightList.Count - 1;
    }

    // 조건 없는 추가 효과이거나, 살아 있는 대원 누군가의 장착한 발동 아이템이 bonusTarget에 맞는다
    private bool IsEarlyBonusTarget(ItemData item)
    {
        if (item.BonusTarget == ItemBonusTarget.Any && !item.BonusNeedsAdjacent)
        {
            return true;
        }

        foreach (var member in _partyModel.MemberList)
        {
            if (!member.IsAlive)
            {
                continue;
            }

            foreach (var itemId in member.EquippedItemIdList)
            {
                var mode = _itemRepository.Get(itemId).Mode;
                if (mode != ItemMode.Passive && IsTargetMatched(item.BonusTarget, mode))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasAliveCommonInBag(string itemId)
    {
        var memberList = _partyModel.MemberList;
        for (var member = 0; member < memberList.Count && member < _bagModel.MemberCount; member++)
        {
            if (!memberList[member].IsAlive)
            {
                continue;
            }

            foreach (var bagItem in _bagModel.GetBag(member))
            {
                if (bagItem.ItemId == itemId && bagItem.Grade == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // F3 EquipmentStatService의 대상 판정(B3)과 같은 규칙: 빔 = 아무 발동, attack = 회복 두 방식 뺀 발동, 그 밖 = 같은 방식.
    // F3 파일을 기획서 D절 밖으로 고치지 않으려고 여기 따로 둔다(합친 뒤 하나로 합칠 후보)
    private static bool IsTargetMatched(ItemBonusTarget target, ItemMode mode)
    {
        switch (target)
        {
            case ItemBonusTarget.Any:
                return true;
            case ItemBonusTarget.Attack:
                return mode != ItemMode.Heal && mode != ItemMode.HealArea && mode != ItemMode.Passive;
            case ItemBonusTarget.Melee:
                return mode == ItemMode.Melee;
            case ItemBonusTarget.Sweep:
                return mode == ItemMode.Sweep;
            case ItemBonusTarget.Projectile:
                return mode == ItemMode.Projectile;
            case ItemBonusTarget.Explosion:
                return mode == ItemMode.Explosion;
            case ItemBonusTarget.Chain:
                return mode == ItemMode.Chain;
            case ItemBonusTarget.Heal:
                return mode == ItemMode.Heal;
            case ItemBonusTarget.HealArea:
                return mode == ItemMode.HealArea;
            default:
                GameLogger.LogError($"[LootService] Unknown bonus target {target}");
                return false;
        }
    }
}
