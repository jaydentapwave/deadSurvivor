using System.Collections.Generic;

// 등급 파일 하나를 읽어 둔다
public class GradeRepository
{
    private const string Path = "Data/Grade/grades";

    private readonly List<GradeData> _gradeDataList;

    public IReadOnlyList<GradeData> GradeDataList => _gradeDataList;
    // 마지막(가장 높은) 등급 번호. 이 등급은 합성되지 않는다(A4)
    public int LastGrade => _gradeDataList.Count - 1;

    public GradeRepository(ResourceService resourceService)
    {
        _gradeDataList = new List<GradeData>(resourceService.LoadJson<GradeTableData>(Path).Grades);
    }

    public GradeData Get(int grade)
    {
        if (grade < 0 || grade >= _gradeDataList.Count)
        {
            GameLogger.LogError($"[GradeRepository] Grade {grade} out of 0..{LastGrade}");
            return _gradeDataList[0];
        }

        return _gradeDataList[grade];
    }
}
