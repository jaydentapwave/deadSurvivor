using System;
using System.Collections.Generic;
using UnityEngine;

// 임시 전투 값(기획서 E절). F3·F4가 칸을 지우고 마지막에 파일을 지운다.
[Serializable]
public class PlaceholderCombatData
{
    [SerializeField] private Vector2[] memberSpots;
    [SerializeField] private PlaceholderWeaponData weapon;

    public IReadOnlyList<Vector2> MemberSpots => memberSpots;
    public PlaceholderWeaponData Weapon => weapon;
}

[Serializable]
public class PlaceholderWeaponData
{
    [SerializeField] private float damage;
    [SerializeField] private float interval;
    [SerializeField] private float range;

    public float Damage => damage;
    public float Interval => interval;
    public float Range => range;
}
