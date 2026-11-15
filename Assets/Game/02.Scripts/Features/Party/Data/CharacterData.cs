using System;
using UnityEngine;

[Serializable]
public class CharacterData
{
    [SerializeField] private string id;
    [SerializeField] private int order;
    [SerializeField] private string name;
    [SerializeField] private string role;
    [SerializeField] private int hp;
    [SerializeField] private float atk;
    [SerializeField] private float def;
    [SerializeField] private string startItem;
    [SerializeField] private string color;

    public string Id => id;
    public int Order => order;
    public string Name => name;
    public string Role => role;
    public int Hp => hp;
    public float Atk => atk;
    public float Def => def;
    public string StartItem => startItem;
    public string Color => color;
}
