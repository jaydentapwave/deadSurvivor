using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

// 읽기 전용 신호. 기존 입력 표면을 가리지 않고, 실제 전리품과 놓을 수 있는 칸을 비춘다.
public sealed class FirstExpeditionPresenter : IInitializable, ILateTickable, IDisposable
{
    private static readonly Color Amber = new(1f, .81f, .42f);
    private static readonly Color Mint = new(.68f, .94f, .75f);
    private readonly List<CanvasGroup> _campCues = new();
    private readonly Image[] _breadcrumbs = new Image[3];
    private readonly FirstExpeditionService _expedition;
    private readonly RunModel _run;
    private readonly RunService _runService;
    private readonly RunRulesData _runRules;
    private readonly TerrainHeightGrid _terrain;
    private readonly Camera _camera;
    private readonly UI_BattleHudView _hud;
    private readonly UI_PrepView _prep;
    private readonly BagModel _bags;
    private readonly PartyModel _party;
    private readonly ItemRepository _items;
    private readonly BagRulesData _bagRules;
    private readonly GradeRepository _grades;

    private RectTransform _fieldCue;
    private Text _travelerMark;
    private int _lastBagVersion = -1;
    private int _lastDownMask = -1;
    private int _lastCamp = -1;

    public FirstExpeditionPresenter(FirstExpeditionService expedition, RunModel run, RunService runService, RunRulesData runRules,
        TerrainHeightGrid terrain, Camera camera, UI_BattleHudView hud, UI_PrepView prep, BagModel bags,
        PartyModel party, ItemRepository items, BagRulesRepository bagRules, GradeRepository grades)
    {
        _expedition = expedition;
        _run = run;
        _runService = runService;
        _runRules = runRules;
        _terrain = terrain;
        _camera = camera;
        _hud = hud;
        _prep = prep;
        _bags = bags;
        _party = party;
        _items = items;
        _bagRules = bagRules.Rules;
        _grades = grades;
    }

