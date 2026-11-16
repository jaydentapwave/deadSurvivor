using System.Collections.Generic;
using UnityEngine;

// 지금 보이는 피해 숫자를 만든 순서로 든다(앞 = 가장 오래된 것). 나이는 숫자와 같은 자리에 둔다
public class DamageNumberViewRegistry
{
    private readonly List<DamageNumberEntry> _entryList = new();

    public IReadOnlyList<DamageNumberEntry> EntryList => _entryList;
    public int Count => _entryList.Count;

    public void Register(DamageNumberEntry entry) => _entryList.Add(entry);

    public void Unregister(int index) => _entryList.RemoveAt(index);
}

public class DamageNumberEntry
{
    public DamageNumberView View { get; }
    // 시작 자리(월드)
    public Vector2 StartPosition { get; }
    public float Age { get; set; }

    public DamageNumberEntry(DamageNumberView view, Vector2 startPosition)
    {
        View = view;
        StartPosition = startPosition;
    }
}
