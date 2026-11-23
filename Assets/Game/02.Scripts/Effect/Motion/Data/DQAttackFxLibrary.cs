using System;
using UnityEngine;

[PreferBinarySerialization]
public sealed class DQAttackFxLibrary : ScriptableObject
{
    public const string ResourcePath = "Art/DQ/AttackFx";
    [Serializable]
    public sealed class Entry
    {
        public string actor;
        public FacingDirection direction;
        public int eventFrame;
        public Sprite[] frames;
    }

    public Entry[] entries;
    public Material bladeMaterial;
    public Material magicMaterial;

    public Sprite GetFrame(string actor, FacingDirection direction, int frame)
    {
        foreach (var entry in entries)
        {
            if (entry.actor == actor && entry.direction == direction)
            {
                return frame >= entry.eventFrame && frame < entry.frames.Length ? entry.frames[frame] : null;
            }
        }
        return null;
    }
}
