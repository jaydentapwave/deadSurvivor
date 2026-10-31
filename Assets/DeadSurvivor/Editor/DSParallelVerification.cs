using System;
using System.Linq;
using UnityEngine;
namespace DeadSurvivor.Editor
{
    public static partial class DSVerification
    {
        static Survivor ParallelFixture(DSGame g)
        {
            Reset(g); g.State = "battle"; g.Spawned = 999999; g.Enemies.Clear(); g.Shots.Clear();
            foreach (var member in g.Team) { member.items.Clear(); member.hp = member.maxhp; }
            g.RefreshEquipment();
            return g.Team[0];
        }
        static Gear AddAction(DSGame g, Survivor c, string type, float cooldown = 0)
        {
            var i = g.MakeItem(type); i.cd = cooldown; c.items.Add(i); return i;
        }
        static void ParallelChecks(DSGame g)
        {
            var r = g.Equipment;
            var c = ParallelFixture(g);
            var sword = AddAction(g, c, "sword"); var bow = AddAction(g, c, "shortbow");
            var enemy = new Enemy { id = 99000, type = "armored", hp = 10000, maxhp = 10000, pos = c.pos + new Vector2(250, 0), phase = "test" };
            g.Enemies.Add(enemy); g.Simulate(0);
            Check(sword.cd <= 0 && bow.cd > 0 && g.Shots.Count == 1, "Out of range first action cannot block later ranged action");
            float bowClock = bow.cd; enemy.pos = c.pos + new Vector2(60, 0); float hp = enemy.hp;
            g.Simulate(0);
            Check(enemy.hp < hp && sword.cd > 0 && bow.cd == bowClock && g.Shots.Count == 1, "Each action owns its cooldown and fire decision");
            sword.cd = 5; bow.cd = 0; g.Shots.Clear(); g.Simulate(0);
            Check(sword.cd == 5 && g.Shots.Count == 1, "Cooling first action does not block ready second action");
            c = ParallelFixture(g);
            var heal = AddAction(g, c, "relic"); bow = AddAction(g, c, "shortbow");
            enemy.pos = c.pos + new Vector2(250, 0); g.Enemies.Add(enemy); g.Simulate(0);
            Check(heal.cd <= 0 && bow.cd > 0 && g.Shots.Count == 1, "No healing target does not block attack");
            c.hp -= 30; bow.cd = 0; hp = c.hp; g.Simulate(0);
            Check(c.hp > hp && heal.cd > 0 && bow.cd > 0 && g.Shots.Count == 2, "Healing and attack both activate in same tick");
            c = ParallelFixture(g); var first = AddAction(g, c, "shortbow"); var second = AddAction(g, c, "shortbow");
            enemy.pos = c.pos + new Vector2(200, 0); g.Enemies.Add(enemy); g.Simulate(0);
            Check(first.id != second.id && first.cd > 0 && second.cd > 0 && g.Shots.Count == 2, "Same type action instances both fire");
            first.cd = .2f; second.cd = .6f; g.Simulate(.1f);
            Check(Math.Abs(first.cd - .1f) < .001f && Math.Abs(second.cd - .5f) < .001f, "Different cooldown progress is retained");
            g.State = "prep"; float savedFirst = first.cd, savedSecond = second.cd; g.Simulate(1);
            Check(first.cd == savedFirst && second.cd == savedSecond, "Prep freezes all action cooldowns");
            Check(g.Unequip(first.id), "Parallel action can be unequipped");
            g.State = "battle"; g.Shots.Clear(); second.cd = 0; g.Simulate(0);
            Check(first.cd == savedFirst && g.Shots.Count == 1, "Unequipped action stops while remaining action fires");
            c.hp = 0; second.cd = .7f; g.Simulate(.1f);
            Check(second.cd == .7f, "Dead owner cannot advance action cooldown");
            g.State = "prep"; g.Revive(0);
            Check(second.cd == 0 && !c.items.Contains(first), "Revival resets equipped action only");

            c = ParallelFixture(g); c.items.Clear();
            first = AddAction(g, c, "shortbow"); first.x = 1; first.y = 1;
            second = AddAction(g, c, "shortbow"); second.x = 1; second.y = 4;
            var speed = g.MakeItem("quiver"); speed.x = 1; speed.y = 2; c.items.Add(speed);
            Check(r.BonusApplies(c, speed, first) && !r.BonusApplies(c, speed, second), "Adjacency targets each action separately");
            Check(Math.Abs(r.Stats(c, first).haste - .2f) < .001f && Math.Abs(r.Stats(c, second).haste - .05f) < .001f && Math.Abs(r.Stats(c).haste - .05f) < .001f, "Scoped haste cannot leak to other action or self healing");
            var share = g.MakeItem("amulet"); share.x = 4; share.y = 4; c.items.Add(share); g.RefreshEquipment();
            Check(Math.Abs(g.Team[1].sharePeriod - 3 / 1.05f) < .001f, "Scoped haste cannot leak into shared healing period");
            var pierceNear = g.MakeItem("pierce"); pierceNear.x = 3; pierceNear.y = 2; c.items.Add(pierceNear);
            Check(r.Stats(c, first).pierce == 1 && r.Stats(c, second).pierce == 0, "Special effect maxima resolve per action");
            string snapshot = LiveEquipmentSnapshot(g);
            var clone = EquipmentRules.CloneTeam(g.Team);
            Check(clone[0].items.Count(i => i.type == "shortbow") == 2 && clone[0].items[0].id != clone[0].items[1].id && LiveEquipmentSnapshot(g) == snapshot, "Clone keeps same type action identities without mutation");
            c.items.Remove(speed); c.items.Remove(pierceNear); g.RefreshEquipment();
            Check(r.Stats(c, first).haste == 0 && r.Stats(c, first).pierce == 0, "Removed effect no longer applies to action");
            g.State = "prep"; c.items.Clear(); g.NextWave();
            Check(g.State == "battle", "Empty equipment inventory may start next wave");
        }
    }
}
