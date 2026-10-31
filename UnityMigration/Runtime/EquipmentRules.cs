using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace DeadSurvivor
{
    // Shared deterministic rules for live equipment changes and cloned previews.
    public sealed class EquipmentRules
    {
        readonly PrototypeData data;
        readonly Dictionary<string, ItemDef> defs;
        public EquipmentRules(PrototypeData data)
        {
            this.data = data;
            defs = data.items.ToDictionary(d => d.id);
        }
        public ItemDef Definition(Gear gear) => defs[gear.type];
        public bool IsAction(Gear gear) => !string.IsNullOrEmpty(Definition(gear).attackMode);
        public IEnumerable<Gear> Actions(Survivor c) => c.items.Where(IsAction);
        public bool MatchesMode(ItemDef effect, Gear action)
        {
            if (!IsAction(action)) return false;
            string mode = Definition(action).attackMode;
            return string.IsNullOrEmpty(effect.targetMode) || (effect.targetMode == "attack" ? mode != "heal" && mode != "healArea" : effect.targetMode == mode);
        }
        bool Scoped(ItemDef d) => d.requiresAdjacent || !string.IsNullOrEmpty(d.targetMode);
        public bool BonusApplies(Survivor c, Gear source, Gear action)
        {
            var d = Definition(source);
            if (string.IsNullOrEmpty(d.bonusEffect)) return false;
            if (!Scoped(d)) return true;
            return action != null && source.id != action.id && MatchesMode(d, action) && (!d.requiresAdjacent || Adjacent(source, action));
        }
        public Vector2Int[] Shape(Gear item, int rotation = -1)
        {
            var points = Definition(item).shape.ToArray();
            int turns = rotation < 0 ? item.rotation : rotation;
            for (int n = 0; n < turns % 4; n++)
                points = points.Select(p => new Vector2Int(-p.y, p.x)).ToArray();
            int minX = points.Min(p => p.x), minY = points.Min(p => p.y);
            return points.Select(p => new Vector2Int(p.x - minX, p.y - minY)).ToArray();
        }
        public bool Adjacent(Gear a, Gear b) => a != b && Shape(a).Any(p => Shape(b).Any(q => Math.Abs(a.x + p.x - b.x - q.x) + Math.Abs(a.y + p.y - b.y - q.y) == 1));
        string BasicBonusState(Survivor c, Gear item)
        {
            var d = Definition(item);
            if (string.IsNullOrEmpty(d.bonusEffect)) return "추가 효과 없음";
            if (!BonusTargetMatches(c, d)) return "대상 공격 방식 없음";
            if (Scoped(d) && !Actions(c).Any(w => BonusApplies(c, item, w))) return "인접 필요";
            return "활성";
        }
        public bool BonusTargetMatches(Survivor c, ItemDef d) => !string.IsNullOrEmpty(d.bonusEffect) && (!Scoped(d) || Actions(c).Any(w => MatchesMode(d, w)));
        public string BonusState(Survivor c, Gear item)
        {
            string state = BasicBonusState(c, item);
            if (state != "활성") return state;
            var d = Definition(item);
            // Scoped maxima are resolved independently for every action, never globally suppressed.
            if (!Scoped(d) && new[] { "shield", "startShield" }.Contains(d.bonusEffect) && c.items.Any(o => o != item && Definition(o).bonusEffect == d.bonusEffect && BasicBonusState(c, o) == "활성" && BonusValue(o) > BonusValue(item)))
                return "더 강한 효과 적용 중";
            return state;
        }
        public float BonusValue(Gear i)
        {
            var d = Definition(i);
            return d.bonusEffect == "pierce" || d.bonusEffect == "extraShots" || d.bonusEffect == "chain" ? d.bonusValue : Value(i, d.bonusValue);
        }
        float Value(Gear i, float n) => n * data.grades[i.grade].mult;
        public CombatStats Stats(Survivor c, Gear action = null)
        {
            CombatStats s = new();
            foreach (var i in c.items)
            {
                var d = Definition(i);
                Apply(ref s, d.baseEffect, Value(i, d.baseValue));
                if (BonusApplies(c, i, action))
                    Apply(ref s, d.bonusEffect, BonusValue(i));
            }
            s.def = Mathf.Min(.8f, data.characters[c.index].def + s.def);
            s.haste = Mathf.Min(1, s.haste);
            s.hp += data.characters[c.index].hp;
            s.shield = Mathf.Max(s.shield, s.startShield);
            return s;
        }
        static void Apply(ref CombatStats s, string kind, float n)
        {
            switch (kind)
            {
                case "attack":
                    s.attack += n;
                    break;
                case "def":
                    s.def += n;
                    break;
                case "hp":
                    s.hp += n;
                    break;
                case "heal":
                    s.heal += n;
                    break;
                case "haste":
                    s.haste += n;
                    break;
                case "range":
                    s.range += n;
                    break;
                case "shield":
                    s.shield = Mathf.Max(s.shield, n);
                    break;
                case "startShield":
                    s.startShield = Mathf.Max(s.startShield, n);
                    break;
                case "radius":
                    s.radius += n;
                    break;
                case "pulse":
                    s.pulse += n;
                    break;
                case "pierce":
                    s.pierce = Mathf.Max(s.pierce, (int)n);
                    break;
                case "extraShots":
                    s.extraShots = Mathf.Max(s.extraShots, (int)n);
                    break;
                case "chain":
                    s.chain = Mathf.Max(s.chain, (int)n);
                    break;
            }
        }
        public float WeaponPower(Survivor c, Gear w)
        {
            if (w == null)
                return 0;
            var d = Definition(w);
            float value = d.damage * data.grades[w.grade].mult;
            return d.attackMode == "heal" || d.attackMode == "healArea" ? value : value * data.characters[c.index].atk * (1 + Stats(c, w).attack);
        }
        public int Provider(List<Survivor> team, int receiver)
        {
            int best = -1;
            float rate = 0;
            if (team[receiver].hp <= 0)
                return best;
            for (int n = 0; n < team.Count; n++)
            {
                var c = team[n];
                if (n == receiver || c.hp <= 0 || !c.items.Any(i => Definition(i).bonusEffect == "share" && BasicBonusState(c, i) == "활성"))
                    continue;
                var s = Stats(c);
                float next = s.heal / (3 / (1 + s.haste));
                if (next > rate)
                {
                    rate = next;
                    best = n;
                }
            }
            return best;
        }
        public bool MergeAllowed(Gear a, Gear b) => a != null && b != null && a.id != b.id && a.type == b.type && a.grade == b.grade && b.grade < data.grades.Length - 1;
        public bool Fits(Survivor c, Gear item, int x, int y, int rotation, Gear replace = null)
        {
            foreach (var p in Shape(item, rotation))
            {
                int xx = x + p.x, yy = y + p.y;
                if (xx < 1 || xx > 4 || yy < 1 || yy > 4 || !c.cells[yy * 6 + xx])
                    return false;
                if (c.items.Any(o => o.id != item.id && o != replace && Shape(o).Any(q => o.x + q.x == xx && o.y + q.y == yy)))
                    return false;
            }
            return true;
        }
        public bool Drop(List<Survivor> team, List<Gear> stash, int id, int ci, int x, int y, int rotation, out string reason)
        {
            reason = "";
            var item = stash.Concat(team.SelectMany(c => c.items)).FirstOrDefault(i => i.id == id);
            if (item == null || ci < 0 || ci >= team.Count)
            {
                reason = "대상 없음";
                return false;
            }
            var owner = team.FirstOrDefault(c => c.items.Contains(item));
            var target = team[ci];
            if (target.hp <= 0 || (owner != null && owner.hp <= 0))
            {
                reason = "사망자 장비 잠금";
                return false;
            }
            var under = target.items.FirstOrDefault(o => Shape(o).Any(q => o.x + q.x == x && o.y + q.y == y));
            if (under != item && MergeAllowed(item, under))
            {
                return Merge(team, stash, item.id, under.id, out reason);
            }
            if (!Fits(target, item, x, y, rotation))
            {
                reason = "경계 초과 또는 장비 겹침";
                return false;
            }
            Remove(team, stash, item);
            item.activationFlash = 0;
            item.x = x;
            item.y = y;
            item.rotation = rotation;
            target.items.Add(item);
            return true;
        }
        public bool Merge(List<Survivor> team, List<Gear> stash, int sourceId, int targetId, out string reason)
        {
            var items = stash.Concat(team.SelectMany(c => c.items));
            var source = items.FirstOrDefault(i => i.id == sourceId);
            var target = items.FirstOrDefault(i => i.id == targetId);
            if (!MergeAllowed(source, target))
            { reason = "동일 아이템·등급만 합성 가능 (최고 등급 제외)"; return false; }
            if (team.Any(c => c.hp <= 0 && (c.items.Contains(source) || c.items.Contains(target))))
            { reason = "사망자 장비 잠금"; return false; }
            Remove(team, stash, source);
            target.grade++;
            target.activationFlash = 0;
            reason = "";
            return true;
        }
        public bool Unequip(List<Survivor> team, List<Gear> stash, int id, out string reason)
        {
            var owner = team.FirstOrDefault(c => c.items.Any(i => i.id == id));
            if (owner == null || owner.hp <= 0)
            {
                reason = owner == null ? "장착한 장비만 해제할 수 있습니다" : "사망자 장비 잠금";
                return false;
            }
            var item = owner.items.First(i => i.id == id);
            Remove(team, stash, item);
            stash.Add(item);
            item.activationFlash = 0;
            reason = "";
            return true;
        }
        public static void Remove(List<Survivor> team, List<Gear> stash, Gear item)
        {
            stash.Remove(item);
            foreach (var c in team)
                c.items.Remove(item);
        }
        public static Gear Clone(Gear i) => new Gear { id = i.id, type = i.type, grade = i.grade, x = i.x, y = i.y, rotation = i.rotation, cd = i.cd, lastPeriod = i.lastPeriod, activationFlash = i.activationFlash };
        public static List<Survivor> CloneTeam(List<Survivor> team) => team.Select(c => new Survivor { index = c.index, hp = c.hp, maxhp = c.maxhp, pos = c.pos, cells = (bool[])c.cells.Clone(), items = c.items.Select(Clone).ToList(), shield = c.shield, healClock = c.healClock, shieldClock = c.shieldClock, shareClock = c.shareClock, shareProvider = c.shareProvider, sharePeriod = c.sharePeriod }).ToList();
    }
}
