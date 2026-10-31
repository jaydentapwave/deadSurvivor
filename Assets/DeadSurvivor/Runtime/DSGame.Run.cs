using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSGame
    {
        public Gear MakeItem(string type)
        {
            return new Gear { id = ++uid, type = type, cd = (float)LootRng.NextDouble() * .2f };
        }
        public void ToggleSelection(int i)
        {
            if (RejectMovementFixtureCommand("ToggleSelection")) return;
            if (Selected.Contains(i))
                Selected.Remove(i);
            else if (Selected.Count < 4)
                Selected.Add(i);
            else
                UI.Toast("최대 4명까지 선발할 수 있습니다.");
            UI.Lobby();
        }
        public void StartRun()
        {
            if (RejectMovementFixtureCommand("StartRun")) return;
            if (Selected.Count == 0)
                return;
            if(MapSession && !DSMapPartyRules.CanStart(MapSession.Geometry,MapSession.StartCenter,Selected.Count)){RejectMap();return;}
            Center=MapSession?MapSession.StartCenter:Vector2.zero;
            uid = enemyUid = 0;
            MoveInput = Vector2.zero;
            Formation = PendingFormation = 0;
            RealBattle = RealPrep = RealPaused = WaveReal = WavePaused = ResultTime = 0;
            TestControlsUsed = JsonUtility.ToJson(Config) != JsonUtility.ToJson(Data.settings);
            Team.Clear();
            for (int i = 0; i < Selected.Count; i++)
            {
                var d = Data.characters[Selected[i]];
                var c = new Survivor { index = Selected[i], hp = d.hp, maxhp = d.hp, pos = Center + Offset(i) };
                for (int y = 1; y <= 4; y++)
                for (int x = 1; x <= 4; x++)
                    c.cells[y * 6 + x] = true;
                var item = MakeItem(d.weapon);
                item.x = 1;
                item.y = 1;
                c.items.Add(item);
                Team.Add(c);
            }
            Wave = Kills = WaveKills = 0;
            TotalTime = 0;
            History.Clear();
            Stash.Clear();
            Center = MapSession?MapSession.StartCenter:Vector2.zero;
            NextWave();
        }
        public Vector2 Offset(int i) => FormationOffset(Formation, i, Mathf.Max(1, Selected.Count));
        public void NextWave()
        {
            if (RejectMovementFixtureCommand("NextWave")) return;

            if (Wave >= 10)
            {
                Result(true);
                return;
            }
            if(!MapAllows(PendingFormation))return;
            ResetMapCombat();
            Stash.Clear();
            Wave++;
            Formation = PendingFormation;
            WaveReal = WavePaused = 0;
            State = "battle";
            Paused = false;
            Elapsed = 0;
            Spawned = WaveKills = 0;
            spawnClock = 0;
            bomberSpawnCount = burningSpawned = 0;
            recycleCooldown = 0;
            burningAngle = Random01() * Mathf.PI * 2;
            Enemies.Clear();
            Shots.Clear();
            Effects.Clear();
            foreach (var c in Team)
            {
                foreach (var item in c.items) item.activationFlash = 0;
                c.healClock = c.shieldClock = c.shareClock = 0;
                c.shield = Stats(c).startShield;
                c.shareProvider = -1;
            }
            RefreshEquipment();
            UI.Battle();
        }
        public void FinishWave(bool skipped = false)
        {
            if (RejectMovementFixtureCommand("FinishWave")) return;
            if (State != "battle")
                return;
            if (Team.All(c => c.hp <= 0))
            {
                Result(false);
                return;
            }
            TestControlsUsed |= skipped;
            History.Add(new WaveRecord { wave = Wave, time = Elapsed, realBattle = WaveReal, pausedTime = WavePaused, formation = Formation, kills = WaveKills, survivors = Team.Count(c => c.hp > 0), skipped = skipped });
            Enemies.Clear();
            Shots.Clear();
            Effects.Clear();
            if (Wave == 10)
            {
                Result(true);
                return;
            }
            State = "prep";
            var rewardPool = RewardWeights();
            int rewardCount = Config.drops > 0 ? Config.drops : Config.waves[Wave - 1].drops;
            if (rewardCount == 0) rewardCount = LootRng.Next(3, 6);
            for (int i = 0; i < rewardCount; i++)
                Stash.Add(MakeItem(Weighted(rewardPool)));
            UI.Prep();
        }
        public RewardWeight[] RewardWeights()
        {
            var living = Team.Where(c => c.hp > 0).ToArray();
            var mergeCandidates = new HashSet<string>(living.SelectMany(c => c.items).Where(i => i.grade == 0).Select(i => i.type));
            return Config.weights.Where(w => w.weight > 0 && Items.ContainsKey(w.id) && Items[w.id].expand == 0).Select(w =>
            {
                var d = Items[w.id];
                float factor = 1;
                if (Config.adaptiveDrops)
                {
                    if (Wave <= 3 && living.Any(c => Equipment.BonusTargetMatches(c, d))) factor = 2;
                    else if (Wave >= 4 && mergeCandidates.Contains(w.id)) factor = 1.5f;
                }
                return new RewardWeight { id = w.id, weight = (double)w.weight * factor };
            }).ToArray();
        }
        public string Weighted() => Weighted(RewardWeights());
        string Weighted(RewardWeight[] pool)
        {
            if (pool.Length == 0)
                return Data.items[0].id;
            double r = LootRng.NextDouble() * pool.Sum(w => w.weight);
            foreach (var w in pool)
            {
                r -= w.weight;
                if (r < 0)
                    return w.id;
            }
            return pool[^1].id;
        }
        public void Result(bool win)
        {
            if (RejectMovementFixtureCommand("Result")) return;
            if (State == "result")
                return;
            if (!win && State == "battle" && (History.Count == 0 || History[History.Count - 1].wave != Wave))
                History.Add(new WaveRecord { wave = Wave, time = Elapsed, realBattle = WaveReal, pausedTime = WavePaused, formation = Formation, kills = WaveKills, survivors = Team.Count(c => c.hp > 0), defeated = true });
            Win = win;
            State = "result";
            Paused = false;
            UI.Result();
        }
        public void ReturnLobby()
        {
            if (RejectMovementFixtureCommand("ReturnLobby")) return;
            if (State != "lobby")
                LobbyTime = 0;
            State = "lobby";
            Paused = false;
            Enemies.Clear();
            Shots.Clear();
            Effects.Clear();
            UI.Lobby();
        }
        public string Export()
        {
            var report = new RunReport { initialLoadTime = InitialLoadTime, measuredInEditor = Application.isEditor, realBattle = RealBattle, realPrep = RealPrep, realPaused = RealPaused, lobbyTime = LobbyTime, resultTime = ResultTime, testControlsUsed = TestControlsUsed, verificationRun = VerificationRun, win = Win, wave = Wave, kills = Kills, totalTime = TotalTime, team = Team, config = Config, history = History };
            string p = Path.Combine(Application.persistentDataPath, "dead-survivor-playtest.json");
            File.WriteAllText(p, JsonUtility.ToJson(report, true));
            UI.Toast("테스트 기록 저장: " + p);
            Debug.Log("Playtest report: " + p);
            return p;
        }

    }
}
