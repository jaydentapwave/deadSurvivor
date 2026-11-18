using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using R3;
using UnityEngine;
using VContainer.Unity;

// 정비 화면 가방·보관함·설명(기획서 C절 V3)과 조작 흐름. 가방이 바뀌면(BagModel.Version) View마다 UpdateUI를 한 번 부른다.
// 공개 처리 메서드(On…)는 판정 진입점·봇이 그대로 부른다(사람이 누르는 길과 같다)
public class BagPresenter : IInitializable, IDisposable
{
    private const string StashItemPoolKey = "BagStashItem";
    private const int SummaryLineCount = 3;

    private readonly BagModel _bagModel;
    private readonly BagService _bagService;
    private readonly BagPreviewService _bagPreviewService;
    private readonly RunModel _runModel;
    private readonly PartyModel _partyModel;
    private readonly CombatModel _combatModel;
    private readonly ItemRepository _itemRepository;
    private readonly SpriteAtlasService _spriteAtlasService;
    private readonly GradeRepository _gradeRepository;
    private readonly CharacterRepository _characterRepository;
    private readonly CombatRulesRepository _combatRulesRepository;
    private readonly EquipmentStatService _equipmentStatService;
    private readonly PoolManager _poolManager;
    private readonly UI_PrepView _prepView;
    private readonly BagRulesData _bagRules;
    private readonly CompositeDisposable _disposables = new();
    private readonly List<Color> _gradeColorList = new();
    private readonly List<Vector2Int> _cellList = new();
    private readonly List<BagCellDisplay> _cellDisplayList = new();
    private readonly List<StashItemDisplay> _stashDisplayList = new();

    // 집은 아이템(−1 = 없음)과 집은 동안의 회전
    private int _pickedUid = -1;
    private int _pickedRotation;
    private BagTarget? _previewTarget;
    private BagPreviewResult _previewResult;
    private bool _isDetailShown;
    // 설명 칸에 보일 아이템(집은 것 또는 마지막으로 누른 것)
    private int _infoUid = -1;
    private string _resultLine = "";
    // 이번 누름이 아이템을 집은 누름인가(뗄 때 탭으로 치지 않는다), 집은 아이템을 끄는 누름인가(드래그 대상)
    private bool _isPressPicking;
    private bool _isDragPress;

    public int PickedUid => _pickedUid;
    public int PickedRotation => _pickedRotation;

    public BagPresenter(BagModel bagModel, BagService bagService, BagPreviewService bagPreviewService, RunModel runModel,
        PartyModel partyModel, CombatModel combatModel, ItemRepository itemRepository, GradeRepository gradeRepository,
        CharacterRepository characterRepository, CombatRulesRepository combatRulesRepository,
        EquipmentStatService equipmentStatService, BagRulesRepository bagRulesRepository, PoolManager poolManager,
        UI_PrepView prepView, SpriteAtlasService spriteAtlasService)
    {
        _bagModel = bagModel;
        _bagService = bagService;
        _bagPreviewService = bagPreviewService;
        _runModel = runModel;
        _partyModel = partyModel;
        _combatModel = combatModel;
        _itemRepository = itemRepository;
        _spriteAtlasService = spriteAtlasService;
        _gradeRepository = gradeRepository;
        _characterRepository = characterRepository;
        _combatRulesRepository = combatRulesRepository;
        _equipmentStatService = equipmentStatService;
        _poolManager = poolManager;
        _prepView = prepView;
        _bagRules = bagRulesRepository.Rules;
    }

