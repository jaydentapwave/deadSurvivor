using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RunRulesData
{
    [SerializeField] private float simStep;
    [SerializeField] private float burningStartRatio;
    [SerializeField] private float burningSeconds;
    [SerializeField] private float[] speedOptions;
    [SerializeField] private int defaultSeed;
    [SerializeField] private float defaultEnemyCountScale;
    [SerializeField] private float unitScale;
    [SerializeField] private int fieldCells;

    public float SimStep => simStep;
    public float BurningStartRatio => burningStartRatio;
    public float BurningSeconds => burningSeconds;
    public IReadOnlyList<float> SpeedOptions => speedOptions;
    public int DefaultSeed => defaultSeed;
    public float DefaultEnemyCountScale => defaultEnemyCountScale;
    public float UnitScale => unitScale;
    public int FieldCells => fieldCells;
}
