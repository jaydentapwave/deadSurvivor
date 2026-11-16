using System;
using UnityEngine;

// 등급 하나(기획서 데이터 스키마 Grade/grades.json). 배열 순서 = 등급 번호
[Serializable]
public class GradeData
{
    [SerializeField] private string id;
    [SerializeField] private string name;
    [SerializeField] private string color;
    [SerializeField] private float mult;

    public string Id => id;
    public string Name => name;
    // #rrggbb
    public string Color => color;
    // 배치 정보 등급 배수(F3 B2)
    public float Mult => mult;
}

// 등급 파일 형
[Serializable]
public class GradeTableData
{
    [SerializeField] private GradeData[] grades;

    public GradeData[] Grades => grades;
}
