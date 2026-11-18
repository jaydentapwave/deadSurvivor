using System;
using UnityEngine;

// Baked view-only metres on XZ; PCG also exports matching gameplay path cells.
[Serializable]
public class FieldLayoutData
{
    public int seed = 181108;
    public float groundHalfDiagonal = 36f;
    public float pathClearance = 3f;
    public float roadsideClearance = 1.2f;
    public float startClearance = 3.5f;
    public float yellowClearance = 6f;
    public FieldPathLayout path = new();
    public FieldBankLayout[] banks;
    public FieldWaterLayout[] water;
    public FieldLayoutPlacement[] placements;
}

[Serializable]
public class FieldPathLayout
{
    public Vector2[] points;
    public Vector2[] branch;
    public float halfWidth = .67f;
    public float widthVariation = .10f;
    public float edgeVariation = .10f;
}

[Serializable]
public class FieldBankLayout
{
    public string name;
    public int seed;
    public float height = .36f;
    public float cell = .125f;
    public Rect[] rectangles;
    public FieldBankRun[] runs;
}

[Serializable]
public class FieldWaterLayout
{
    public string name;
    public string mesh;
    public Vector2[] points;
    public float width;
    public float height;
}

[Serializable]
public class FieldLayoutPlacement
{
    public string kind = "card";
    public string group;
    public string sprite;
    public string prop;
    public float x;
    public float y;
    public float height;
    public float yaw;
    public float scale = 1f;
    public bool isFlipped;
    public bool isCastingShadow;
    public bool isRoadside;
    public Vector2 Floor => new(x, y);
}
