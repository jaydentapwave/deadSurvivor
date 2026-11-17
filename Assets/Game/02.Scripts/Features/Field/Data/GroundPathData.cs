using System;
using System.Collections.Generic;
using UnityEngine;

// 흙길이 지나는 바닥 칸 목록(아트 기준서 3.2 흙길 대각선). 칸 = 아이소 Tilemap 칸 좌표(+x ↗, +y ↖).
// 길은 갈림 없이 한 줄로 이어져야 한다(이웃 1~2칸, 고르는 표가 갈림을 다루지 않음).
[Serializable]
public class GroundPathData
{
    [SerializeField] private GroundCell[] cells;

    public IReadOnlyList<GroundCell> Cells => cells;
}

[Serializable]
public struct GroundCell
{
    [SerializeField] private int x;
    [SerializeField] private int y;

    public int X => x;
    public int Y => y;
}
