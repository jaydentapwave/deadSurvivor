using System;
using UnityEngine;

[Serializable]
public class WaveData
{
    [SerializeField] private string id;
    [SerializeField] private int order;
    [SerializeField] private float duration;
    [SerializeField] private bool hasBurningTime;

    public string Id => id;
    public int Order => order;
    public float Duration => duration;
    public bool HasBurningTime => hasBurningTime;
}
