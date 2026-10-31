using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace DeadSurvivor.Editor
{
    [Serializable] public class ExperimentResult
    {
        public string policy = "initial equipment; no reward equip/merge/revive; square movement 20s";
        public int seed, formation, steps, wave, kills;
        public float dt = .05f, simulationTime, maxSlotError, viewWidth = 1180, viewHeight = 748;
        public bool win, repeatMatches;
        public int[] characters;
        public string initialEquipment, settings;
        public float[] damage, healing, alive;
        public string outcome, variant = "baseline";
        public bool equipped;
    }
    public static class DSExperiments
    {
        // Synchronous only: the Editor update loop cannot interleave real input or simulation.
        public static string Compare(int seed, int formation)
        {
            var g = UnityEngine.Object.FindAnyObjectByType<DSGame>();
            if (g == null || g.State != "lobby") throw new Exception("Experiments require an owned fresh lobby session");
            var config = g.Config; var rng = g.Rng; var loot = g.LootRng;
            float width = g.ViewWidth, height = g.ViewHeight;
            var selected = g.Selected.ToList(); bool verification = g.VerificationRun;
            int oldFormation = g.Formation, oldPending = g.PendingFormation;
            try
            {
                g.VerificationRun = true;
                string first = JsonUtility.ToJson(Run(g, seed, formation));
                string second = JsonUtility.ToJson(Run(g, seed, formation));
                if (first != second) throw new Exception("Identical experiment did not reproduce");
                var result = JsonUtility.FromJson<ExperimentResult>(first);
                result.repeatMatches = true;
                return JsonUtility.ToJson(result, true);
            }
            finally
            {
                g.ViewWidth = width; g.ViewHeight = height;
                g.Config = config; g.Rng = rng; g.LootRng = loot; g.Selected = selected;
                g.VerificationRun = verification; g.MoveInput = Vector2.zero;
                g.Formation = oldFormation; g.PendingFormation = oldPending;
                g.ReturnLobby();
            }
        }
        [Serializable] public class LoadoutComparison
        {
            public ExperimentResult baseline, equipped;
            public float survivalDelta;
        }
        public static string CompareLoadout(int seed, string item)
        {
            var g = UnityEngine.Object.FindAnyObjectByType<DSGame>();
            if (g == null || g.State != "lobby" || !g.Items.ContainsKey(item)) throw new Exception("Owned lobby and known item required");
            var config = g.Config; var rng = g.Rng; var loot = g.LootRng; var selection = g.Selected.ToList();
            bool verification = g.VerificationRun; float width = g.ViewWidth, height = g.ViewHeight;
            int formation = g.Formation, pending = g.PendingFormation;
            try
            {
                g.VerificationRun = true;
                var baseline = Run(g, seed, 0, item, false);
                var equipped = Run(g, seed, 0, item, true);
                return JsonUtility.ToJson(new LoadoutComparison { baseline = baseline, equipped = equipped, survivalDelta = equipped.simulationTime - baseline.simulationTime }, true);
            }
            finally
            {
                g.Config = config; g.Rng = rng; g.LootRng = loot; g.Selected = selection; g.VerificationRun = verification;
                g.ViewWidth = width; g.ViewHeight = height; g.Formation = formation; g.PendingFormation = pending;
                g.MoveInput = Vector2.zero; g.ReturnLobby();
            }
        }
        static ExperimentResult Run(DSGame g, int seed, int formation, string variant = null, bool equipVariant = false)
        {
            g.Rng = new System.Random(seed); g.LootRng = new System.Random(seed ^ 0x5f3759df);
            g.Config = JsonUtility.FromJson<RunSettings>(JsonUtility.ToJson(g.Data.settings));
            g.Selected = new List<int> { 0, 2, 4, 5 };
            g.ViewWidth = 1180; g.ViewHeight = 748;
            g.StartRun(); g.Formation = g.PendingFormation = formation;
            if (variant != null)
            {
                var definition = g.Items[variant];
                var member = g.Team.FirstOrDefault(c => g.Equipment.Actions(c).Any(w => g.Equipment.MatchesMode(definition, w))) ?? g.Team[0];
                // Configure matching action in both branches; the tested item is then added, never replaces it.
                if (definition.targetMode == "chain")
                { member.items.Clear(); var compatible = g.MakeItem("wand"); compatible.x = compatible.y = 1; member.items.Add(compatible); }
                if (equipVariant)
                {
                    var item = g.MakeItem(variant);
                    bool placed = false;
                    for (int rot = 0; rot < 4 && !placed; rot++)
                    for (int py = 1; py <= 4 && !placed; py++)
                    for (int px = 1; px <= 4 && !placed; px++)
                    if (g.Equipment.Fits(member, item, px, py, rot))
                    { item.x = px; item.y = py; item.rotation = rot; if (definition.requiresAdjacent && !g.Equipment.Actions(member).Any(w => g.Equipment.BonusApplies(member, item, w))) continue; member.items.Add(item); placed = true; }
                    if (!placed) throw new Exception("Experiment cannot place " + variant);
                }
                g.RefreshEquipment();
                foreach (var member2 in g.Team) { member2.hp = member2.maxhp; member2.shield = g.Stats(member2).startShield; foreach (var gear in member2.items) gear.cd = 0; }
            }
            for (int n = 0; n < g.Team.Count; n++) g.Team[n].pos = g.Center + g.Offset(n);
            var result = new ExperimentResult { variant = variant ?? "baseline", equipped = equipVariant, seed = seed, formation = formation, characters = g.Selected.ToArray(), settings = JsonUtility.ToJson(g.Config), initialEquipment = string.Join(";", g.Team.Select(c => string.Join(",", c.items.Select(i => $"{i.type}:{i.grade}@{i.x},{i.y}/{i.rotation}")))) };
            if (variant != null) result.policy += "; parallel-v1 additive loadout; paired full HP and cooldown zero; compatible chain action in both branches";
            Vector2[] directions = { Vector2.right, Vector2.down, Vector2.left, Vector2.up };
            while (g.State != "result" && result.steps < 9000)
            {
                if (g.State == "prep") { g.NextWave(); continue; }
                g.MoveInput = directions[(result.steps / 100) % 4];
                g.Simulate(result.dt); result.steps++;
                for (int n = 0; n < g.Team.Count; n++)
                {
                    var c = g.Team[n];
                    if (!float.IsFinite(c.pos.x) || !float.IsFinite(c.pos.y)) throw new Exception("Nonfinite formation position");
                    if (c.hp > 0) result.maxSlotError = Mathf.Max(result.maxSlotError, Vector2.Distance(c.pos, g.Center + g.Offset(n)));
                }
            }
            if (g.State != "result") throw new Exception("Experiment step limit exceeded");
            result.wave = g.Wave; result.kills = g.Kills; result.win = g.Win; result.simulationTime = g.TotalTime;
            result.damage = g.Team.Select(c => c.damageTaken).ToArray(); result.healing = g.Team.Select(c => c.healingReceived).ToArray(); result.alive = g.Team.Select(c => c.aliveTime).ToArray();
            result.outcome = g.Win ? "clear" : "defeat";
            return result;
        }
    }
}
