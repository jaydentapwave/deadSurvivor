using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace DeadSurvivor.Editor
{
    public static partial class DSVerification
    {
        static List<string> passed = new();
        static void Check(bool value, string label)
        {
            if (!value)
                throw new Exception("FAILED: " + label);
            passed.Add(label);
        }
        public static string Run()
        {
            var g = UnityEngine.Object.FindAnyObjectByType<DSGame>();
            if (g == null)
                throw new Exception("DSGame missing");
            var cfg = JsonUtility.ToJson(g.Config);
            var rng = g.Rng; var lootRng = g.LootRng;
            bool priorVerification = g.VerificationRun;
            g.VerificationRun = true;
            try
            {
                return RunChecks(g);
            }
            finally { g.Config = JsonUtility.FromJson<RunSettings>(cfg); g.Rng = rng; g.LootRng = lootRng; g.VerificationRun = priorVerification; g.MoveInput = Vector2.zero; g.ReturnLobby(); g.Selected.Clear(); g.UI.Lobby(); }
        }
        static void Reset(DSGame g)
        {
            g.Selected = new List<int> { 0, 2, 4, 5 };
            g.StartRun();
            g.FinishWave(true);
        }
        static string EquipmentSnapshot(List<Survivor> team, List<Gear> stash) =>
            string.Join("|", team.Select(c => string.Join(";", c.items.OrderBy(i => i.id).Select(i => JsonUtility.ToJson(i))))) + "#" + string.Join(";", stash.OrderBy(i => i.id).Select(i => JsonUtility.ToJson(i)));
        static string LiveEquipmentSnapshot(DSGame g) =>
            string.Join("|", g.Team.Select(c => JsonUtility.ToJson(c) + ":" + string.Join(",", new[] { c.pos.x, c.pos.y, c.healClock, c.shieldClock, c.flash, c.shareClock }.Select(v => BitConverter.SingleToInt32Bits(v)))))
            + "#" + EquipmentSnapshot(g.Team, g.Stash) + "#" + string.Join(",", g.Team.SelectMany(c => c.items).Concat(g.Stash).Select(i => i.id + ":" + BitConverter.SingleToInt32Bits(i.cd) + ":" + BitConverter.SingleToInt32Bits(i.lastPeriod) + ":" + BitConverter.SingleToInt32Bits(i.activationFlash)));
        static void CheckPreviewTransaction(DSGame g, string label, int id, int ci, int x = 1, int y = 1, int rotation = 0)
        {
            var r = g.Equipment;
            var team = EquipmentRules.CloneTeam(g.Team);
            var stash = g.Stash.Select(EquipmentRules.Clone).ToList();
            string original = LiveEquipmentSnapshot(g);
            bool predicted = ci == -2 ? r.Merge(team, stash, id, x, out _) : ci < 0 ? r.Unequip(team, stash, id, out _) : r.Drop(team, stash, id, ci, x, y, rotation, out _);
            Check(predicted && LiveEquipmentSnapshot(g) == original, label + " preview preserves live HP/shield/timers/cooldowns/items");
            bool applied = ci == -2 ? g.Merge(id, x) : ci < 0 ? g.Unequip(id) : g.PlaceRotated(id, ci, x, y, rotation);
            Check(applied && EquipmentSnapshot(team, stash) == EquipmentSnapshot(g.Team, g.Stash), label + " preview/apply item state identical");
            Check(Enumerable.Range(0, team.Count).All(n => r.Stats(team[n]).Equals(g.Stats(g.Team[n])) && r.Provider(team, n) == r.Provider(g.Team, n) && r.Actions(team[n]).All(w => r.WeaponPower(team[n], w) == r.WeaponPower(g.Team[n], g.Team[n].items.First(i => i.id == w.id)) && r.Stats(team[n], w).Equals(r.Stats(g.Team[n], g.Team[n].items.First(i => i.id == w.id))))), label + " full party stats/power/provider identical");
        }
        static void PreviewTransactionChecks(DSGame g)
        {
            Reset(g);
            foreach (var survivor in g.Team) for (int y = 1; y <= 4; y++) for (int x = 1; x <= 4; x++) survivor.cells[y * 6 + x] = true;
            g.Team[0].items.Clear();
            var wand = g.MakeItem("wand"); g.Stash.Add(wand);
            CheckPreviewTransaction(g, "Action placement", wand.id, 0);
            var crystal = g.MakeItem("crystal"); crystal.x = 4; crystal.y = 4; g.Team[0].items.Add(crystal); g.RefreshEquipment();
            CheckPreviewTransaction(g, "Adjacency move", crystal.id, 0, 1, 2);
            Check(g.Equipment.BonusState(g.Team[0], crystal) == "활성", "Adjacency preview fixture activates class effect");
            CheckPreviewTransaction(g, "Rotation", wand.id, 0, 3, 1, 1);
            Check(g.Equipment.BonusState(g.Team[0], crystal) == "인접 필요", "Rotation fixture deactivates previous adjacency");
            var duplicate = g.MakeItem("crystal"); g.Stash.Add(duplicate);
            CheckPreviewTransaction(g, "Merge", duplicate.id, 0, crystal.x, crystal.y);
            Check(crystal.grade == 1 && g.Find(duplicate.id) == null, "Merge preview fixture upgrades target and consumes source");
            var amulet = g.MakeItem("amulet"); amulet.x = 4; amulet.y = 4; g.Team[2].items.Add(amulet); g.RefreshEquipment();
            Check(g.Equipment.Provider(g.Team, 0) == 2, "Cross character fixture starts with active priest share");
            CheckPreviewTransaction(g, "Cross character move", amulet.id, 0, 4, 4);
            Check(g.Equipment.Provider(g.Team, 1) == 0, "Cross character fixture transfers share provider");
            CheckPreviewTransaction(g, "Unequip", wand.id, -1);
            Check(!g.Equipment.Actions(g.Team[0]).Any(), "Action unequip preview removes action");
            string original = EquipmentSnapshot(g.Team, g.Stash);
            Check(!g.Equipment.Unequip(g.Team, g.Stash, wand.id, out _) && EquipmentSnapshot(g.Team, g.Stash) == original, "Already stashed unequip is atomic rejection");
            var mergeTarget = g.MakeItem("amulet"); g.Stash.Add(mergeTarget);
            CheckPreviewTransaction(g, "Equipped source to stash merge", amulet.id, -2, mergeTarget.id);
            Check(g.Find(amulet.id) == null && g.Stats(g.Team[0]).heal == 0 && mergeTarget.grade == 1, "Stash merge removes equipped source effects");
            var stashSource = g.MakeItem("amulet"); stashSource.grade = 1; g.Stash.Add(stashSource);
            CheckPreviewTransaction(g, "Stash to stash merge", stashSource.id, -2, mergeTarget.id);
            Check(mergeTarget.grade == 2 && g.Find(stashSource.id) == null, "Stash merge retains upgraded target");
            var invalidSource = g.MakeItem("amulet"); g.Stash.Add(invalidSource); original = LiveEquipmentSnapshot(g);
            Check(!g.Equipment.Merge(g.Team, g.Stash, invalidSource.id, mergeTarget.id, out _) && LiveEquipmentSnapshot(g) == original, "Invalid stash merge preserves all live state");
        }
        static string RunChecks(DSGame g)
        {
            passed.Clear();
            Check(g.InitialLoadTime >= 0 && float.IsFinite(g.InitialLoadTime), "First UI load measured with monotonic time");
            float firstLoad = g.InitialLoadTime;
            g.Rng = new System.Random(42);
            Reset(g);
            var r = g.Equipment;
            Check(g.Data.items.Length == 32, "32 item definitions");
            Check(g.Data.items.Count(i => !string.IsNullOrEmpty(i.attackMode)) == 8, "8 independently triggered item types");
            Check(g.Data.items.All(d => string.IsNullOrEmpty(d.attackMode) || d.bonusEffect != "pulse"), "Current items never share action and pulse cooldown");
            foreach (var d in g.Data.items)
            {
                var gear = g.MakeItem(d.id);
                var original = r.Shape(gear);
                for (int rot = 0; rot < 4; rot++)
                {
                    var shape = r.Shape(gear, rot);
                    Check(shape.Length == d.shape.Length && shape.Distinct().Count() == shape.Length && shape.All(p => p.x >= 0 && p.y >= 0), d.id + " rotation " + rot);
                    var seen = new HashSet<Vector2Int> { shape[0] };
                    bool grow = true;
                    while (grow)
                    {
                        grow = false;
                        foreach (var p in shape)
                        if (!seen.Contains(p) && seen.Any(q => Math.Abs(p.x - q.x) + Math.Abs(p.y - q.y) == 1))
                        {
                            seen.Add(p);
                            grow = true;
                        }
                    }
                    Check(seen.Count == shape.Length, d.id + " connected " + rot);
                }
                Check(r.Shape(gear, 4).SequenceEqual(original), d.id + " full rotation");
            }
            var c = g.Team[0];
            var sword = r.Actions(c).First();
            var axe = g.MakeItem("axe"); g.Stash.Add(axe);
            string before = LiveEquipmentSnapshot(g);
            Check(!g.Place(axe.id, 0, sword.x + 1, sword.y) && LiveEquipmentSnapshot(g) == before, "Overlapping action rejects without auto replacement");
            Check(g.Place(axe.id, 0, 1, 2) && c.items.Contains(sword) && c.items.Contains(axe), "Two action items coexist");
            var wand = g.MakeItem("wand"); g.Stash.Add(wand);
            before = LiveEquipmentSnapshot(g);
            Check(!g.PlaceRotated(wand.id, 0, 4, 4, 1) && LiveEquipmentSnapshot(g) == before, "Failed placement atomic");
            Check(g.Unequip(axe.id) && c.items.Contains(sword), "Unequip removes only selected action");
            Check(g.Unequip(sword.id), "Last action may be unequipped");
            int wave = g.Wave; g.NextWave();
            Check(g.Wave == wave + 1 && g.State == "battle", "No action departure allowed");
            g.FinishWave(true); c = g.Team[0]; g.Stash.Add(wand);
            Check(g.Place(wand.id, 0, 1, 1), "Action placement without class change");
            var crystal = g.MakeItem("crystal"); g.Stash.Add(crystal);
            Check(g.Place(crystal.id, 0, 1, 2), "Adjacent effect placement");
            Check(r.BonusState(c, crystal) == "활성", "Adjacent bonus active");
            Check(g.Place(crystal.id, 0, 4, 4) && r.BonusState(c, crystal) == "인접 필요", "Diagonal/far adjacency excluded");
            var wrong = g.MakeItem("whetstone"); g.Stash.Add(wrong); g.Place(wrong.id, 0, 1, 2);
            Check(r.BonusState(c, wrong) == "활성" && r.Stats(c, wand).attack > r.Stats(c).attack, "Matching attack effect works without class gate");
            Reset(g);
            c = g.Team[0];
            var priest = g.Team[2];
            var heal = g.MakeItem("amulet");
            c.items.Add(heal);
            g.RefreshEquipment();
            Check(r.Provider(g.Team, 1) == 0, "Share item works without character class");
            c.items.Remove(heal);
            priest.items.Add(heal);
            g.RefreshEquipment();
            Check(r.Provider(g.Team, 0) == 2 && r.Provider(g.Team, 2) == -1, "Priest shares to others, self excluded");
            g.Team[3].items.Clear();
            g.Team[3].items.Add(g.MakeItem("censer"));
            var bigger = g.MakeItem("rosary");
            g.Team[3].items.Add(bigger);
            g.RefreshEquipment();
            Check(r.Provider(g.Team, 0) == 3, "Strongest priest provider");
            g.Team[3].hp = 0;
            g.RefreshEquipment();
            Check(r.Provider(g.Team, 0) == 2, "Dead priest stops share");
            priest.pos = new Vector2(9999, 9999);
            Check(r.Provider(g.Team, 0) == 2, "Party share has no distance limit");
            c.hp = 0;
            g.RefreshEquipment();
            Check(r.Provider(g.Team, 0) == -1 && !g.Unequip(r.Actions(c).First().id), "Dead receiver and equipment locked");
            g.Revive(0);
            Check(c.hp == c.maxhp, "Free revival restores current maximum");
            c.hp = 30;
            var armor = g.MakeItem("chainmail");
            c.items.Add(armor);
            g.RefreshEquipment();
            Check(c.hp == 30 && c.maxhp > g.Def(c).hp, "Health gear cannot grant free healing");
            c.items.Remove(armor);
            g.RefreshEquipment();
            Check(c.hp == 30, "Unequip preserves bounded current health");
            Reset(g);
            c = g.Team[0];
            sword = r.Actions(c).First();
            var duplicate = g.MakeItem(sword.type);
            g.Stash.Add(duplicate);
            int x = sword.x, y = sword.y, rotation = sword.rotation;
            Check(g.Merge(duplicate.id, sword.id) && sword.grade == 1 && sword.x == x && sword.y == y && sword.rotation == rotation, "Merge preserves target placement");
            sword.grade = g.Data.grades.Length - 1;
            duplicate = g.MakeItem(sword.type);
            duplicate.grade = sword.grade;
            g.Stash.Add(duplicate);
            Check(!g.Merge(duplicate.id, sword.id), "Legendary merge prohibited");
            Reset(g);
            var originalConfig = JsonUtility.ToJson(g.Config);
            g.Config.duration = 5;
            g.Config.countScale = .1f;
            foreach (var w in g.Config.waves)
            {
                w.count = 10;
                w.damage = .00001f;
            }
            g.StartRun();
            for (int n = 1; n <= 10; n++)
            {
                for (int k = 0; k < 2500 && g.State == "battle"; k++)
                {
                    g.MoveInput = new Vector2(1, .3f).normalized;
                    g.Simulate(.05f);
                }
                Check(g.State == (n == 10 ? "result" : "prep"), "Wave transition " + n);
                if (n < 10)
                    g.NextWave();
            }
            Check(g.Win && g.History.Count == 10, "Ten-wave victory");
            g.Config = JsonUtility.FromJson<RunSettings>(originalConfig);
            // Behavioral checks exercise timed effects and each primary attack mode.
            Reset(g);
            g.State = "battle";
            g.Spawned = 999999;
            g.Elapsed = 0;
            g.Enemies.Clear();
            g.Enemies.Add(new Enemy { id = 90000, type = "armored", hp = 999999, maxhp = 999999, pos = new Vector2(9000, 9000), phase = "test" });
            foreach (var member in g.Team)
            {
                member.items.Clear();
                member.hp = 50;
                member.healClock = member.shareClock = 0;
            }
            g.Team[0].items.Add(g.MakeItem("sword"));
            g.Team[1].items.Add(g.MakeItem("shortbow"));
            g.Team[2].items.Add(g.MakeItem("relic"));
            g.Team[3].items.Add(g.MakeItem("wand"));
            g.Team[2].items.Add(g.MakeItem("amulet"));
            g.RefreshEquipment();
            foreach (var member in g.Team)
            foreach (var gear in member.items)
                gear.cd = 999;
            for (int k = 0; k < 60; k++)
                g.Simulate(.05f);
            Check(Math.Abs(g.Team[0].hp - 51) < .01 && Math.Abs(g.Team[2].hp - 51) < .01, "Timed share and self healing each once");
            g.State = "prep";
            float hp = g.Team[0].hp, clock = g.Team[0].shareClock;
            g.Simulate(20);
            Check(g.Team[0].hp == hp && g.Team[0].shareClock == clock, "Prep freezes healing timers");
            g.Team[2].hp = 0;
            g.RefreshEquipment();
            g.State = "battle";
            for (int k = 0; k < 60; k++)
                g.Simulate(.05f);
            Check(g.Team[0].hp == hp && g.Team[2].hp == 0, "Dead provider stops actual healing without revival");
            Reset(g);
            c = g.Team[0];
            c.items.Clear();
            c.items.Add(g.MakeItem("sword"));
            c.items.Add(g.MakeItem("banner"));
            g.State = "battle";
            g.Spawned = 999999;
            g.Enemies.Clear();
            g.Enemies.Add(new Enemy { id = 90001, type = "armored", hp = 999999, maxhp = 999999, pos = new Vector2(9000, 9000), phase = "test" });
            for (int k = 0; k < 161; k++)
                g.Simulate(.05f);
            Check(Math.Abs(c.shield - 20) < .01, "Periodic shield refills to cap");
            for (int k = 0; k < 161; k++)
                g.Simulate(.05f);
            Check(Math.Abs(c.shield - 20) < .01, "Shield does not accumulate");
            foreach (var def in g.Data.items.Where(i => !string.IsNullOrEmpty(i.attackMode)))
            {
                Reset(g);
                c = g.Team[0];
                c.items.Clear();
                var weapon = g.MakeItem(def.id);
                weapon.cd = 0;
                c.items.Add(weapon);
                g.RefreshEquipment();
                g.State = "battle";
                g.Spawned = 999999;
                g.Enemies.Clear();
                var enemy = new Enemy { id = 91000, type = "armored", hp = 9999, maxhp = 9999, pos = c.pos + new Vector2(60, 0), phase = "test" };
                g.Enemies.Add(enemy);
                foreach (var other in g.Team.Skip(1))
                    other.items.Clear();
                foreach (var member in g.Team)
                    member.hp = member.maxhp - 30;
                float initial = g.Team.Sum(t => t.hp);
                g.Simulate(.05f);
                if (def.attackMode == "heal" || def.attackMode == "healArea")
                    Check(g.Team.Sum(t => t.hp) > initial, def.id + " direct healing fires");
                else
                {
                    for (int k = 0; k < 20; k++)
                        g.Simulate(.05f);
                    Check(enemy.hp < 9999, def.id + " attack hits");
                }
            }
            Check(Math.Abs(g.Data.settings.waves.Sum(w => w.duration) - 435) < .01, "Default battle duration 435 seconds");
            Check(g.Data.settings.drops == 0 && g.Data.settings.waves.All(w => w.drops == 0) && g.UI.ValidSettings(g.Data.settings), "Default reward settings enable random count");
            for (int size = 1; size <= 4; size++)
            for (int formation = 0; formation < 3; formation++)
            {
                var offsets = Enumerable.Range(0, size).Select(i => DSGame.FormationOffset(formation, i, size)).ToArray();
                Check(offsets.Distinct().Count() == size, "Unique formation slots " + size + "/" + formation);
                Check(offsets.Aggregate(Vector2.zero, (a, b) => a + b).sqrMagnitude < .001f, "Centered formation " + size + "/" + formation);
            }
            Reset(g);
            int oldFormation = g.Formation;
            g.SelectFormation(1);
            Check(g.Formation == oldFormation && g.PendingFormation == 1, "Formation waits for next wave");
            g.NextWave();
            Check(g.Formation == 1, "Formation applied on next wave");
            g.StartRun();
            g.Enemies.Clear();
            g.Spawned = 999999;
            g.Simulate(.01f);
            Check(g.State == "battle", "Empty battlefield does not end wave early");
            foreach (int finalWave in new[] { 1, 10 })
            {
                g.StartRun();
                g.Wave = finalWave;
                g.Elapsed = g.Duration - .01f;
                foreach (var member in g.Team)
                    member.hp = 0;
                g.Simulate(.05f);
                Check(g.State == "result" && !g.Win && g.Stash.Count == 0, "Total defeat precedes deadline at wave " + finalWave);
            }
            g.StartRun();
            g.Spawned = 999999;
            g.Enemies.Clear();
            g.Elapsed = g.Duration - .01f;
            g.Simulate(.05f);
            Check(g.State == "prep" && Math.Abs(g.History[0].time - g.Config.waves[0].duration * g.Config.duration / 30) < .001, "Deadline clamps final dt");
            g.StartRun();
            g.Wave = 10;
            g.FinishWave(true);
            Check(g.State == "result" && g.Win && g.History.Count == 1 && g.History[0].skipped, "Final wave skip goes to result");
            g.StartRun();
            g.Elapsed = 2;
            g.WaveReal = 3;
            g.WavePaused = 1;
            foreach (var member in g.Team)
                member.hp = 0;
            g.Simulate(0);
            g.Result(false);
            Check(g.History.Count == 1 && g.History[0].defeated && g.History[0].time == 2 && g.History[0].realBattle == 3 && g.History[0].pausedTime == 1, "Defeat history finalized exactly once");
            g.Config = JsonUtility.FromJson<RunSettings>(JsonUtility.ToJson(g.Data.settings));
            g.StartRun();
            int rewards = 0;
            var rewardCounts = new HashSet<int>();
            for (int n = 1; n <= 10; n++)
            {
                g.FinishWave(true);
                if (n < 10)
                {
                    Check(g.Stash.Count >= 3 && g.Stash.Count <= 5 && g.Stash.All(i => g.Items.ContainsKey(i.type)), "Random reward count and valid items wave " + n);
                    rewardCounts.Add(g.Stash.Count);
                    rewards += g.Stash.Count;
                    int awarded = g.Stash.Count;
                    g.FinishWave(true);
                    Check(g.Stash.Count == awarded, "Repeated finish does not duplicate rewards wave " + n);
                    g.NextWave();
                }
            }
            Check(rewards >= 27 && rewards <= 45 && g.State == "result" && g.Stash.Count == 0, "Random nine reward batches and no final-wave loot");
            // Exercise independent seeds through the actual reward path, including both bounds.
            for (int seed = 0; seed < 30; seed++)
            {
                g.StartRun(); g.LootRng = new System.Random(seed); g.FinishWave(true);
                Check(g.Stash.Count >= 3 && g.Stash.Count <= 5, "Seeded reward bounds " + seed);
                rewardCounts.Add(g.Stash.Count);
            }
            Check(rewardCounts.SetEquals(new[] { 3, 4, 5 }), "All three reward counts are reachable");
            g.StartRun(); g.LootRng = new System.Random(771); g.FinishWave(true);
            string seededLoot = string.Join("|", g.Stash.Select(i => i.type));
            g.StartRun(); g.LootRng = new System.Random(771); g.FinishWave(true);
            Check(string.Join("|", g.Stash.Select(i => i.type)) == seededLoot, "Same seed reproduces reward count and items");
            g.Config.waves[0].drops = 5; g.StartRun(); g.FinishWave(true);
            Check(g.Stash.Count == 5, "Explicit wave reward count preserved");
            g.Config.drops = 7; g.StartRun(); g.FinishWave(true);
            Check(g.Stash.Count == 7, "Global test reward override takes precedence");
            g.Config = JsonUtility.FromJson<RunSettings>(JsonUtility.ToJson(g.Data.settings));
            Reset(g);
            g.SelectFormation(2);
            g.NextWave();
            var position = g.Offset(1);
            g.Team[1].hp = 0;
            g.FinishWave(true);
            g.Revive(1);
            Check(g.Team[1].pos == g.Center + position, "Revival preserves formation slot");
            var invalid = JsonUtility.FromJson<RunSettings>(JsonUtility.ToJson(g.Data.settings));
            invalid.waves[0].duration = 0;
            Check(!g.UI.ValidSettings(invalid), "Zero wave duration rejected");
            invalid.waves[0].duration = 30;
            invalid.waves[0].drops = 10000;
            Check(!g.UI.ValidSettings(invalid), "Excessive drops rejected");
            Reset(g);
            var firstPriest = g.Team[2];
            var secondPriest = g.Team[3];
            firstPriest.items.Add(g.MakeItem("amulet"));
            secondPriest.items.Clear();
            secondPriest.items.Add(g.MakeItem("censer"));
            secondPriest.items.Add(g.MakeItem("amulet"));
            g.RefreshEquipment();
            Check(r.Provider(g.Team, 0) == 2, "Shared healing tie uses lowest slot");
            g.Team[0].shareClock = 2;
            firstPriest.items.Add(g.MakeItem("ring"));
            g.RefreshEquipment();
            Check(g.Team[0].shareClock == 0 && g.Team[0].sharePeriod < 3, "Provider period change resets share timer");
            c = g.Team[0];
            for (int n = 0; n < 40; n++) { c.items.Add(g.MakeItem("ironshield")); c.items.Add(g.MakeItem("gauntlet")); }
            Check(g.Stats(c).def == .8f && g.Stats(c).haste == 1, "Defense and speed caps");
            Reset(g); c = g.Team[0]; c.items.Add(g.MakeItem("banner")); g.RefreshEquipment(); c.shield = 20;
            float beforeHP = c.hp;
            float expectedShield = 20 - 10 * (1 - g.Stats(c).def);
            typeof(DSGame).GetMethod("Damage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(g, new object[] { c, 10f });
            Check(c.hp == beforeHP && Math.Abs(c.shield - expectedShield) < .01, "Shield absorbs mitigated damage before health");
            Reset(g);
            var archer = g.Team[1];
            var arrowA = g.MakeItem("pierce"); arrowA.x = 1; arrowA.y = 2;
            var arrowB = g.MakeItem("pierce"); arrowB.x = 2; arrowB.y = 2; arrowB.grade = 4;
            archer.items.Add(arrowA); archer.items.Add(arrowB);
            Check(r.BonusValue(arrowA) == r.BonusValue(arrowB) && r.BonusState(archer, arrowA) == "활성", "Fixed-count bonus does not invent grade superiority");
            foreach (var definition in g.Data.items)
            {
                string description = g.UI.ItemDescription(g.MakeItem(definition.id));
                if (!string.IsNullOrEmpty(definition.attackMode))
                    Check(description.Contains("기본 " + definition.interval), definition.id + " action interval description");
                Check(description.Contains("기본 효과:") && description.Contains("추가 효과:") && description.Contains("조건:"), definition.id + " complete description");
            }
            Check(g.UI.ItemDescription(arrowB).Contains("추가 관통 1명"), "Legendary pierce description remains one target");
            var shieldDescription = g.MakeItem("ironshield"); shieldDescription.grade = 4;
            Check(g.UI.ItemDescription(shieldDescription).Contains((g.Items["ironshield"].baseValue * g.Data.grades[4].mult * 100).ToString("0.##") + "%"), "Scaled percentage description");
            Check(g.UI.ItemDescription(g.MakeItem("robe")).Contains("8초 보호막"), "Shield period description");
            Check(g.UI.ItemDescription(g.MakeItem("amulet")).Contains("자기 회복/3초"), "Healing period description");
            Check(g.UI.ItemDescription(g.MakeItem("firestaff")).Contains("폭발 투사체") && g.UI.ItemDescription(g.MakeItem("censer")).Contains("주변 회복"), "Weapon behavior description");
            Check(g.InitialLoadTime == firstLoad, "Restart does not overwrite first UI load");
            Reset(g); g.Config.adaptiveDrops = true;
            string weightsBefore = JsonUtility.ToJson(g.Config);
            var early = g.RewardWeights();
            Check(early.First(w => w.id == "whetstone").weight == g.Config.weights.First(w => w.id == "whetstone").weight * 2, "Early compatible auxiliary weighted twice");
            Check(early.First(w => w.id == "hat").weight == g.Config.weights.First(w => w.id == "hat").weight, "Unsupported weapon bonus gets no early bias");
            Check(early.First(w => w.id == "axe").weight > 0 && JsonUtility.ToJson(g.Config) == weightsBefore, "Alternative primary remains possible and source weights unchanged");
            g.Wave = 4;
            var late = g.RewardWeights();
            Check(late.First(w => w.id == "sword").weight == g.Config.weights.First(w => w.id == "sword").weight * 1.5f, "Later ordinary equipment merge candidate weighted");
            g.Equipment.Actions(g.Team[0]).First().grade = 4; g.Stash.Add(g.MakeItem("sword"));
            Check(g.RewardWeights().First(w => w.id == "sword").weight == g.Config.weights.First(w => w.id == "sword").weight, "Legendary and stash do not create merge bias");
            g.Equipment.Actions(g.Team[0]).First().grade = 0; g.Team[0].hp = 0;
            Check(g.RewardWeights().First(w => w.id == "sword").weight == g.Config.weights.First(w => w.id == "sword").weight, "Dead inventory excluded from reward bias");
            Check(late.First(w => w.id == "sword").weight > g.RewardWeights().First(w => w.id == "sword").weight, "Reward batch snapshot remains immutable");
            g.Config.weights.First(w => w.id == "sword").weight = 0;
            Check(!g.RewardWeights().Any(w => w.id == "sword"), "Zero user weight excluded");
            foreach (var definition in g.Data.items)
            {
                var icon = g.World.ItemIcon(definition.id);
                Check(icon != null && icon.texture.GetPixels().Count(p => p.a > 0) > 20, definition.id + " prototype icon visible");
            }
            Reset(g); g.State = "battle"; g.Spawned = 999999; g.Enemies.Clear(); g.Shots.Clear();
            foreach (var member in g.Team) foreach (var gear in member.items) gear.cd = 999;
            var dummy = new Enemy { id = 98000, type = "armored", hp = 1000, maxhp = 1000, pos = new Vector2(60, 0), phase = "test" };
            g.Enemies.Add(dummy);
            g.Shots.Add(new Shot { type = "firestaff", pos = dummy.pos, target = dummy.pos, damage = 10, radius = 75, range = 100 });
            g.Simulate(0); g.Simulate(0);
            Check(dummy.hp == 990 && g.Shots.Count == 0, "Explosion hits once and projectile disappears");
            for (int n = 0; n < 2; n++) g.Shots.Add(new Shot { type = "shortbow", pos = dummy.pos, target = dummy.pos, damage = 10, pierce = 1, range = 100 });
            g.Simulate(0); g.Simulate(0);
            Check(dummy.hp == 970, "Two projectiles can hit same enemy but neither hits it twice");
            g.Shots.Clear(); c = g.Team[0]; c.items.Clear(); c.items.Add(g.MakeItem("wand")); c.items[0].cd = 0;
            dummy.pos = c.pos + new Vector2(60, 0); dummy.hp = 1000;
            var secondDummy = new Enemy { id = 98001, type = "armored", hp = 1000, maxhp = 1000, pos = c.pos + new Vector2(80, 0), phase = "test" }; g.Enemies.Add(secondDummy);
            float chainPower = g.Equipment.WeaponPower(c, c.items[0]); g.Simulate(0);
            Check(Math.Abs(dummy.hp - (1000 - chainPower)) < .01 && Math.Abs(secondDummy.hp - (1000 - chainPower)) < .01, "Chain never revisits either target");
            for (int formation = 0; formation < 3; formation++)
            {
                Reset(g); g.Formation = g.PendingFormation = formation; g.State = "battle"; g.Spawned = 999999; g.Enemies.Clear();
                g.Center = new Vector2(50000, -50000);
                for (int n = 0; n < g.Team.Count; n++) g.Team[n].pos = g.Center + g.Offset(n);
                foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down, Vector2.zero })
                {
                    g.MoveInput = direction;
                    bool continuous = true, separated = true, stopConverges = true;
                    float previousError = g.Team.Select((member,n) => Vector2.Distance(member.pos, g.Center + g.Offset(n))).Max();
                    for (int step = 0; step < 20; step++)
                    {
                        var previousPositions = g.Team.Select(member => member.pos).ToArray();
                        g.Simulate(.05f);
                        float error = g.Team.Select((member,n) => Vector2.Distance(member.pos, g.Center + g.Offset(n))).Max();
                        continuous &= g.Team.Select((member,n) => Vector2.Distance(member.pos, previousPositions[n])).All(d => d <= 7.1f);
                        for (int a = 0; a < g.Team.Count; a++) for (int b = a + 1; b < g.Team.Count; b++) separated &= Vector2.Distance(g.Team[a].pos, g.Team[b].pos) >= 47;
                        if (direction == Vector2.zero) stopConverges &= error <= previousError + .01f;
                        previousError = error;
                    }
                    Check(g.Team.Select((member,n) => Vector2.Distance(member.pos,g.Center+g.Offset(n))).All(d => d < 5), "Formation tracks reversals and stop " + formation + "/" + direction);
                    Check(continuous, "Formation each step bounded movement " + formation + "/" + direction);
                    Check(separated, "Formation each step preserves center separation " + formation + "/" + direction);
                    if (direction == Vector2.zero) Check(stopConverges && previousError < .01f, "Formation stop converges without sustained oscillation " + formation);
                }
            }
            Reset(g); g.Config.adaptiveDrops = true;
            foreach (var weight in g.Config.weights) weight.weight = 2e38f;
            Check(g.RewardWeights().All(w => double.IsFinite(w.weight) && w.weight > 0) && double.IsFinite(g.RewardWeights().Sum(w => w.weight)), "Huge finite weights remain valid with bias");
            var draws = new HashSet<string>(); for (int n = 0; n < 1000; n++) draws.Add(g.Weighted());
            Check(draws.Count == 32, "Huge weights do not collapse lottery to last candidate");
            Check(!r.Shape(g.MakeItem("scripture")).SequenceEqual(r.Shape(g.MakeItem("lantern"))) && !r.Shape(g.MakeItem("bell")).SequenceEqual(r.Shape(g.MakeItem("vestment"))), "Duplicate healer equipment has distinct packing shapes");
            var healingTestPriest = new Survivor { index = 4, hp = 50 }; healingTestPriest.items.Add(g.MakeItem("relic"));
            var healingItem = g.MakeItem("scripture"); healingItem.grade = 4; healingTestPriest.items.Add(healingItem);
            Check(Math.Abs(r.Stats(healingTestPriest).heal - 4.8f * g.Data.grades[4].mult) < .01, "Adjusted healing scales once per grade");
            foreach (var pair in new[] { new[] { "scripture", "lantern" }, new[] { "bell", "vestment" } })
            foreach (var id in pair)
            {
                var pack = new Survivor { index = 0, hp = 100 };
                for (int packingY = 1; packingY <= 4; packingY++) for (int packingX = 1; packingX <= 4; packingX++) pack.cells[packingY * 6 + packingX] = true;
                var sample = g.MakeItem(id); var hole = r.Shape(sample);
                for (int packingY = 1; packingY <= 4; packingY++) for (int packingX = 1; packingX <= 4; packingX++)
                    if (!hole.Any(q => q.x + 1 == packingX && q.y + 1 == packingY)) { var block = g.MakeItem("amulet"); block.x = packingX; block.y = packingY; pack.items.Add(block); }
                var other = g.MakeItem(pair.First(v => v != id));
                Check(r.Fits(pack, sample, 1, 1, 0) && !Enumerable.Range(0, 4).Any(rot => r.Fits(pack, other, 1, 1, rot)), id + " distinct real 4x4 packing case");
            }
            var layouts = new[] {
                new[] { "sword", "whetstone", "gauntlet" }, new[] { "shortbow", "quiver", "split" }, new[] { "firestaff", "crystal", "spellbook" }, new[] { "relic", "scripture", "bell" },
                new[] { "sword", "ironshield", "chainmail" }, new[] { "shortbow", "leather", "helmet" }, new[] { "firestaff", "robe", "chainmail" }, new[] { "relic", "helmet", "robe" },
                new[] { "sword", "amulet", "ring" }, new[] { "shortbow", "amulet", "ring" }, new[] { "firestaff", "amulet", "ring" }, new[] { "relic", "amulet", "ring" }
            };
            foreach (var layout in layouts)
            {
                var pack = new Survivor { index = 0, hp = 100 };
                for (int cellY = 1; cellY <= 4; cellY++) for (int cellX = 1; cellX <= 4; cellX++) pack.cells[cellY * 6 + cellX] = true;
                foreach (var id in layout)
                {
                    var gear = g.MakeItem(id); bool placed = false;
                    for (int rot = 0; rot < 4 && !placed; rot++) for (int py = 1; py <= 4 && !placed; py++) for (int px = 1; px <= 4 && !placed; px++)
                        if (r.Fits(pack, gear, px, py, rot)) { gear.x = px; gear.y = py; gear.rotation = rot; if (r.Definition(gear).requiresAdjacent && r.Actions(pack).FirstOrDefault() != null && !r.Adjacent(gear, r.Actions(pack).FirstOrDefault())) continue; pack.items.Add(gear); placed = true; }
                    Check(placed, "Mixed build fits " + string.Join("/",layout) + "/" + id);
                }
                var stats = r.Stats(pack);
                Check(stats.hp > 0 && stats.def <= .8f && stats.haste <= 1 && (!layout.Contains("amulet") || stats.heal > 0), "Mixed build stats valid " + string.Join("/",layout));
            }
            PreviewTransactionChecks(g);
            ParallelChecks(g);
            BattleDisplayChecks(g);
            return passed.Count + " checks passed\n" + string.Join("\n", passed.Select(s => "PASS " + s));
        }
    }
}
