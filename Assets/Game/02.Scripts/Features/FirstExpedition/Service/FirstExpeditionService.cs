using System.Collections.Generic;
using UnityEngine;

// 첫 원정의 조우만 관리한다. 이동·전투·장비는 일반 판과 같은 서비스를 쓴다.
public sealed class FirstExpeditionService
{
    private const string CompletionKey = "PackExpedition.FirstExpeditionCompleted";
    private const float ApproachDistance = 142f;
    private static readonly string[] StartingParty = { "bruno", "bolt" };
    private static readonly string[] FirstLoot = { "whetstone", "scripture", "leather" };
    private static readonly string[] SecondLoot = { "whetstone", "quiver", "helmet" };
    private static readonly string[] ThirdLoot = { "crystal", "robe", "feather" };

    private readonly PartyModel _party;
    private readonly PartyService _partyService;
    private readonly PartyMoveService _movement;
    private readonly MonsterSpawnService _spawns;
    private readonly BagService _bags;
    private readonly CombatService _combat;
    private readonly ItemRepository _items;
    private readonly List<Monster> _guards = new(3);
    private bool _hasApproached;
    private bool _isFirstExpedition;

    public bool IsPending => !PlayerPrefs.HasKey(CompletionKey);
    public bool HasEncounter => _guards.Count > 0;
    public Vector2 EncounterPosition { get; private set; }
    public Vector2 EncounterStart { get; private set; }
    public string WaitingCharacterId { get; private set; }
    public int EncounterSteps { get; private set; }

    public FirstExpeditionService(PartyModel party, PartyService partyService, PartyMoveService movement,
        MonsterSpawnService spawns, BagService bags, CombatService combat, ItemRepository items)
    {
        _party = party;
        _partyService = partyService;
        _movement = movement;
        _spawns = spawns;
        _bags = bags;
        _combat = combat;
        _items = items;
    }

    public void CreateParty(bool isFirstExpedition)
    {
        ClearEncounter();
        _isFirstExpedition = isFirstExpedition;
        _partyService.CreateParty(isFirstExpedition ? StartingParty : null);
    }

    public float ScaleSpawns(int wave) => _isFirstExpedition ? wave switch
    {
        1 => 0.16f,
        2 => 0.35f,
        3 => 0.65f,
        _ => 1f,
    } : 1f;

    public void BeginEncounter(int wave)
    {
        ClearEncounter();
        if (!_isFirstExpedition || wave > 3)
        {
            return;
        }

        WaitingCharacterId = wave == 1 ? null : wave == 2 ? "lena" : "mira";
        EncounterStart = _party.Center;
        // Pick the nearer side of the field centre so a party at the edge can still reach the encounter.
        var direction = Mathf.Abs(EncounterStart.y) > Mathf.Abs(EncounterStart.x)
            ? new Vector2(0f, EncounterStart.y > 0f ? -1f : 1f)
            : new Vector2(EncounterStart.x > 0f ? -1f : 1f, 0f);
        EncounterPosition = _movement.ClampCenter(EncounterStart + direction * 240f);

        var guardCount = wave == 1 ? 1 : 3;
        for (var i = 0; i < guardCount; i++)
        {
            var angle = i * (Mathf.PI * 2f / guardCount);
            var offset = wave == 1 ? Vector2.zero : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 62f;
            _guards.Add(_spawns.SpawnEncounterGuard(EncounterPosition + offset));
        }
    }

    // 시계가 기다리는 동안에도 실제 이동과 전투가 진행된다.
    public void BeforeEncounterCombat()
    {
        EncounterSteps++;
        if (_hasApproached || (_party.Center - EncounterPosition).sqrMagnitude > ApproachDistance * ApproachDistance)
        {
            return;
        }

        _hasApproached = true;
        foreach (var guard in _guards)
        {
            guard.IsWaitingForEncounter = false;
        }
    }

    public void AfterEncounterCombat()
    {
        if (!_hasApproached)
        {
            return;
        }

        foreach (var guard in _guards)
        {
            if (guard.IsAlive)
            {
                return;
            }
        }

        if (WaitingCharacterId != null)
        {
            _partyService.AddMember(WaitingCharacterId, EncounterPosition);
            _bags.AddMemberBag();
            _combat.AddMemberState();
        }

        ClearEncounter();
    }

    public bool GrantEarlyLoot(int wave)
    {
        if (!_isFirstExpedition || wave > 3)
        {
            return false;
        }

        _bags.AddLoot(wave == 1 ? FirstLoot : wave == 2 ? SecondLoot : ThirdLoot);
        return true;
    }

    public bool IsLearningAtCamp(int wave) => _isFirstExpedition && wave <= 3;

    // 입구를 열려면 자동 공격이 필요하므로 공격 가능한 동료가 한 명은 있어야 한다.
    public bool CanLeaveCamp(int wave)
    {
        if (!_isFirstExpedition || wave >= 3)
        {
            return true;
        }

        foreach (var member in _party.MemberList)
        {
            if (!member.IsAlive)
            {
                continue;
            }

            foreach (var itemId in member.EquippedItemIdList)
            {
                var mode = _items.Get(itemId).Mode;
                if (mode is not (ItemMode.Passive or ItemMode.Heal or ItemMode.HealArea))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void CompleteOpening()
    {
        if (_isFirstExpedition && IsPending)
        {
            PlayerPrefs.SetInt(CompletionKey, 1);
            PlayerPrefs.Save();
        }
    }

    public void ClearEncounter()
    {
        _guards.Clear();
        _hasApproached = false;
        WaitingCharacterId = null;
    }
}
