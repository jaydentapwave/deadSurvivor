using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSGame
    {
        void Damage(Survivor c, float amount)
        {
            if (c.hp <= 0)
                return;
            var s = Stats(c);
            float taken = amount * (1 - s.def);
            c.shield = Mathf.Min(c.shield, s.shield);
            float absorbed = Mathf.Min(c.shield, taken);
            c.shield -= absorbed;
            float oldHP = c.hp;
            c.hp = Mathf.Max(0, c.hp - taken + absorbed);
            c.damageTaken += oldHP - c.hp;
            c.flash = .15f;
            if (c.hp <= 0)
            {
                Burst(c.pos, 35, .6f, "#e17769");
                RefreshProviders();
            }
        }
        void Hit(Enemy e, float n, string type, Vector2 dir)
        {
            if (e.hp <= 0)
                return;
            e.hp -= n;
            e.flash = .12f;
            Effects.Add(new Effect { pos = e.pos + new Vector2(0, -18), text = Mathf.RoundToInt(n).ToString(), life = .65f, max = .65f, color = "#fff2cc" });
            if (e.hp <= 0)
            {
                Kills++;
                WaveKills++;
                Burst(e.pos, 18, .35f, "#b3cc76");
            }
        }
        void Burst(Vector2 pos, float r, float life, string color)
        {
            Effects.Add(new Effect { pos = pos, r = r, life = life, max = life, color = color });
        }
        float Heading()
        {
            if (MoveInput.sqrMagnitude < .0001f)
                return Random01() * Mathf.PI * 2;
            float roll = Random01();
            float sector = roll < .5f ? 0 : roll < .65f ? Mathf.PI / 2 : roll < .8f ? -Mathf.PI / 2 : Mathf.PI;
            return Mathf.Atan2(MoveInput.y, MoveInput.x) + sector + (Random01() - .5f) * Mathf.PI / 2;
        }
        Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        public void Spawn(string type = null, float? angle = null, bool extra = false)
        {
            NoteMovementFixtureCombat();
            var d = Config.waves[Wave - 1];
            type ??= d.types[Rng.Next(d.types.Length)];
            if (type == "bomber" && angle == null)
            {
                if (bomberSpawnCount++ % 6 == 0)
                    bomberSpawnAngle = Heading();
                angle = bomberSpawnAngle + (Random01() - .5f) * .24f;
            }
            float a = angle ?? Heading();
            float r = new Vector2(ViewWidth / 2, ViewHeight / 2).magnitude + 70;
            var spec = Monsters[type];
            Vector2 position=Center+Direction(a)*r;
            if(MapSession && !TryMapSpawn(type,a,r,out position)){MapSpawnFailures++;return;}
            Enemies.Add(new Enemy { id = ++enemyUid, type = type, pos = position, hp = spec.hp * d.hp, maxhp = spec.hp * d.hp, damage = spec.damage * d.damage });
            if (!extra)
                Spawned++;
        }
        void Recycle(float dt)
        {
            NoteMovementFixtureCombat();
            recycleCooldown = Mathf.Max(0, recycleCooldown - dt);
            List<Enemy> eligible = new();
            foreach (var e in Enemies)
            {
                Vector2 v = e.pos - Center;
                bool far = Mathf.Abs(v.x) > ViewWidth / 2 + 300 || Mathf.Abs(v.y) > ViewHeight / 2 + 300;
                bool behind = MoveInput.sqrMagnitude < .0001f || Vector2.Dot(v, MoveInput) < 0;
                if (!far || !behind || e.hp <= 0 || e.phase != "approach")
                {
                    e.offscreenTime = 0;
                    continue;
                }
                e.offscreenTime += dt;
                if (e.offscreenTime >= 3)
                    eligible.Add(e);
            }
            if (recycleCooldown > 0 || eligible.Count == 0)
                return;
            foreach (var e in eligible.OrderByDescending(e => e.offscreenTime).Take(2))
            {
                var v = Direction(Heading());
                float r = Mathf.Min((ViewWidth / 2 + 150) / Mathf.Max(1e-8f, Mathf.Abs(v.x)), (ViewHeight / 2 + 150) / Mathf.Max(1e-8f, Mathf.Abs(v.y)));
                var position=Center+v*r;
                if(MapSession && !TryMapSpawn(e.type,Mathf.Atan2(v.y,v.x),r,out position))continue;
                e.pos = position;
                mapRoutes.Remove(e);
                e.offscreenTime = 0;
                e.cd = Mathf.Max(1, e.cd);
                e.flock = Vector2.zero;
                e.phaseTime = 0;
            }
            recycleCooldown = .5f;
        }
        void Burning()
        {
            NoteMovementFixtureCombat();
            if (Wave < 6)
                return;
            float start = Duration / 2;
            if (Elapsed < start)
                return;
            int count = Mathf.RoundToInt(Config.waves[Wave - 1].count * Config.countScale);
            float interval = Mathf.Max(.01f, (Duration - 4) / Mathf.Max(1, count - 1) / 2);
            int due = Mathf.Min(Mathf.CeilToInt(10 / interval), Mathf.FloorToInt((Mathf.Min(Elapsed, start + 10 - .0001f) - start) / interval) + 1);
            int attempts=0;
            while (burningSpawned < due && (!MapSession || attempts++<4))
            {
                float a = burningAngle + (burningSpawned % 2) * Mathf.PI + (Random01() - .5f) * .3f;
                int before=Enemies.Count;
                Spawn(Random01() < .8f ? "melee" : "charger", a, true);
                if(MapSession && Enemies.Count==before)break;
                burningSpawned++;
            }
        }
        void BomberMove(Enemy e, Survivor target, float dt)
        {
            var mates = Enemies.Where(n => n != e && n.type == "bomber" && n.hp > 0 && n.phase != "fuse" && Vector2.Distance(e.pos, n.pos) < 260).ToList();
            if (e.phase != "swarm" && Vector2.Distance(e.pos, target.pos) < 300)
            {
                e.phase = "swarm";
                e.rushTime = 1.4f;
                foreach (var n in mates)
                {
                    n.phase = "swarm";
                    n.rushTime = 1.4f;
                }
            }
            Vector2 v = (target.pos - e.pos).normalized;
            if (mates.Count > 0)
            {
                Vector2 center = Vector2.zero, align = Vector2.zero, separation = Vector2.zero;
                foreach (var n in mates)
                {
                    center += n.pos;
                    align += n.flock;
                    float d = Vector2.Distance(e.pos, n.pos);
                    if (d < 38)
                        separation += d > .01f ? (e.pos - n.pos) / d * (1 - d / 38) : Direction(e.id * 2.4f);
                }
                v += .65f * (center / mates.Count - e.pos).normalized + .35f * align / mates.Count + 1.8f * separation;
            }
            e.flock = Vector2.Lerp(e.flock, v.normalized, Mathf.Min(1, dt * 7));
            e.pos=EnemyAdvance(e,target.pos,e.flock,(e.phase=="swarm"?225:Monsters[e.type].speed)*dt);
            if (e.phase == "swarm")
            {
                e.rushTime -= dt;
                if (e.rushTime <= 0)
                    e.phase = "approach";
            }
        }
        void Rally(Enemy leader)
        {
            var group = Enemies.Where(e => e != leader && e.type == "charger" && e.hp > 0 && e.phase == "approach" && e.cd <= 0 && Vector2.Distance(e.pos, leader.pos) <= 300).OrderBy(e => Vector2.Distance(e.pos, leader.pos)).Take(15).ToList();
            group.Insert(0, leader);
            foreach (var e in group)
            {
                var t = ClosestSurvivor(e.pos);
                if (t == null)
                    continue;
                e.phase = "windup";
                e.phaseTime = .8f;
                e.charge = (t.pos - e.pos).normalized;
            }
        }
        Survivor ClosestSurvivor(Vector2 p)
        {
            Survivor best = null;
            float r = float.MaxValue;
            foreach (var c in Team)
            {
                if (c.hp <= 0)
                    continue;
                float d = (p - c.pos).sqrMagnitude;
                if (d < r)
                {
                    r = d;
                    best = c;
                }
            }
            return best;
        }
        void UpdateEnemy(Enemy e, float dt)
        {
            NoteMovementFixtureCombat();
            if (e.hp <= 0)
                return;
            var spec = Monsters[e.type];
            e.cd -= dt;
            e.flash = Mathf.Max(0, e.flash - dt);
            var t = ClosestSurvivor(e.pos);
            if (t == null)
                return;
            float best = Vector2.Distance(e.pos, t.pos);
            var dir = (t.pos - e.pos).normalized;
            if (e.type == "bomber" && (e.phase == "fuse" || best < 65 && MapLine(e.pos,t.pos)))
            {
                if (e.phase != "fuse")
                {
                    e.phase = "fuse";
                    e.phaseTime = 1.2f;
                }
                e.phaseTime -= dt;
                if (e.phaseTime <= 0)
                {
                    foreach (var c in Team)
                    if (c.hp > 0 && Vector2.Distance(e.pos, c.pos) <= 90 && MapLine(e.pos,c.pos))
                        Damage(c, e.damage);
                    Burst(e.pos, 90, .5f, "#ff6969");
                    e.hp = 0;
                }
                return;
            }
            if (e.type == "bomber")
            {
                BomberMove(e, t, dt);
                return;
            }
            if (e.type == "charger")
            {
                if (e.phase == "windup")
                {
                    e.phaseTime -= dt;
                    if (e.phaseTime <= 0)
                    {
                        e.phase = "dash";
                        e.phaseTime = .7f;
                        e.dashHits.Clear();
                    }
                    return;
                }
                if (e.phase == "dash")
                {
                    Vector2 old = e.pos;
                    e.pos=EnemyDisplace(e,e.pos+e.charge*460*Mathf.Min(dt,e.phaseTime));
                    foreach (var c in Team)
                    if (c.hp > 0 && !e.dashHits.Contains(c) && SegmentDistance(old, e.pos, c.pos) < 28 && MapLine(old,c.pos) && MapLine(e.pos,c.pos))
                    {
                        Damage(c, e.damage);
                        e.dashHits.Add(c);
                    }
                    e.phaseTime -= dt;
                    if (e.phaseTime <= 0)
                    {
                        e.phase = "recover";
                        e.phaseTime = .8f;
                        e.cd = 2;
                    }
                    return;
                }
                if (e.phase == "recover")
                {
                    e.phaseTime -= dt;
                    if (e.phaseTime <= 0)
                        e.phase = "approach";
                    return;
                }
                if (best < 280 && e.cd <= 0 && MapLine(e.pos,t.pos))
                {
                    Rally(e);
                    return;
                }
            }
            if (e.type == "ranged")
            {
                float range = 252;
                if (best > range * .92f || !MapLine(e.pos,t.pos))
                    e.pos=EnemyAdvance(e,t.pos,dir,spec.speed*dt);
                else if (best < range * .65f)
                    e.pos=EnemyDisplace(e,e.pos-dir*spec.speed*dt);
                if (best < range && e.cd <= 0 && MapLine(e.pos,t.pos))
                {
                    Shots.Add(new Shot { pos = e.pos, dir = dir, speed = 180, damage = e.damage, type = "enemy", range = range * 1.5f });
                    e.cd = 2;
                }
            }
            else if (best > spec.radius + 5)
                e.pos=EnemyAdvance(e,t.pos,dir,spec.speed*dt);
            else if (e.cd <= 0 && MapLine(e.pos,t.pos))
            {
                Damage(t, e.damage);
                e.cd = e.type == "armored" ? 1.8f : 1;
            }
        }
        public static float SegmentDistance(Vector2 a, Vector2 b, Vector2 p)
        {
            var v = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, v) / Mathf.Max(v.sqrMagnitude, 1e-8f));
            return Vector2.Distance(p, a + v * t);
        }
        public void Simulate(float dt)
        {
#if UNITY_EDITOR
            if(movementFixture!=null)
            {
                SimulateFixture(dt);
                return;
            }
#endif
            if (State != "battle" || Paused)
                return;
            dt = Mathf.Min(dt, Mathf.Max(0, Duration - Elapsed));
            if(MapSession && !MapSession.CombatEnabled){MapOnlyStep(dt);return;}
            Elapsed += dt;
            TotalTime += dt;
            mapSearches=mapSpawnSearches=0;
            foreach(var gone in mapRoutes.Keys.Where(e=>!Enemies.Contains(e)||e.hp<=0).ToArray())mapRoutes.Remove(gone);
            int count = Mathf.RoundToInt(Config.waves[Wave - 1].count * Config.countScale);
            spawnClock -= dt;
            int spawnAttempts=0;
            while (Spawned < count && spawnClock <= 0 && (!MapSession || spawnAttempts++<4))
            {
                int before=Spawned;Spawn();
                if(MapSession && Spawned==before){spawnClock=.25f;break;}
                spawnClock += Mathf.Max(0, Duration - 4) / Mathf.Max(1, count - 1);
            }
            Burning();
            var motion = PlanPartyMotion(dt);
            Center = motion.Center;
            RefreshProviders();
            for (int ci = 0; ci < Team.Count; ci++)
            {
                var c = Team[ci];
                if (c.hp <= 0)
                    continue;
                c.aliveTime += dt;
                c.flash = Mathf.Max(0, c.flash - dt);
                c.pos = motion.Actual[ci];
                TickEquipment(c, dt);
            }
            Recycle(dt);
            int start=MapSession && Enemies.Count>0?mapEnemyCursor%Enemies.Count:0;
            for(int i=0;i<Enemies.Count;i++)UpdateEnemy(Enemies[(start+i)%Enemies.Count],dt);
            if(MapSession)mapEnemyCursor=(start+1)%Mathf.Max(1,Enemies.Count);
            foreach (var b in Shots)
            {
                if (b.dead)
                    continue;
                float step = b.speed * dt;
                var old = b.pos;
                var requested=b.pos+b.dir*step;
                if(MapSession && (!MapSession.Geometry.ValidCoordinate(requested)||!MapSession.Geometry.SafeSweep(old,requested,2))){b.dead=true;continue;}
                b.pos=requested;
                b.travel += step;
                if (b.type == "enemy")
                {
                    foreach (var c in Team)
                    if (c.hp > 0 && SegmentDistance(old, b.pos, c.pos) < 15 && MapLine(old,c.pos))
                    {
                        Damage(c, b.damage);
                        b.dead = true;
                        break;
                    }
                }
                else if (Items[b.type].attackMode == "explosion")
                {
                    if (Vector2.Distance(b.pos, b.target) < step + 5 || b.travel >= b.range)
                    {
                        Burst(b.pos, b.radius, .4f, "#ffb86d");
                        foreach (var e in Enemies)
                        if (Vector2.Distance(b.pos, e.pos) < b.radius && MapLine(b.pos,e.pos))
                            Hit(e, b.damage, b.type, b.dir);
                        b.dead = true;
                    }
                }
                else
                foreach (var e in Enemies)
                {
                    if (e.hp <= 0 || b.hits.Contains(e.id))
                        continue;
                    if (SegmentDistance(old, b.pos, e.pos) < Monsters[e.type].radius && MapLine(old,e.pos))
                    {
                        Hit(e, b.damage, b.type, b.dir);
                        b.hits.Add(e.id);
                        if (b.hits.Count > b.pierce)
                        {
                            b.dead = true;
                            break;
                        }
                    }
                }
                if (b.travel >= b.range)
                    b.dead = true;
            }
            Shots.RemoveAll(b => b.dead);
            Enemies.RemoveAll(e => e.hp <= 0);
            foreach (var e in Effects)
            {
                e.life -= dt;
                if (e.text != null)
                    e.pos.y -= dt * 28;
            }
            Effects.RemoveAll(e => e.life <= 0);
            if (Team.All(c => c.hp <= 0))
            {
                Result(false);
                return;
            }
            if (Elapsed >= Duration)
                FinishWave();
        }
    }
}
