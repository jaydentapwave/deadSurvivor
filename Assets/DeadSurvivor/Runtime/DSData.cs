using System;
using System.Collections.Generic;
using UnityEngine;
namespace DeadSurvivor
{
    [Serializable]
    public class CharacterDef
    {
        public string id, name, role, weapon, color; public float hp, atk, def;
    }
    [Serializable]
    public class ItemDef
    {
        public string attackMode, baseEffect, bonusEffect, targetMode, spriteKey; public float baseValue, bonusValue, effectInterval = 3, effectRadius = 100; public bool requiresAdjacent; public string id, name, icon, color, effect; public Vector2Int[] shape; public float damage, interval, range; public int expand;
    }
    [Serializable]
    public class MonsterDef
    {
        public string id, name, color; public float hp, damage, speed, radius;
    }
    [Serializable]
    public class GradeDef
    {
        public string name, color; public float mult, armor;
    }
    [Serializable]
    public class Weight
    {
        public string id; public float weight;
    }
    public class RewardWeight { public string id; public double weight; }
    [Serializable]
    public class WaveDef
    {
        public float duration; public int drops; public int count; public float hp, damage; public string[] types;
    }
    [Serializable]
    public class RunSettings
    {
        public bool adaptiveDrops;
        public float duration = 30, speed = 1, countScale = 1; public int drops = 0; public Weight[] weights; public WaveDef[] waves;
    }
    [Serializable]
    public class PrototypeData
    {
        public CharacterDef[] characters; public ItemDef[] items; public MonsterDef[] monsters; public GradeDef[] grades; public RunSettings settings;
    }
    [Serializable]
    public class Gear
    {
        public int id, grade, x, y; public string type; public int rotation; [NonSerialized] public float cd, lastPeriod, activationFlash;
    }
    [Serializable]
    public class Survivor
    {
        public int index; public float hp, maxhp, shield, damageTaken, healingReceived, aliveTime; public List<Gear> items = new(); public bool[] cells = new bool[36]; [NonSerialized] public Vector2 pos; [NonSerialized] public float healClock, shieldClock, flash, shareClock; public int shareProvider = -1; public float sharePeriod;
    }
    public class Enemy
    {
        public int id; public string type, phase = "approach"; public Vector2 pos, charge, flock; public float hp, maxhp, damage, cd = 1, flash, phaseTime, rushTime, offscreenTime; public HashSet<Survivor> dashHits = new();
    }
    public class Shot
    {
        public string type; public Vector2 pos, dir, target; public float speed, damage, range, travel; public bool dead; public Survivor owner; public int pierce; public float radius = 75; public HashSet<int> hits = new();
    }
    public class Effect
    {
        public Vector2 pos; public float life, max, r; public string color; public string text;
    }
    public struct CombatStats
    {
        public float def, heal, attack, haste, range, shield, hp, startShield, radius, pulse; public int pierce, extraShots, chain;
    }
    [Serializable]
    public class WaveRecord
    {
        public int wave, kills, survivors; public float time, realBattle, prepTime, pausedTime; public int formation; public bool skipped, defeated; public List<string> revived = new();
    }
    [Serializable]
    public class RunReport
    {
        public string initialLoadScope = "Awake start to first completed UI frame; excludes process launch"; public bool measuredInEditor;
        public float realBattle, realPrep, realPaused, lobbyTime, resultTime, initialLoadTime = -1; public bool testControlsUsed, verificationRun; public string version = "Equipment prototype v0.4"; public bool win; public int wave, kills; public float totalTime; public List<Survivor> team; public RunSettings config; public List<WaveRecord> history;
    }
}
