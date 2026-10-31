using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
namespace DeadSurvivor
{
    public partial class DSGame : MonoBehaviour
    {
        public PrototypeData Data; public RunSettings Config; public Dictionary<string, ItemDef> Items = new(); public Dictionary<string, MonsterDef> Monsters = new();
        public List<int> Selected = new(); public List<Survivor> Team = new(); public List<Gear> Stash = new(); public List<Enemy> Enemies = new(); public List<Shot> Shots = new(); public List<Effect> Effects = new(); public List<WaveRecord> History = new();
        public string State = "lobby"; public bool Paused, Win; public int Wave, Kills, Spawned, WaveKills; public float Elapsed, TotalTime; public Vector2 Center, MoveInput; public float ViewWidth = 1180, ViewHeight = 748;
        public DSUI UI; public DSWorld World; int uid, enemyUid, burningSpawned, bomberSpawnCount; float spawnClock, recycleCooldown, burningAngle, bomberSpawnAngle; public System.Random Rng = new(); public System.Random LootRng = new();
        public float Random01() => (float)Rng.NextDouble(); public float Duration => Config.waves[Mathf.Clamp(Wave - 1, 0, Config.waves.Length - 1)].duration * Config.duration / 30f; public CharacterDef Def(Survivor c) => Data.characters[c.index];
        public float InitialLoadTime = -1;
        double initializationStarted;
        System.Collections.IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();
            InitialLoadTime = (float)(Time.realtimeSinceStartupAsDouble - initializationStarted);
        }
        void Awake()
        {
            initializationStarted = Time.realtimeSinceStartupAsDouble;
            Data = JsonUtility.FromJson<PrototypeData>(Resources.Load<TextAsset>("DeadSurvivor/PrototypeData").text);
            Config = JsonUtility.FromJson<RunSettings>(JsonUtility.ToJson(Data.settings));
            Items = Data.items.ToDictionary(x => x.id);
            Equipment = new EquipmentRules(Data);
            Monsters = Data.monsters.ToDictionary(x => x.id);
            World = gameObject.AddComponent<DSWorld>();
            World.Init(this);
            UI = gameObject.AddComponent<DSUI>();
            UI.Init(this);
        }
        void Update()
        {
#if UNITY_EDITOR
            if(movementFixture!=null)
            {
                movementFixture.AdvanceFrame(this);
                World.Render();
                UI.Tick();
                return;
            }
#endif
            RecordTime(Time.unscaledDeltaTime);
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.spaceKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)
                {
                    if (State == "battle")
                    {
                        Paused = !Paused;
                        UI.ShowPause();
                    }
                }
                if (k.digit1Key.wasPressedThisFrame && State == "battle" && !Paused)
                    FinishWave(true);
                if (k.rKey.wasPressedThisFrame)
                    UI.RotateHeld();
            }
            if (State == "battle" && !Paused)
            {
                MoveInput = ReadMovementInput();
                Simulate(Mathf.Min(Time.unscaledDeltaTime, .05f) * Config.speed);
            }
            World.Render();
            UI.Tick();
        }
        public Vector2 ReadMovementInput()
        {
            var k=Keyboard.current;
            var input=ReadKeyboardMovement(k);
            if(UI.JoystickActive) input=UI.Joystick;
            return Vector2.ClampMagnitude(input,1);
        }
        public static Vector2 ReadKeyboardMovement(Keyboard k)
        {
            if(k==null)return Vector2.zero;
            return new Vector2((k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0),
                (k.sKey.isPressed||k.downArrowKey.isPressed?1:0)-(k.wKey.isPressed||k.upArrowKey.isPressed?1:0));
        }
    }
}
