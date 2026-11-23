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
    // Step 14 leaf-cluster cards on trees and bushes (null for other models).
    public Material LeafMaterial;
    public Material FadedLeafMaterial;
    public Bounds Bounds;
    public int Triangles;
}
