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
    [SerializeField] private string sprite;

    public string Id => id;
    public int Order => order;
    public string Name => name;
    public string Role => role;
    public int Hp => hp;
    public float Atk => atk;
    public float Def => def;
    public string StartItem => startItem;
    public string Color => color;
    // 그림 키. 비면 F1 임시 도형(아트 단계가 채운다)
    public string Sprite => sprite;
}
