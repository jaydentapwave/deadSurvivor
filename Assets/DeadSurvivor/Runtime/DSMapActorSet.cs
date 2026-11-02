using System;
using UnityEngine;
namespace DeadSurvivor
{
    // 045: which 3D prefab draws which hero/monster in map mode. Visual data only; never read by the simulation.
    [CreateAssetMenu(menuName="DeadSurvivor/Map Actor Set")]
    public sealed class DSMapActorSet : ScriptableObject
    {
        [Serializable] public class Entry
        {
            public string id;
            public GameObject prefab;     // root at the feet; model faces +Z
            public Material material;     // optional shared override (e.g. per-monster tint)
            public float yawOffset;       // degrees, if the model's front is not +Z
        }
        public Entry[] Heroes=Array.Empty<Entry>();
        public Entry[] Monsters=Array.Empty<Entry>();
        public Material HitMaterial;
        [Tooltip("World height of a hero model (prefab normalised to height 1).")] public float HeroHeight=.8f;
        [Tooltip("World height of a radius-18 monster; other radii scale linearly.")] public float MonsterHeight=.52f;
        public float MonsterReferenceRadius=18;
        [Tooltip("Camera-up lift of the HP bar above the feet.")] public float BarLift=.66f;
        [Tooltip("Camera-up lift of the occlusion focus point above the feet.")] public float FocusLift=.28f;
        public float TurnDegreesPerSecond=720;
        public float RunSpeedThreshold=.15f;   // world units per second
        public float BobHeight=.04f, BobRate=7;
        public Entry Hero(string id)=>Find(Heroes,id);
        public Entry Monster(string id)=>Find(Monsters,id);
        static Entry Find(Entry[] list,string id){foreach(var e in list)if(e!=null&&e.id==id&&e.prefab)return e;return null;}
    }
}
