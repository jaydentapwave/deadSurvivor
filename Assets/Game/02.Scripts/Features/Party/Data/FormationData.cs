using System;
using System.Collections.Generic;
using UnityEngine;

// 진형 하나(F2 기획서 B절, 데이터 스키마). slots[i] = 캐릭터 order 순 i번째 대원의 자리(바닥 단위, y 위 +).
// JsonUtility가 중첩 배열을 못 읽어 자리는 {x, y} 목록이다.
[Serializable]
public class FormationData
{
    [SerializeField] private string id;
    [SerializeField] private int order;
    [SerializeField] private string name;
    [SerializeField] private Vector2[] slots;

    public string Id => id;
    public int Order => order;
    public string Name => name;
    public IReadOnlyList<Vector2> Slots => slots;
}
