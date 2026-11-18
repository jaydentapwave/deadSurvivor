using System;
using UnityEngine;

// Adopted scenery assets; prefab and fade materials are shared across all placements.
public sealed class FieldModelCatalog : ScriptableObject
{
    public FieldModelEntry[] Entries;

    public FieldModelEntry Find(string id)
    {
        foreach (var entry in Entries)
        {
            if (entry.Id == id)
            {
                return entry;
            }
        }
        throw new ArgumentException("Unknown field model: " + id);
    }
}

[Serializable]
public sealed class FieldModelEntry
{
    public string Id;
    public GameObject Prefab;
    public Material Material;
    public Material FadedMaterial;
    public Bounds Bounds;
    public int Triangles;
}
