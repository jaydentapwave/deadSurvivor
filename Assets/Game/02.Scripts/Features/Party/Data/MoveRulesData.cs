using System;
using UnityEngine;

// 이동 규칙(F2 기획서 A·D절, 데이터 스키마)
[Serializable]
public class MoveRulesData
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float followRate;
    [SerializeField] private float joystickRadius;

    // 바닥 단위/초
    public float MoveSpeed => moveSpeed;
    // 1/초. 한 걸음 따라가기 비율 = min(1, followRate × 걸음 시간)
    public float FollowRate => followRate;
    // 캔버스 단위(기준 640×360)
    public float JoystickRadius => joystickRadius;
}
