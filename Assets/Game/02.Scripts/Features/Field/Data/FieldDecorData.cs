using System;
using System.Collections.Generic;
using UnityEngine;

// 장식 배치는 Tilemap 칸 좌표. 가장자리 띠 폭도 데이터로 두며, 꽃·풀만 바닥 겹이다.
// pivotOffsetXPixels: 실제 발 접점에서 임포트 피벗(아래 가운데)까지의 X 오프셋. 울타리는 +8px.
[Serializable]
public class FieldDecorData
{
    [SerializeField] private int edgeBandCells;
    [SerializeField] private FieldDecorPlacement[] placements;

    public int EdgeBandCells => edgeBandCells;
    public IReadOnlyList<FieldDecorPlacement> Placements => placements;
}

[Serializable]
public struct FieldDecorPlacement
{
    [SerializeField] private string sprite;
    [SerializeField] private int x;
    [SerializeField] private int y;
    [SerializeField] private bool isGround;
    [SerializeField] private bool isFlipped;
    [SerializeField] private int pivotOffsetXPixels;

    public string Sprite => sprite;
    public Vector3Int Cell => new(x, y, 0);
    public bool IsGround => isGround;
    public bool IsFlipped => isFlipped;
    public int PivotOffsetXPixels => pivotOffsetXPixels;
}