    public void Initialize()
    {
        _fieldCue = new GameObject("Encounter cue", typeof(RectTransform)).GetComponent<RectTransform>();
        _fieldCue.SetParent(_hud.transform, false);
        _fieldCue.anchorMin = Vector2.zero;
        _fieldCue.anchorMax = Vector2.one;
        _fieldCue.offsetMin = _fieldCue.offsetMax = Vector2.zero;
        _fieldCue.SetAsFirstSibling();

        for (var i = 0; i < _breadcrumbs.Length; i++)
        {
            var image = new GameObject("Trail glint", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(_fieldCue, false);
            image.rectTransform.sizeDelta = Vector2.one * (5f + i);
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            image.raycastTarget = false;
            _breadcrumbs[i] = image;
        }

        _travelerMark = new GameObject("Traveler mark", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        _travelerMark.rectTransform.SetParent(_fieldCue, false);
        _travelerMark.rectTransform.sizeDelta = new Vector2(24f, 32f);
        _travelerMark.font = _hud.EncounterCueFont;
        _travelerMark.fontSize = 22;
        _travelerMark.fontStyle = FontStyle.Bold;
        _travelerMark.alignment = TextAnchor.MiddleCenter;
        _travelerMark.text = "!";
        _travelerMark.color = Amber;
        _travelerMark.raycastTarget = false;
        _fieldCue.gameObject.SetActive(false);
    }

    public void LateTick()
    {
        var time = Time.unscaledTime;
        RenderField(time);
        RenderCamp(time);
    }

    public void Dispose()
    {
        ClearCampCues();
        if (_fieldCue != null)
        {
            UnityEngine.Object.Destroy(_fieldCue.gameObject);
        }
    }

    private void RenderField(float time)
    {
        var showCue = _run.Phase.CurrentValue == RunPhase.Battle && _expedition.HasEncounter;
        _fieldCue.gameObject.SetActive(showCue);
        if (!showCue)
        {
            return;
        }

        for (var i = 0; i < _breadcrumbs.Length; i++)
        {
            var progress = (i + 1f) / (_breadcrumbs.Length + 1f);
            var point = Vector2.Lerp(_expedition.EncounterStart, _expedition.EncounterPosition, progress);
            var image = _breadcrumbs[i];
            Project(image.rectTransform, point, .06f);
            image.color = new Color(Amber.r, Amber.g, Amber.b, .36f + .5f * Pulse(time - progress * .6f));
        }

        var isRescue = _expedition.WaitingCharacterId != null;
        _travelerMark.gameObject.SetActive(isRescue);
        if (isRescue)
        {
            Project(_travelerMark.rectTransform, _expedition.EncounterPosition, 1.28f + .08f * Pulse(time));
        }
    }

    private void Project(RectTransform cue, Vector2 floor, float height)
    {
        var world = IsoProjection.FloorToWorld(floor, _runRules.UnitScale, _terrain) + Vector3.up * height;
        var screen = _camera.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_fieldCue, screen, null, out var local);
        var rect = _fieldCue.rect;
        cue.anchoredPosition = new Vector2(Mathf.Clamp(local.x, rect.xMin + 20f, rect.xMax - 20f),
            Mathf.Clamp(local.y, rect.yMin + 72f, rect.yMax - 50f));
    }

    private void RenderCamp(float time)
    {
        var wave = _run.CompletedWave.CurrentValue;
        var camp = _run.Phase.CurrentValue == RunPhase.Prep && _expedition.IsLearningAtCamp(wave) ? wave : -1;
        var downMask = 0;
        if (camp > 0)
        {
            for (var i = 0; i < _party.MemberList.Count; i++)
            {
                if (!_party.MemberList[i].IsAlive)
                {
                    downMask |= 1 << i;
                }
            }
        }

        var version = _bags.Version.CurrentValue;
        if (camp != _lastCamp || version != _lastBagVersion || downMask != _lastDownMask)
        {
            _lastCamp = camp;
            _lastBagVersion = version;
            _lastDownMask = downMask;
            ClearCampCues();
            _prep.NextWaveButton.interactable = _run.Phase.CurrentValue != RunPhase.Prep || _runService.CanStartNextWave;
            if (camp > 0)
            {
                SetCampCues(camp, downMask);
            }
        }

        var alpha = .65f + .35f * Pulse(time);
        foreach (var cue in _campCues)
        {
            cue.alpha = alpha;
        }
    }

    private void SetCampCues(int camp, int downMask)
    {
        for (var i = 0; i < _party.MemberList.Count; i++)
        {
            if ((downMask & (1 << i)) != 0)
            {
                AddCampCue(_prep.ReviveViews[i].ReviveButton.targetGraphic, Mint);
            }
        }

        var stash = _bags.StashList;
        for (var i = 0; i < stash.Count; i++)
        {
            var stone = stash[i];
            if (stone.ItemId != "whetstone" || stone.Grade != 0)
            {
                continue;
            }

            if (camp == 1 && (downMask & 1) == 0 && TryMarkPlacement(stone))
            {
                AddCampCue(_prep.StashView.GetItemGraphic(i), Amber);
            }
            else if (camp == 2 && TryMarkMerge(stone, i, downMask))
            {
                AddCampCue(_prep.StashView.GetItemGraphic(i), Amber);
            }

            break;
        }
    }

    private bool TryMarkPlacement(BagItem stone)
    {
        var shape = _items.Get(stone.ItemId).Shape;
        var bag = _bags.GetBag(0);
        var sword = BagRules.FindItemAt(bag, 0, 0, -1, _items);
        if (sword?.ItemId == "sword" && sword.Rotation == 0
            && BagRules.Fits(bag, stone.Uid, shape, stone.Rotation, 0, 1, _items,
                _bagRules.BagWidth, _bagRules.BagHeight, out _))
        {
            AddCampCue(_prep.BagViews[0].GetCellGraphic(_bagRules.BagWidth), Amber);
            return true;
        }

        return false;
    }

    private bool TryMarkMerge(BagItem source, int sourceIndex, int downMask)
    {
        var stash = _bags.StashList;
        for (var i = sourceIndex + 1; i < stash.Count; i++)
        {
            if (BagRules.CanMerge(source, stash[i], _grades.LastGrade))
            {
                AddCampCue(_prep.StashView.GetItemGraphic(i), Amber);
                return true;
            }
        }

        for (var member = 0; member < _bags.MemberCount; member++)
        {
            if ((downMask & (1 << member)) != 0)
            {
                continue;
            }

            foreach (var target in _bags.GetBag(member))
            {
                if (BagRules.CanMerge(source, target, _grades.LastGrade))
                {
                    var index = target.Y * _bagRules.BagWidth + target.X;
                    AddCampCue(_prep.BagViews[member].GetCellGraphic(index), Amber);
                    return true;
                }
            }
        }

        return false;
    }

    private void AddCampCue(Graphic graphic, Color color)
    {
        var cue = new GameObject("Camp cue", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>();
        var rect = (RectTransform)cue.transform;
        rect.SetParent(graphic.rectTransform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one;
        rect.offsetMax = -Vector2.one;
        cue.blocksRaycasts = false;

        const float thickness = 1.5f;
        for (var side = 0; side <= 1; side++)
        {
            AddCueEdge(rect, new Vector2(0f, side), new Vector2(1f, side), new Vector2(0f, thickness), color);
            AddCueEdge(rect, new Vector2(side, 0f), new Vector2(side, 1f), new Vector2(thickness, 0f), color);
        }

        _campCues.Add(cue);
    }

    private static void AddCueEdge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Color color)
    {
        var edge = new GameObject("Cue edge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        edge.rectTransform.SetParent(parent, false);
        edge.rectTransform.anchorMin = anchorMin;
        edge.rectTransform.anchorMax = anchorMax;
        edge.rectTransform.sizeDelta = size;
        edge.rectTransform.anchoredPosition = Vector2.zero;
        edge.raycastTarget = false;
        edge.color = color;
    }

    private void ClearCampCues()
    {
        foreach (var cue in _campCues)
        {
            cue.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(cue.gameObject);
        }

        _campCues.Clear();
    }

    private static float Pulse(float time) => .5f + .5f * Mathf.Sin(time * 5f);
}
