using System;
using UnityEngine;

[Serializable]
public class ItemData
{
    [SerializeField] private string id;
    [SerializeField] private string name;

    public string Id => id;
    public string Name => name;
}
