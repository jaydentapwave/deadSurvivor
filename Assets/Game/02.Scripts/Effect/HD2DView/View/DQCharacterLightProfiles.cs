using System;
using UnityEngine;

// Authored visual material assignments, independent of combat and clip timing/data.
public sealed class DQCharacterLightProfiles : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string ActorId;
        public Material Material;
    }
    public Entry[] Entries;

    public Material Find(string actorId)
    {
        foreach (var entry in Entries)
        {
            if (entry.ActorId == actorId)
            {
                return entry.Material;
            }
        }
        throw new ArgumentException("Missing DQ lighting material for " + actorId, nameof(actorId));
    }
}
