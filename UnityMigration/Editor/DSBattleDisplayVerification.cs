using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace DeadSurvivor.Editor
{
    public static partial class DSVerification
    {
        static void BattleDisplayChecks(DSGame g)
        {
            Check(g.Data.settings.countScale == .49f, "Default population reduced another thirty percent");
            int[] expected = { 98, 132, 167, 201, 235, 270, 304, 338, 372, 407 };
            Check(g.Data.settings.waves.Select(w => Mathf.RoundToInt(w.count * g.Data.settings.countScale)).SequenceEqual(expected), "All ten waves use forty-nine percent population budgets");
            var c = ParallelFixture(g); var sword = AddAction(g, c, "sword"); var bow = AddAction(g, c, "shortbow");
            g.Simulate(0);
            Check(sword.activationFlash == 0 && bow.activationFlash == 0 && g.World.ItemCooldownRemaining(c, bow) == 0, "Ready without target never flashes as fired");
            var enemy = new Enemy { id = 99200, type = "armored", hp = 10000, maxhp = 10000, pos = c.pos + new Vector2(250, 0), phase = "test" }; g.Enemies.Add(enemy);
            g.Simulate(0);
            Check(sword.activationFlash == 0 && bow.activationFlash > 0 && Math.Abs(bow.lastPeriod - .7f) < .001f && g.World.ItemCooldownRemaining(c, bow) == 1, "Only actual ranged activation starts full cooldown and flash");
            g.Simulate(.1f);
            Check(bow.activationFlash > 0 && bow.activationFlash < .18f && g.World.ItemCooldownRemaining(c, bow) < 1 && g.World.ItemCooldownRemaining(c, bow) > 0, "Cooldown image follows actual independent timer");
            float clock = bow.cd, flash = bow.activationFlash; g.Paused = true; g.Simulate(1);
            Check(bow.cd == clock && bow.activationFlash == flash, "Pause freezes cooldown image and activation flash");
            g.Paused = false;
            var copy = EquipmentRules.Clone(bow);
            Check(copy.cd == bow.cd && copy.lastPeriod == bow.lastPeriod && copy.activationFlash == bow.activationFlash, "Preview clone preserves all cooldown presentation state");
            g.UI.Battle();
            Check(g.UI.BattleItemCount == 2, "Bottom HUD lists each equipped instance");
            Check(DSUI.BattleItemStatus(c, bow, true) == "발동!" && DSUI.BattleItemStatus(c, sword, true) == "준비", "Bottom HUD distinguishes actual firing and ready");
            Check(DSUI.BattleItemStatus(c, sword, false) == "지속 효과", "Bottom HUD labels passive items");
            string before = LiveEquipmentSnapshot(g); g.UI.RefreshBattleItems(); g.World.Render();
            Check(LiveEquipmentSnapshot(g) == before, "Rendering all equipped actions never mutates gameplay");
            var armor = g.MakeItem("chainmail"); c.items.Add(armor); g.World.Render();
            Check(!DSWorld.HasItemCooldown(g.Items[armor.type]), "Passive item appears without invented attack cooldown");
            c.hp = 0; g.World.Render();
            g.UI.RefreshBattleItems();
            Check(DSUI.BattleItemStatus(c, bow, true) == "비활성", "Bottom HUD marks dead owner inactive");
            g.State = "prep"; g.Revive(0);
            Check(bow.activationFlash == 0 && bow.lastPeriod == 0, "Revival clears stale activation presentation");
            g.World.Render();
            g.State = "battle"; bow.activationFlash = .18f; g.FinishWave(true); g.NextWave();
            Check(bow.activationFlash == 0, "Wave transition clears stale flash");
            g.State = "prep"; bow.activationFlash = .18f;
            Check(g.Unequip(bow.id) && bow.activationFlash == 0, "Unequip clears stale flash");

        }
    }
    public static class DSBattleDisplayVerification
    {
        public static string PrepareCapture(bool full = false)
        {
            var g = UnityEngine.Object.FindAnyObjectByType<DSGame>();
            g.Selected = new List<int> { 0, 2, 4, 5 }; g.StartRun();
            foreach (var c in g.Team)
            {
                c.items.Clear();
                if (full)
                    for (int n = 0; n < 16; n++) { var i = g.MakeItem("amulet"); i.x = n % 4 + 1; i.y = n / 4 + 1; c.items.Add(i); }
                else
                {
                    foreach (string id in new[] { "sword", "shortbow", "orb", "amulet" })
                    {
                        var i = g.MakeItem(id); bool placed = false;
                        for (int y = 1; y <= 4 && !placed; y++) for (int x = 1; x <= 4 && !placed; x++)
                            if (g.Equipment.Fits(c, i, x, y, 0)) { i.x = x; i.y = y; c.items.Add(i); placed = true; }
                        if (!placed) throw new Exception("Capture fixture cannot fit " + id);
                    }
                }
            }
            g.RefreshEquipment(); g.Enemies.Clear(); g.Spawned = 999999;
            if (!full)
            {
                g.Enemies.Add(new Enemy { id = 99300, type = "armored", hp = 10000, maxhp = 10000, pos = g.Team[0].pos + new Vector2(-70, 0), phase = "test" });
                foreach (var c in g.Team) foreach (var i in c.items) { i.cd = 0; i.activationFlash = 0; }
                g.Simulate(.05f); g.Simulate(.08f);
                // Different phases demonstrate independent clocks; actual first activations occurred above.
                g.Team[0].items.First(i => i.type == "shortbow").cd = .35f;
            }
            g.Paused = true; g.UI.Battle(); g.World.Render();
            if (g.UI.BattleItemCount != g.Team.Sum(c => c.items.Count)) throw new Exception("Bottom HUD omitted equipped items");
            return "PASS owned paused capture fixture; icons=" + g.UI.BattleItemCount;
        }
        public static string CheckBottomInput()
        {
            Canvas.ForceUpdateCanvases();
            var panel = GameObject.Find("TeamHUD").GetComponent<RectTransform>();
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = (corners[0] + corners[2]) / 2 };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || hits[0].gameObject.name != "MovementInput") throw new Exception("Bottom inventory input hits: " + string.Join(",", hits.Select(h => h.gameObject.name)) + " at " + pointer.position);
            return "PASS bottom inventory forwards movement input";
        }
    }
}
