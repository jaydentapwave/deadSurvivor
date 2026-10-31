using System.Linq;
using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSGame
    {
        public EquipmentRules Equipment;
        public Gear Find(int id) => Stash.Concat(Team.SelectMany(c => c.items)).FirstOrDefault(i => i.id == id);
        public Survivor Owner(Gear i) => Team.FirstOrDefault(c => c.items.Contains(i));
        public Vector2Int[] Shape(Gear i) => Equipment.Shape(i);
        public void Remove(Gear i) => EquipmentRules.Remove(Team, Stash, i);
        public bool CanPlace(Gear i, Survivor c, int x, int y) => i != null && c.hp > 0 && !(Owner(i)?.hp <= 0) && Equipment.Fits(c, i, x, y, i.rotation);
        public bool Place(int id, int ci, int x, int y) => PlaceRotated(id, ci, x, y, Find(id)?.rotation ?? 0);
        public bool PlaceRotated(int id, int ci, int x, int y, int rotation)
        {
            if (State != "prep")
                return false;
            bool ok = Equipment.Drop(Team, Stash, id, ci, x, y, rotation, out _);
            if (ok)
                RefreshEquipment();
            return ok;
        }
        public bool CanMerge(Gear a, Gear b) => Equipment.MergeAllowed(a, b) && !(Owner(a)?.hp <= 0) && !(Owner(b)?.hp <= 0);
        public bool Merge(int a, int b)
        {
            if (State != "prep" || !Equipment.Merge(Team, Stash, a, b, out _))
                return false;
            RefreshEquipment();
            return true;
        }
        public bool Unequip(int id)
        {
            if (State != "prep" || !Equipment.Unequip(Team, Stash, id, out _))
                return false;
            RefreshEquipment();
            return true;
        }
        public void RefreshEquipment()
        {
            foreach (var c in Team)
            {
                c.maxhp = Stats(c).hp;
                c.hp = Mathf.Min(c.hp, c.maxhp);
                c.shield = Mathf.Min(c.shield, Stats(c).shield);
            }
            RefreshProviders();
        }
        public void Revive(int ci)
        {
            if (RejectMovementFixtureCommand("Revive")) return;
            if (ci < 0 || ci >= Team.Count)
                return;
            var c = Team[ci];
            if (State != "prep" || c.hp > 0)
                return;
            c.hp = c.maxhp;
            c.healClock = c.shieldClock = c.shield = c.flash = c.shareClock = 0;
            c.pos = Center + Offset(ci);
            foreach (var i in c.items)
            { i.cd = 0; i.lastPeriod = 0; i.activationFlash = 0; }
            if (History.Count > 0)
                History[^1].revived.Add(Def(c).name);
            RefreshEquipment();
            UI.Prep();
        }
    }
}
