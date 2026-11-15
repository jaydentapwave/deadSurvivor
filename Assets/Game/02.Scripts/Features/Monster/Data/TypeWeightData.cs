using System;
using UnityEngine;

// 가중치 뽑기 한 칸: 몬스터 id와 가중치(웨이브 typeWeights, 생성 규칙 burningTypeWeights)
[Serializable]
public class TypeWeightData
{
    [SerializeField] private string monster;
    [SerializeField] private float weight;

    public string Monster => monster;
    public float Weight => weight;
}