    public void Initialize()
    {
        foreach (var grade in _gradeRepository.GradeDataList)
        {
            if (!ColorUtility.TryParseHtmlString(grade.Color, out var color))
            {
                GameLogger.LogError($"[BagPresenter] Grade {grade.Id} color '{grade.Color}' is not #rrggbb");
            }

            _gradeColorList.Add(color);
        }

        var bagViews = _prepView.BagViews;
        for (var member = 0; member < bagViews.Count; member++)
        {
            var bagView = bagViews[member];
            if (bagView.CellCount != _bagRules.BagWidth * _bagRules.BagHeight)
            {
                GameLogger.LogError($"[BagPresenter] Bag view {member} has {bagView.CellCount} cells, "
                    + $"bag is {_bagRules.BagWidth}×{_bagRules.BagHeight}");
            }

            bagView.Initialize(member, _bagRules.BagWidth);
            SubscribeInput(bagView.Pressed, bagView.Tapped, bagView.Dragged, bagView.Dropped);
        }

        var stashView = _prepView.StashView;
        stashView.Initialize();
        SubscribeInput(stashView.Pressed, stashView.Tapped, stashView.Dragged, stashView.Dropped);
        _prepView.RotateButton.OnClickAsObservable().Subscribe(_ => OnRotateClicked()).AddTo(_disposables);
        _prepView.CancelButton.OnClickAsObservable().Subscribe(_ => OnCancelClicked()).AddTo(_disposables);
        _prepView.ItemInfoView.DetailButton.OnClickAsObservable().Subscribe(_ => OnDetailClicked()).AddTo(_disposables);
        _bagModel.Version.Subscribe(_ => OnBagChanged()).AddTo(_disposables);
        _runModel.Phase.Where(phase => phase == RunPhase.Prep).Subscribe(_ => OnPrepEntered()).AddTo(_disposables);
        _combatModel.ReviveCount.Subscribe(_ => OnMemberRevived()).AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();

    // 아이템을 집는다(설명 표시, 회전 = 그 아이템 회전). 쓰러진 대원의 아이템은 집지 않는다
    public void OnItemPressed(int uid)
    {
        var item = _bagModel.FindItem(uid, out var owner);
        if (item == null)
        {
            GameLogger.LogWarning($"[BagPresenter] No bag item {uid} to pick");
            return;
        }

        ClearPick();
        _infoUid = uid;
        if (owner >= 0 && !_partyModel.MemberList[owner].IsAlive)
        {
            _resultLine = GetReasonText(BagFailReason.Locked);
            Render();
            return;
        }

        _pickedUid = uid;
        _pickedRotation = item.Rotation;
        _resultLine = "";
        Render();
    }

    // 집은 상태에서 대상을 탭: 그곳을 미리보기, 미리보기 중인 같은 대상을 다시 탭하면 적용
    public void OnTargetTapped(BagTarget target)
    {
        if (_pickedUid < 0)
        {
            return;
        }

        if (_previewTarget.HasValue && _previewTarget.Value.IsSame(target))
        {
            Apply(target);
            return;
        }

        ShowPreview(target);
    }

    // 드래그 중 손가락 밑 대상으로 미리보기(대상이 없으면 미리보기를 지운다)
    public void OnItemDragged(BagTarget? target)
    {
        if (_pickedUid < 0)
        {
            return;
        }

        if (!target.HasValue)
        {
            ClearPreview();
            Render();
            return;
        }

        if (!_previewTarget.HasValue || !_previewTarget.Value.IsSame(target.Value))
        {
            ShowPreview(target.Value);
        }
    }

    // 드래그를 놓음: 대상이 있으면 적용, 없으면 취소(원래 자리 유지)
    public void OnItemDropped(BagTarget? target)
    {
        if (_pickedUid < 0)
        {
            GameLogger.LogWarning("[BagPresenter] Dropped with nothing picked");
            return;
        }

        if (!target.HasValue)
        {
            OnCancelClicked();
            return;
        }

        Apply(target.Value);
    }

    public void OnRotateClicked()
    {
        if (_pickedUid < 0)
        {
            return;
        }

        _pickedRotation = (_pickedRotation + 1) % 4;
        if (_previewTarget.HasValue)
        {
            ShowPreview(_previewTarget.Value);
            return;
        }

        Render();
    }

    public void OnCancelClicked()
    {
        ClearPick();
        Render();
    }

    public void OnDetailClicked()
    {
        _isDetailShown = !_isDetailShown;
        Render();
    }

    private void OnBagChanged() => Render();

    private void OnPrepEntered()
    {
        ClearPick();
        _infoUid = -1;
        _resultLine = "";
        _isDetailShown = false;
        Render();
    }

    private void OnMemberRevived() => Render();

    // View 입력 사실 → 처리 메서드(설계 브리핑 "입력 분기 계약")
    private void OnViewPressed(int uid)
    {
        _isPressPicking = false;
        _isDragPress = false;
        if (uid < 0)
        {
            return;
        }

        if (_pickedUid < 0)
        {
            OnItemPressed(uid);
            _isPressPicking = _pickedUid == uid;
            _isDragPress = _isPressPicking;
            return;
        }

        // 다른 아이템을 누르면 집기를 바꾸지 않는다(그 아이템을 대상으로 탭하려는 누름)
        _isDragPress = uid == _pickedUid;
    }

    private void OnViewTapped(BagTarget target)
    {
        var isPressPicking = _isPressPicking;
        _isPressPicking = false;
        _isDragPress = false;
        if (isPressPicking)
        {
            return;
        }

        OnTargetTapped(target);
    }

    private void OnViewDragged(BagTarget? target)
    {
        if (_isDragPress)
        {
            OnItemDragged(target);
        }
    }

    private void OnViewDropped(BagTarget? target)
    {
        var isDragPress = _isDragPress;
        _isPressPicking = false;
        _isDragPress = false;
        if (isDragPress)
        {
            OnItemDropped(target);
        }
    }

    private void SubscribeInput(Observable<int> pressed, Observable<BagTarget> tapped, Observable<BagTarget?> dragged,
        Observable<BagTarget?> dropped)
    {
        pressed.Subscribe(OnViewPressed).AddTo(_disposables);
        tapped.Subscribe(OnViewTapped).AddTo(_disposables);
        dragged.Subscribe(OnViewDragged).AddTo(_disposables);
        dropped.Subscribe(OnViewDropped).AddTo(_disposables);
    }

    private void ShowPreview(BagTarget target)
    {
        _previewTarget = target;
        _previewResult = _bagPreviewService.Preview(_pickedUid, _pickedRotation, target);
        Render();
    }

    // 적용 뒤 집기를 풀고 결과 한 줄(성공 "적용했다" / 실패 이유)
    private void Apply(BagTarget target)
    {
        var uid = _pickedUid;
        var isApplied = _bagService.Apply(uid, _pickedRotation, target, out var reason);
        ClearPick();
        _infoUid = uid;
        _resultLine = isApplied ? "적용했다" : GetReasonText(reason);
        Render();
    }

    private void ClearPick()
    {
        _pickedUid = -1;
        _pickedRotation = 0;
        _isPressPicking = false;
        _isDragPress = false;
        ClearPreview();
    }

    private void ClearPreview()
    {
        _previewTarget = null;
        _previewResult = null;
    }

    private void Render()
    {
        RenderBags();
        RenderStash();
        RenderInfo();
    }

    private void RenderBags()
    {
        var bagViews = _prepView.BagViews;
        var cellCount = _bagRules.BagWidth * _bagRules.BagHeight;
        for (var member = 0; member < bagViews.Count; member++)
        {
            _cellDisplayList.Clear();
            for (var i = 0; i < cellCount; i++)
            {
                _cellDisplayList.Add(new BagCellDisplay { Uid = -1, Label = "" });
            }

            var hasBag = member < _bagModel.MemberCount;
            if (hasBag)
            {
                foreach (var item in _bagModel.GetBag(member))
                {
                    FillItemCells(item);
                }
            }

            if (_previewResult != null && _previewResult.Member == member)
            {
                var mark = _previewResult.IsSuccess ? BagCellMark.Valid : BagCellMark.Invalid;
                foreach (var cell in _previewResult.CellList)
                {
                    var index = cell.y * _bagRules.BagWidth + cell.x;
                    var display = _cellDisplayList[index];
                    display.Mark = mark;
                    _cellDisplayList[index] = display;
                }
            }

            var isDown = member < _partyModel.MemberList.Count && !_partyModel.MemberList[member].IsAlive;
            bagViews[member].UpdateUI(_cellDisplayList, isDown);
        }
    }

    // 회전된 아이콘의 각 점유 칸만 그린다. 입력·등급·미리보기는 기존 칸에 남긴다.
    private void FillItemCells(BagItem item)
    {
        var itemData = _itemRepository.Get(item.ItemId);
        var icon = GetIcon(itemData);
        var size = icon != null ? ItemIconUtils.GetSize(icon, item.Rotation) : Vector2Int.one;
        BagRules.GetItemCells(item, _itemRepository, _cellList);
        for (var i = 0; i < _cellList.Count; i++)
        {
            var cell = _cellList[i];
            if (cell.x < 0 || cell.y < 0 || cell.x >= _bagRules.BagWidth || cell.y >= _bagRules.BagHeight)
            {
                GameLogger.LogError($"[BagPresenter] Bag item {item.Uid} cell {cell} out of bag");
                continue;
            }

            _cellDisplayList[cell.y * _bagRules.BagWidth + cell.x] = new BagCellDisplay
            {
                Uid = item.Uid,
                Label = i == 0 ? GetShortName(itemData) : "",
                Icon = icon,
                IconUv = ItemIconUtils.GetCellUv(new Vector2Int(cell.x - item.X, cell.y - item.Y), size),
                Rotation = item.Rotation,
                GradeColor = GetGradeColor(item.Grade),
                IsPicked = item.Uid == _pickedUid,
            };
        }
    }

    private void RenderStash()
    {
        var stashView = _prepView.StashView;
        var stashList = _bagModel.StashList;
        while (stashView.ItemViewCount < stashList.Count)
        {
            stashView.AddItemView(_poolManager.Get(StashItemPoolKey, stashView.ItemTemplate, stashView.Content));
        }

        _stashDisplayList.Clear();
        foreach (var item in stashList)
        {
            var itemData = _itemRepository.Get(item.ItemId);
            var cellList = new List<Vector2Int>();
            var rotation = item.Uid == _pickedUid ? _pickedRotation : item.Rotation;
            BagRules.GetCells(itemData.Shape, rotation, cellList);
            _stashDisplayList.Add(new StashItemDisplay
            {
                Uid = item.Uid,
                Name = itemData.Name,
                Icon = GetIcon(itemData),
                Rotation = rotation,
                GradeColor = GetGradeColor(item.Grade),
                CellList = cellList,
                IsNew = _bagModel.LastGrantedUidList.Contains(item.Uid),
                IsPicked = item.Uid == _pickedUid,
            });
        }

        stashView.UpdateUI(_stashDisplayList);
    }

    private void RenderInfo()
    {
        var description = BuildDescription();
        if (_resultLine.Length > 0)
        {
            description = description.Length > 0 ? $"{description}\n{_resultLine}" : _resultLine;
        }

        var preview = "";
        var hasMoreLines = false;
        if (_previewResult != null)
        {
            if (!_previewResult.IsSuccess)
            {
                preview = $"놓을 수 없음: {GetReasonText(_previewResult.Reason)}";
            }
            else if (_previewResult.LineList.Count == 0)
            {
                preview = "능력치 변화 없음";
            }
            else
            {
                var lineList = _previewResult.LineList;
                var shownCount = _isDetailShown ? lineList.Count : Mathf.Min(SummaryLineCount, lineList.Count);
                preview = string.Join("\n", lineList.GetRange(0, shownCount));
                hasMoreLines = lineList.Count > SummaryLineCount;
                if (!_isDetailShown && hasMoreLines)
                {
                    preview += $"\n추가 변화 {lineList.Count - SummaryLineCount}개";
                }
            }
        }

        var infoItem = _bagModel.FindItem(_infoUid, out _);
        var icon = infoItem != null ? GetIcon(_itemRepository.Get(infoItem.ItemId)) : null;
        var rotation = infoItem != null ? infoItem.Uid == _pickedUid ? _pickedRotation : infoItem.Rotation : 0;
        _prepView.ItemInfoView.UpdateUI(description, preview, hasMoreLines, _isDetailShown && hasMoreLines, icon, rotation);
    }

    // 이름, 등급, 발동 방식, 피해·회복량(× 등급 배수)·간격·사거리(× 사거리 단위), 기본·추가 효과와 조건, 지금 상태
    private string BuildDescription()
    {
        if (_infoUid < 0)
        {
            return "";
        }

        var item = _bagModel.FindItem(_infoUid, out var owner);
        if (item == null)
        {
            return "";
        }

        var itemData = _itemRepository.Get(item.ItemId);
        var grade = _gradeRepository.Get(item.Grade);
        var builder = new StringBuilder();
        builder.Append($"{itemData.Name}  [{grade.Name}]  {GetModeText(itemData.Mode)}");
        if (itemData.Mode != ItemMode.Passive)
        {
            var powerName = itemData.IsHealMode ? "회복량" : "피해";
            var range = itemData.Range * _combatRulesRepository.Rules.RangeUnit;
            builder.Append($"\n{powerName} {F(itemData.Power * grade.Mult)} · 간격 {F(itemData.Interval)}초 · 사거리 {F(range)}");
        }

        if (itemData.BaseEffect != ItemEffectKind.None)
        {
            builder.Append($"\n기본: {GetEffectText(itemData.BaseEffect, itemData.BaseValue * grade.Mult)}");
        }

        if (itemData.BonusEffect != ItemEffectKind.None)
        {
            var bonusValue = _equipmentStatService.GetBonusValue(itemData, grade.Mult);
            builder.Append($"\n추가: {GetEffectText(itemData.BonusEffect, bonusValue)}");
            if (itemData.BonusTarget != ItemBonusTarget.Any || itemData.BonusNeedsAdjacent)
            {
                builder.Append($" (대상 {GetTargetText(itemData.BonusTarget)}{(itemData.BonusNeedsAdjacent ? ", 인접 필요" : "")})");
            }

            builder.Append($"\n상태: {GetBonusStateText(item, owner)}");
        }

        return builder.ToString();
    }

    // 가방에 있으면 활성·인접 필요·대상 없음, 보관함이면 놓는 곳에 따라
    private string GetBonusStateText(BagItem item, int owner)
    {
        if (owner < 0)
        {
            return "놓는 곳에 따라";
        }

        var member = _partyModel.MemberList[owner];
        var equippedIdList = member.EquippedItemIdList;
        var slot = IndexOfUid(_bagModel.GetBag(owner), item.Uid);
        if (_equipmentStatService.IsBonusActive(owner, equippedIdList, slot))
        {
            return "활성";
        }

        // 모든 칸이 붙어 있다고 치면 켜지는가 → 인접만 모자람
        var adjacentAssumed = new EquipmentStatService(_itemRepository, _combatRulesRepository,
            new AdjacentAssumedPlacementInfo(new BagPlacementInfo(_bagModel, _gradeRepository, _itemRepository)));
        return adjacentAssumed.IsBonusActive(owner, equippedIdList, slot) ? "인접 필요" : "대상 없음";
    }

    private static int IndexOfUid(IReadOnlyList<BagItem> bag, int uid)
    {
        for (var i = 0; i < bag.Count; i++)
        {
            if (bag[i].Uid == uid)
            {
                return i;
            }
        }

        return -1;
    }

    private Color GetGradeColor(int grade) =>
        grade >= 0 && grade < _gradeColorList.Count ? _gradeColorList[grade] : Color.white;

    private Sprite GetIcon(ItemData itemData) =>
        string.IsNullOrEmpty(itemData.Icon) ? null : _spriteAtlasService.Get(itemData.Icon);

    private static string GetShortName(ItemData itemData) =>
        itemData.Name.Length <= 2 ? itemData.Name : itemData.Name.Substring(0, 2);

    private static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string GetReasonText(BagFailReason reason)
    {
        switch (reason)
        {
            case BagFailReason.Locked:
                return "쓰러진 대원은 부활한 뒤 장비를 바꿀 수 있다";
            case BagFailReason.OutOfBag:
                return "가방 밖";
            case BagFailReason.Overlap:
                return "다른 아이템과 겹침";
            case BagFailReason.CannotMerge:
                return "합성 불가";
            case BagFailReason.InStash:
                return "이미 보관함에 있다";
            case BagFailReason.NotPrep:
                return "정비 화면이 아니다";
            case BagFailReason.NotFound:
                return "아이템이 없다";
            default:
                return "";
        }
    }

    private static string GetModeText(ItemMode mode)
    {
        switch (mode)
        {
            case ItemMode.Melee:
                return "근접";
            case ItemMode.Sweep:
                return "휘두르기";
            case ItemMode.Projectile:
                return "투사체";
            case ItemMode.Explosion:
                return "폭발";
            case ItemMode.Chain:
                return "연쇄";
            case ItemMode.Heal:
                return "회복";
            case ItemMode.HealArea:
                return "범위 회복";
            default:
                return "지속 효과";
        }
    }

    private static string GetTargetText(ItemBonusTarget target)
    {
        switch (target)
        {
            case ItemBonusTarget.Any:
                return "발동 아이템";
            case ItemBonusTarget.Attack:
                return "공격 발동";
            case ItemBonusTarget.Melee:
                return "근접";
            case ItemBonusTarget.Sweep:
                return "휘두르기";
            case ItemBonusTarget.Projectile:
                return "투사체";
            case ItemBonusTarget.Explosion:
                return "폭발";
            case ItemBonusTarget.Chain:
                return "연쇄";
            case ItemBonusTarget.Heal:
                return "회복";
            default:
                return "범위 회복";
        }
    }

    // 비율 효과(공격·방어·가속·사거리·반경)는 %, 그 밖은 값
    private static string GetEffectText(ItemEffectKind kind, float value)
    {
        switch (kind)
        {
            case ItemEffectKind.Attack:
                return $"공격 +{P(value)}";
            case ItemEffectKind.Def:
                return $"방어 +{P(value)}";
            case ItemEffectKind.Hp:
                return $"최대 HP +{F(value)}";
            case ItemEffectKind.Heal:
                return $"자기 회복 {F(value)}";
            case ItemEffectKind.Haste:
                return $"가속 +{P(value)}";
            case ItemEffectKind.Range:
                return $"사거리 +{P(value)}";
            case ItemEffectKind.Radius:
                return $"폭발 반경 +{P(value)}";
            case ItemEffectKind.Shield:
                return $"보호막 {F(value)}";
            case ItemEffectKind.StartShield:
                return $"시작 보호막 {F(value)}";
            case ItemEffectKind.Pierce:
                return $"관통 {F(value)}";
            case ItemEffectKind.ExtraShots:
                return $"갈래 {F(value)}";
            case ItemEffectKind.Chain:
                return $"추가 연쇄 {F(value)}";
            case ItemEffectKind.Pulse:
                return $"파동 {F(value)}";
            case ItemEffectKind.Share:
                return "회복 나눔";
            default:
                return "";
        }
    }

    private static string P(float value) => (value * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    // 설명의 "인접 필요" 판정용: 등급 배수는 가방 그대로, 모든 칸이 붙어 있다고 친다
    private class AdjacentAssumedPlacementInfo : IItemPlacementInfo
    {
        private readonly IItemPlacementInfo _placementInfo;

        public AdjacentAssumedPlacementInfo(IItemPlacementInfo placementInfo)
        {
            _placementInfo = placementInfo;
        }

        public float GetGradeMultiplier(int memberIndex, int slotIndex) => _placementInfo.GetGradeMultiplier(memberIndex, slotIndex);

        public bool IsAdjacent(int memberIndex, int slotA, int slotB) => true;
    }
}
