using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSUI
    {
        public int HeldRotation;
        bool detailed; int detailPage, detailPages = 1;
        DSPointer previewTarget, confirmedTapTarget;
        public string ItemDescription(Gear i)
        {
            var d = g.Items[i.type];
            string mode = d.attackMode == "heal" || d.attackMode == "healArea" ? "회복" : "피해";
            string basis = string.IsNullOrEmpty(d.attackMode)
                ? EffectAmount(d.baseEffect, d.baseValue * g.Data.grades[i.grade].mult)
                : $"{mode} {d.damage * g.Data.grades[i.grade].mult:0.##} · 기본 {d.interval}초 · {d.range * 42:0}px";
            string bonus = string.IsNullOrEmpty(d.bonusEffect) ? "없음" : EffectAmount(d.bonusEffect, g.Equipment.BonusValue(i));
            if (d.bonusEffect == "share") bonus = "자기 회복량을 다른 생존자에게 공유 (최고 제공자 1명)";
            if (d.bonusEffect == "pulse") bonus += $" · {d.effectInterval:0.##}초 · {d.effectRadius:0}px";
            string condition = d.requiresAdjacent ? "대상 장비 옆 배치" : "인접 조건 없음";
            if (!string.IsNullOrEmpty(d.targetMode)) condition += " / " + TargetName(d.targetMode);
            var owner = g.Owner(i);
            string state = owner == null ? "장착 위치에 따라 판정" : g.Equipment.BonusState(owner, i);
            return $"{d.name} · {Grade(i)}{(string.IsNullOrEmpty(d.attackMode) ? "" : " · " + AttackName(d.attackMode))}\n기본 효과: {basis}\n추가 효과: {bonus}\n조건: {condition}\n현재: {state}\n표시 주기는 기본값 (속도 효과 적용 전)";
        }
        string AttackName(string mode) => mode == "melee" ? "근접 베기" : mode == "sweep" ? "부채꼴 공격" : mode == "projectile" ? "단일 투사체" : mode == "explosion" ? "폭발 투사체" : mode == "chain" ? "연쇄 공격" : mode == "heal" ? "단일 회복" : mode == "healArea" ? "주변 회복" : mode;
        string TargetName(string mode) => mode == "attack" ? "공격 무기" : mode == "projectile" ? "투사체 무기" : mode == "chain" ? "연쇄 무기" : mode == "explosion" ? "폭발 무기" : mode == "heal" ? "단일 회복 무기" : mode == "healArea" ? "범위 회복 무기" : mode;
        string EffectAmount(string effect, float value)
        {
            if (new[] { "attack", "def", "haste", "range", "radius" }.Contains(effect)) return EffectName(effect) + " +" + (value * 100).ToString("0.##") + "%";
            string unit = effect == "pierce" || effect == "chain" ? "명" : effect == "extraShots" ? "발" : effect == "hp" || effect == "heal" || effect == "shield" || effect == "startShield" ? " HP" : "";
            return EffectName(effect) + " " + value.ToString("0.##") + unit;
        }
        string EffectName(string effect)
        {
            switch (effect)
            {
                case "attack":
                    return "공격 증가율";
                case "def":
                    return "방어율";
                case "hp":
                    return "최대 HP";
                case "heal":
                    return "자기 회복/3초";
                case "share":
                    return "파티 회복 공유";
                case "haste":
                    return "속도 증가율";
                case "range":
                    return "사거리 증가율";
                case "pierce":
                    return "추가 관통";
                case "extraShots":
                    return "추가 발사";
                case "chain":
                    return "추가 연쇄";
                case "shield":
                    return "8초 보호막";
                case "startShield":
                    return "시작 보호막";
                case "radius":
                    return "폭발 반경";
                case "pulse":
                    return "3초 범위 피해";
                default:
                    return effect;
            }
        }
        void ShowEquipmentText(string text, bool details)
        {
            HideTooltip();
            foreach (Transform child in page)
            if (child.name == "FormationPreview" || DSGame.FormationNames.Contains(child.name))
                child.gameObject.SetActive(false);
            tooltip = Box("EquipmentComparison", root, W / 2 - 270, H - 440, 540, 320, "#101b20fa");
            Text(tooltip, text, 14, 10, 512, 260, 17);
            if (details && detailed)
                Button(tooltip, "다음 " + (detailPage + 1) + "/" + detailPages, 240, 278, 140, 32, () => { detailPage = (detailPage + 1) % detailPages; if (previewTarget != null) ShowEquipmentPreview(previewTarget); });
            if (details)
                Button(tooltip, detailed ? "간략히" : "상세 보기", 390, 278, 130, 32, () => { detailed = !detailed; detailPage = 0; if (previewTarget != null) ShowEquipmentPreview(previewTarget); });
        }
        void ShowEquipmentSelection()
        {
            var item = g.Find(Held);
            if (item == null)
                return;
            previewTarget = null;
            confirmedTapTarget = null;
            detailed = false;
            detailPage = 0;
            foreach (var marker in page.GetComponentsInChildren<Transform>().Where(t => (t.name == "Recommendation" || t.name == "BasicEffectNote")).ToArray())
            {
                marker.gameObject.SetActive(false);
                Destroy(marker.gameObject);
            }
            foreach (var outline in page.GetComponentsInChildren<UnityEngine.UI.Outline>())
                outline.enabled = false;
            var d = g.Items[item.type];
            foreach (var c in g.Team)
            {
                if (c.hp <= 0 || !string.IsNullOrEmpty(d.attackMode)) continue;
                if (string.IsNullOrEmpty(d.bonusEffect))
                {
                    var basePanel = page.Find("Inventory_" + g.Def(c).id);
                    if (basePanel != null) Text(basePanel, "기본 효과만 적용", 12, 30, 280, 24, 16, "#b7c8ca").name = "BasicEffectNote";
                    continue;
                }
                var candidate = EquipmentRules.Clone(item);
                var copy = EquipmentRules.CloneTeam(new List<Survivor> { c })[0];
                copy.items.Add(candidate);
                string state = g.Equipment.BonusState(copy, candidate);
                if (state == "대상 공격 방식 없음")
                    continue;
                var panel = page.Find("Inventory_" + g.Def(c).id);
                if (panel != null)
                {
                    Outline(panel.gameObject, "#e9c76b");
                    var note = Text(panel, "[추천] " + (d.requiresAdjacent ? "대상 장비 옆 배치" : d.bonusEffect == "share" ? "파티 회복 공유" : "추가 효과"), 12, 30, 280, 24, 16, "#e9c76b");
                    note.name = "Recommendation";
                }
            }
            ShowEquipmentText(ItemDescription(item) + "\n위치 선택 후 확정 · 회전 " + HeldRotation * 90 + "°", false);
        }
        void Outline(GameObject go, string color)
        {
            var o = go.GetComponent<UnityEngine.UI.Outline>() ?? go.AddComponent<UnityEngine.UI.Outline>();
            o.enabled = true;
            o.effectColor = C(color);
            o.effectDistance = new Vector2(2, 2);
        }
        void FlashItem(int id)
        {
            foreach (var p in page.GetComponentsInChildren<DSPointer>().Where(p => p.item == id))
                Outline(p.gameObject, "#e9c76b");
            StartCoroutine(ClearFlash());
        }
        System.Collections.IEnumerator ClearFlash()
        {
            yield return new WaitForSecondsRealtime(.35f);
            if (Held < 0 && page != null)
            foreach (var o in page.GetComponentsInChildren<UnityEngine.UI.Outline>())
                o.enabled = false;
        }
        void PulseRecommendations()
        {
            if (Held < 0 || page == null)
                return;
            foreach (var marker in page.GetComponentsInChildren<Transform>().Where(t => t.name == "Recommendation"))
            {
                var outline = marker.parent.GetComponent<UnityEngine.UI.Outline>();
                if (outline != null && !(previewTarget != null && previewTarget.character >= 0 && marker.parent.name == "Inventory_" + g.Def(g.Team[previewTarget.character]).id))
                {
                    var color = C("#e9c76b");
                    color.a = .7f + .2f * Mathf.Sin(Time.unscaledTime * 2);
                    outline.effectColor = color;
                }
            }
        }
        void ClearPreviewCells()
        {
            if (page == null)
                return;
            foreach (var cell in page.GetComponentsInChildren<Transform>().Where(t => t.name == "PlacementPreview").ToArray())
            {
                cell.gameObject.SetActive(false);
                Destroy(cell.gameObject);
            }
        }
        void PaintPreview(Gear item, int ci, int x, int y, int rotation, bool ok)
        {
            foreach (var q in g.Equipment.Shape(item, rotation))
            {
                var cell = page.GetComponentsInChildren<DSPointer>().FirstOrDefault(p => p.character == ci && p.x == x + q.x && p.y == y + q.y);
                if (cell == null)
                    continue;
                var rt = cell.GetComponent<RectTransform>();
                var overlay = Box("PlacementPreview", cell.transform, 0, 0, rt.rect.width, rt.rect.height, ok ? "#81af5d66" : "#dd787866");
                if (!ok)
                    Text(overlay, "×", 2, 2, rt.rect.width - 4, 24, 18, "#ffffff");
            }
        }
        void ShowEquipmentPreview(DSPointer target)
        {
            if (previewTarget != target)
            {
                detailPage = 0;
                confirmedTapTarget = null;
            }
            previewTarget = target;
            ClearPreviewCells();
            var item = g.Find(Held);
            if (item == null)
                return;
            foreach (var o in page.GetComponentsInChildren<UnityEngine.UI.Outline>())
                o.enabled = o.transform.Find("Recommendation") != null;
            bool unequip = IsUnequipTarget(target);
            bool stashMerge = !unequip && target.character < 0 && !target.stash && target.item >= 0 && target.item != Held;
            int targetCharacter = unequip ? g.Team.IndexOf(g.Owner(item)) : stashMerge ? Mathf.Max(0, g.Team.IndexOf(g.Owner(item))) : target.character;
            if (targetCharacter < 0)
            {
                ShowEquipmentText(ItemDescription(item) + "\n위치 선택 후 확정", false);
                return;
            }
            var targetPanel = page.Find("Inventory_" + g.Def(g.Team[targetCharacter]).id);
            if (targetPanel != null && (!stashMerge || g.Owner(item) != null)) Outline(targetPanel.gameObject, "#71d7f0");
            var team = EquipmentRules.CloneTeam(g.Team);
            var stash = g.Stash.Select(EquipmentRules.Clone).ToList();
            string reason;
            bool ok = unequip ? g.Equipment.Unequip(team, stash, Held, out reason)
                : stashMerge ? g.Equipment.Merge(team, stash, Held, target.item, out reason)
                : g.Equipment.Drop(team, stash, Held, targetCharacter, target.x, target.y, HeldRotation, out reason);
            if (!ok)
            {
                if (!unequip && !stashMerge) PaintPreview(item, targetCharacter, target.x, target.y, HeldRotation, false);
                Outline(target.gameObject, "#dd7878");
                ShowEquipmentText("× " + reason, false);
                return;
            }
            Gear changed = null;
            var previewOwner = team[targetCharacter];
            if (!unequip && !stashMerge)
            {
                changed = previewOwner.items.FirstOrDefault(i => i.id == Held) ?? previewOwner.items.FirstOrDefault(i => g.Equipment.Shape(i).Any(q => i.x + q.x == target.x && i.y + q.y == target.y));
                var shape = changed == null ? g.Equipment.Shape(item, HeldRotation) : g.Shape(changed);
                int x = changed?.x ?? target.x, y = changed?.y ?? target.y;
                PaintPreview(changed ?? item, targetCharacter, x, y, changed?.rotation ?? HeldRotation, true);
                foreach (var q in shape)
                {
                    var cell = page.GetComponentsInChildren<DSPointer>().FirstOrDefault(p => p.character == targetCharacter && p.x == x + q.x && p.y == y + q.y);
                    if (cell != null) Outline(cell.gameObject, "#81af5d");
                }
                if (changed != null)
                {
                    var related = g.Equipment.IsAction(changed)
                        ? previewOwner.items.Where(i => g.Items[i.type].requiresAdjacent && g.Equipment.BonusApplies(previewOwner, i, changed))
                        : g.Equipment.Actions(previewOwner).Where(w => g.Items[changed.type].requiresAdjacent && g.Equipment.BonusApplies(previewOwner, changed, w));
                    foreach (var other in related)
                        foreach (var cell in page.GetComponentsInChildren<DSPointer>().Where(p => p.character == targetCharacter && p.item == other.id))
                            Outline(cell.gameObject, "#e9c76b");
                }
            }
            var lines = new List<string>();
            if (stashMerge)
                lines.Add("보관함 합성 " + Grade(g.Find(target.item)) + " → " + Grade(stash.First(i => i.id == target.item)));
            if (unequip)
            {
                lines.Add(g.Items[item.type].name + " → 중앙 보관함");
            }
            bool sharedHealingChanged = false;
            for (int n = 0; n < team.Count; n++)
            {
                var before = g.Stats(g.Team[n]);
                var after = g.Equipment.Stats(team[n]);
                string name = g.Def(team[n]).name;
                Compare(lines, name + " 최대 HP", before.hp, after.hp, "");
                Compare(lines, name + " 방어", before.def * 100, after.def * 100, "%");
                Compare(lines, name + " 공격 증가", before.attack * 100, after.attack * 100, "%");
                Compare(lines, name + " 회복", before.heal, after.heal, "/주기");
                Compare(lines, name + " 회복 주기", 3 / (1 + before.haste), 3 / (1 + after.haste), "초");
                Compare(lines, name + " 보호막", before.shield, after.shield, "");
                Compare(lines, name + " 관통", before.pierce, after.pierce, "명");
                Compare(lines, name + " 추가 발사", before.extraShots, after.extraShots, "발");
                Compare(lines, name + " 연쇄", before.chain, after.chain, "명");
                Compare(lines, name + " 반경 증가", before.radius * 100, after.radius * 100, "%");
                Compare(lines, name + " 주기 피해", before.pulse, after.pulse, "/3초");
                var oldActions = g.Equipment.Actions(g.Team[n]).ToDictionary(i => i.id);
                var newActions = g.Equipment.Actions(team[n]).ToDictionary(i => i.id);
                foreach (int id in oldActions.Keys.Union(newActions.Keys).OrderBy(id => id))
                {
                    oldActions.TryGetValue(id, out var wb); newActions.TryGetValue(id, out var wa);
                    string label = name + " " + g.Items[(wa ?? wb).type].name + " #" + id;
                    var bs = wb == null ? default : g.Equipment.Stats(g.Team[n], wb);
                    var ass = wa == null ? default : g.Equipment.Stats(team[n], wa);
                    if (wb != null && wa != null)
                    {
                        Compare(lines, label + " 피해/회복", g.Equipment.WeaponPower(g.Team[n], wb), g.Equipment.WeaponPower(team[n], wa), "");
                        Compare(lines, label + " 주기", g.Items[wb.type].interval / (1 + bs.haste), g.Items[wa.type].interval / (1 + ass.haste), "초");
                        Compare(lines, label + " 거리", g.Items[wb.type].range * 42 * (1 + bs.range), g.Items[wa.type].range * 42 * (1 + ass.range), "px");
                        Compare(lines, label + " 관통", bs.pierce, ass.pierce, "명");
                        Compare(lines, label + " 추가 발사", bs.extraShots, ass.extraShots, "발");
                        Compare(lines, label + " 연쇄", bs.chain, ass.chain, "명");
                        Compare(lines, label + " 폭발 반경 증가", bs.radius * 100, ass.radius * 100, "%");
                    }
                    else
                    {
                        Compare(lines, label + " 관통", bs.pierce, ass.pierce, "명");
                        Compare(lines, label + " 추가 발사", bs.extraShots, ass.extraShots, "발");
                        Compare(lines, label + " 연쇄", bs.chain, ass.chain, "명");
                        Compare(lines, label + " 폭발 반경 증가", bs.radius * 100, ass.radius * 100, "%");
                        string powerBefore = wb == null ? "없음" : g.Equipment.WeaponPower(g.Team[n], wb).ToString("0.##");
                        string powerAfter = wa == null ? "없음" : g.Equipment.WeaponPower(team[n], wa).ToString("0.##");
                        lines.Add(label + " 피해/회복 " + powerBefore + " → " + powerAfter);
                        lines.Add(label + " 주기 " + (wb == null ? "없음" : (g.Items[wb.type].interval / (1 + bs.haste)).ToString("0.##") + "초") + " → " + (wa == null ? "없음" : (g.Items[wa.type].interval / (1 + ass.haste)).ToString("0.##") + "초"));
                        lines.Add(label + " 거리 " + (wb == null ? "없음" : (g.Items[wb.type].range * 42 * (1 + bs.range)).ToString("0.##") + "px") + " → " + (wa == null ? "없음" : (g.Items[wa.type].range * 42 * (1 + ass.range)).ToString("0.##") + "px"));
                    }
                }
                int pb = g.Equipment.Provider(g.Team, n), pa = g.Equipment.Provider(team, n);
                float hb = pb < 0 ? 0 : g.Stats(g.Team[pb]).heal / (3 / (1 + g.Stats(g.Team[pb]).haste)), ha = pa < 0 ? 0 : g.Equipment.Stats(team[pa]).heal / (3 / (1 + g.Equipment.Stats(team[pa]).haste));
                if (pb != pa || !Mathf.Approximately(hb, ha))
                {
                    sharedHealingChanged = true;
                    lines.Add(name + " 공유 " + hb.ToString("0.##") + " → " + ha.ToString("0.##") + " HP/초 (" + (pa < 0 ? "없음" : g.Def(team[pa]).name) + ")");
                }
            }
            if (g.Items[item.type].bonusEffect == "share" && !sharedHealingChanged) lines.Add("파티 공유 회복 변화 없음");
            if (lines.Count == 0)
                lines.Add("능력치·공유 회복 변화 없음");
            string bonus = unequip ? "장비 해제 미리보기" : stashMerge ? "보관함 합성 미리보기" : changed == null ? "합성" : g.Equipment.BonusState(team[targetCharacter], changed);
            detailPages = Mathf.Max(1, Mathf.CeilToInt(lines.Count / 7f));
            detailPage %= detailPages;
            var selected = detailed ? lines.Skip(detailPage * 7).Take(7).ToList() : lines.Take(3).ToList();
            ShowEquipmentText(g.Items[item.type].name + " · " + bonus + "\n" + string.Join("\n", selected) + (lines.Count > selected.Count ? "\n추가 변화 " + (lines.Count - selected.Count) + "개" : ""), true);
        }
        void Compare(List<string> lines, string label, float before, float after, string unit)
        {
            if (!Mathf.Approximately(before, after))
                {
                bool lowerIsBetter = label.EndsWith(" 주기") || label.EndsWith(" 간격");
                bool improved = lowerIsBetter ? after < before : after > before;
                lines.Add((improved ? "[개선] " : "[감소] ") + label + " " + before.ToString("0.##") + " → " + after.ToString("0.##") + unit);
            }
        }
    }
}
