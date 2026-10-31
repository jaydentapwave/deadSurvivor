using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSGame
    {
        void Heal(Survivor c, float amount)
        {
            if (c.hp > 0)
            {
                float oldHP = c.hp;
                c.hp = Mathf.Min(c.maxhp, c.hp + amount);
                c.healingReceived += c.hp - oldHP;
            }
        }
        void TickEquipment(Survivor c, float dt)
        {
            NoteMovementFixtureCombat();
            foreach (var item in c.items) item.activationFlash = Mathf.Max(0, item.activationFlash - dt);
            var s = Stats(c);
            float healPeriod = 3 / (1 + s.haste);
            c.healClock += dt;
            while (c.healClock + 1e-4f >= healPeriod)
            {
                c.healClock -= healPeriod;
                Heal(c, s.heal);
            }
            if (c.shareProvider >= 0)
            {
                c.shareClock += dt;
                while (c.shareClock + 1e-4f >= c.sharePeriod)
                {
                    c.shareClock -= c.sharePeriod;
                    Heal(c, Stats(Team[c.shareProvider]).heal);
                }
            }
            float periodic = 0;
            foreach (var item in c.items)
            {
                var d = Items[item.type];
                if (d.bonusEffect == "shield" && Equipment.BonusState(c, item) == "활성")
                    periodic = Mathf.Max(periodic, d.bonusValue * Data.grades[item.grade].mult);
                if (d.bonusEffect == "pulse" && Equipment.BonusState(c, item) == "활성")
                {
                    item.cd -= dt;
                    if (item.cd <= 0)
                    {
                        item.cd = item.lastPeriod = d.effectInterval;
                        item.activationFlash = .18f;
                        Burst(c.pos, d.effectRadius, .4f, "#a586dc");
                        foreach (var e in Enemies)
                        if (e.hp > 0 && Vector2.Distance(c.pos, e.pos) <= d.effectRadius)
                            Hit(e, d.bonusValue * Data.grades[item.grade].mult, "pulse", Vector2.zero);
                    }
                }
            }
            c.shield = Mathf.Min(c.shield, s.shield);
            if (periodic > 0)
            {
                c.shieldClock += dt;
                if (c.shieldClock >= 8)
                {
                    c.shieldClock %= 8;
                    c.shield = Mathf.Max(c.shield, periodic);
                }
            }
            else
                c.shieldClock = 0;
            foreach (var weapon in Equipment.Actions(c)) TickAction(c, weapon, dt);
        }
        void TickAction(Survivor c, Gear weapon, float dt)
        {
            var s = Equipment.Stats(c, weapon);
            var w = Items[weapon.type];
            weapon.cd -= dt;
            if (weapon.cd > 0)
                return;
            float range = w.range * 42 * (1 + s.range), power = Equipment.WeaponPower(c, weapon);
            if (w.attackMode == "heal" || w.attackMode == "healArea")
            {
                var targets = Team.Where(t => t.hp > 0 && t.hp < t.maxhp && Vector2.Distance(t.pos, c.pos) <= range).OrderBy(t => t.hp / t.maxhp).ToArray();
                if (targets.Length == 0)
                    return;
                if (w.attackMode == "heal")
                    Heal(targets[0], power);
                else
                foreach (var t in targets)
                    Heal(t, power);
                Burst(c.pos, w.attackMode == "heal" ? 30 : range, .3f, "#e3d891");
                weapon.cd = weapon.lastPeriod = w.interval / (1 + s.haste);
                weapon.activationFlash = .18f;
                return;
            }
            var target = Enemies.Where(e => e.hp > 0 && Vector2.Distance(c.pos, e.pos) <= range).OrderBy(e => (e.pos - c.pos).sqrMagnitude).FirstOrDefault();
            if (target == null)
                return;
            weapon.cd = weapon.lastPeriod = w.interval / (1 + s.haste);
                weapon.activationFlash = .18f;
            Vector2 direction = (target.pos - c.pos).normalized;
            if (w.attackMode == "melee" || w.attackMode == "sweep")
            {
                float angle = w.attackMode == "melee" ? 50 : 90;
                foreach (var e in Enemies)
                if (e.hp > 0 && Vector2.Distance(c.pos, e.pos) <= range && Vector2.Angle(direction, e.pos - c.pos) <= angle)
                    Hit(e, power, "melee", direction);
                Burst(c.pos, range, .25f, "#d9ad83");
                return;
            }
            if (w.attackMode == "chain")
            {
                var visited = new HashSet<int>();
                var next = target;
                Vector2 origin = c.pos;
                for (int n = 0; n < 3 + s.chain && next != null; n++)
                {
                    visited.Add(next.id);
                    Hit(next, power, "chain", (next.pos - origin).normalized);
                    Burst(next.pos, 25, .25f, "#b9a9fa");
                    origin = next.pos;
                    next = Enemies.Where(e => e.hp > 0 && !visited.Contains(e.id) && Vector2.Distance(e.pos, origin) <= 150).OrderBy(e => (e.pos - origin).sqrMagnitude).FirstOrDefault();
                }
                return;
            }
            for (int n = 0; n < 1 + s.extraShots; n++)
            {
                float a = (n - s.extraShots * .5f) * .12f;
                Vector2 dir = new Vector2(direction.x * Mathf.Cos(a) - direction.y * Mathf.Sin(a), direction.x * Mathf.Sin(a) + direction.y * Mathf.Cos(a));
                Shots.Add(new Shot { pos = c.pos, dir = dir, speed = 440, type = weapon.type, damage = power, range = range, owner = c, target = target.pos, pierce = s.pierce, radius = 75 * (1 + s.radius) });
            }
        }
    }
}
