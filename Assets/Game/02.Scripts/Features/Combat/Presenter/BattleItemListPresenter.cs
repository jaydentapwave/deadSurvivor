using UnityEngine;
using VContainer.Unity;

// 전투 하단 아이템 칸(기획서 F절 V2): 전투 화면일 때 매 프레임 CombatModel을 읽어 칸마다 UpdateUI를 한 번 부른다.
// 전투 걸음(BattlePresenter.Tick) 뒤에 그리도록 LateTick(F1 BattleHudPresenter·WorldPresenter와 같다)
public class BattleItemListPresenter : ILateTickable
{
    private readonly RunModel _runModel;
    private readonly PartyModel _partyModel;
    private readonly CombatModel _combatModel;
    private readonly EquipmentStatService _equipmentStatService;
    private readonly SpriteAtlasService _spriteAtlasService;
    private readonly UI_BattleHudView _battleHudView;

    public BattleItemListPresenter(RunModel runModel, PartyModel partyModel, CombatModel combatModel,
        EquipmentStatService equipmentStatService, SpriteAtlasService spriteAtlasService, UI_BattleHudView battleHudView)
    {
        _runModel = runModel;
        _partyModel = partyModel;
        _combatModel = combatModel;
        _equipmentStatService = equipmentStatService;
        _spriteAtlasService = spriteAtlasService;
        _battleHudView = battleHudView;
    }

    public void LateTick()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            return;
        }

        var statusViews = _battleHudView.MemberStatusViews;
        var memberStateList = _combatModel.MemberStateList;
        for (var member = 0; member < statusViews.Count; member++)
        {
            var itemViews = statusViews[member].BattleItemViews;
            var slotList = member < memberStateList.Count ? memberStateList[member].SlotList : null;
            for (var slot = 0; slot < itemViews.Count; slot++)
            {
                var hasSlot = slotList != null && slot < slotList.Count;
                itemViews[slot].SetVisible(hasSlot);
                if (!hasSlot)
                {
                    continue;
                }

                var itemSlot = slotList[slot];
                var period = itemSlot.LastPeriod > 0f ? itemSlot.LastPeriod : GetCurrentPeriod(itemSlot);
                var barRatio = period > 0f ? itemSlot.DisplayCooldown / period : 0f;
                itemViews[slot].UpdateUI(itemSlot.Item.Name, GetIcon(itemSlot.Item), GetItemState(member, slot),
                    Mathf.Max(0f, itemSlot.DisplayCooldown), barRatio);
            }
        }
    }

    // 칸 상태(위가 먼저): 쓰러짐 → 쿨 없는 지속 칸 → 발동 표시 중 → 남은 쿨 > 0 → 준비
    public BattleItemState GetItemState(int memberIndex, int slotIndex)
    {
        var slot = _combatModel.MemberStateList[memberIndex].SlotList[slotIndex];
        if (!_partyModel.MemberList[memberIndex].IsAlive)
        {
            return BattleItemState.Disabled;
        }

        if (slot.Item.Mode == ItemMode.Passive && !slot.IsPulse)
        {
            return BattleItemState.Passive;
        }

        if (slot.FiredSeconds > 0f)
        {
            return BattleItemState.Fired;
        }

        return slot.DisplayCooldown > 0f ? BattleItemState.Cooling : BattleItemState.Ready;
    }

    // 마지막 주기가 0(아직 발동 전)이면 지금 주기: 발동 칸은 간격 ÷ (1 + 가속), 파동 칸은 효과 간격
    private float GetCurrentPeriod(BattleItemSlot slot)
    {
        return slot.IsPulse ? slot.Item.EffectInterval : _equipmentStatService.GetPeriod(slot.Item, slot.Stats);
    }

    // 그림 없는 시험용 아이템은 기존 이름 표시. 실제 32종은 아틀라스 이름 키.
    private Sprite GetIcon(ItemData item) =>
        string.IsNullOrEmpty(item.Icon) ? null : _spriteAtlasService.Get(item.Icon);
}
