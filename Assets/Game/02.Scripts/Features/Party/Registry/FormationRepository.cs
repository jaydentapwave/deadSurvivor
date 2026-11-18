using System.Collections.Generic;
using System.Linq;

public class FormationRepository
{
    private const string Folder = "Data/Formation";

    private readonly List<FormationData> _formationDataList;
    private readonly Dictionary<string, FormationData> _formationDict;

    // 진형 수 = 데이터 파일 수, order 순(F2 기획서 B절)
    public IReadOnlyList<FormationData> FormationDataList => _formationDataList;

    public FormationRepository(ResourceService resourceService)
    {
        _formationDataList = resourceService.LoadAllJson<FormationData>(Folder).OrderBy(data => data.Order).ToList();
        _formationDict = _formationDataList.ToDictionary(data => data.Id);
    }

    public bool TryGet(string id, out FormationData data) => _formationDict.TryGetValue(id, out data);
}
