using System.Collections.Generic;
using System.Linq;

// 몬스터 종류(Data/Monster)와 생성 규칙(Data/Spawn/spawn_rules)
public class MonsterRepository
{
    private const string Folder = "Data/Monster";
    private const string SpawnRulesPath = "Data/Spawn/spawn_rules";

    private readonly List<MonsterData> _monsterDataList;
    private readonly Dictionary<string, MonsterData> _monsterDict;

    public IReadOnlyList<MonsterData> MonsterDataList => _monsterDataList;
    public SpawnRulesData SpawnRules { get; }

    public MonsterRepository(ResourceService resourceService)
    {
        _monsterDataList = resourceService.LoadAllJson<MonsterData>(Folder).OrderBy(data => data.Id).ToList();
        _monsterDict = _monsterDataList.ToDictionary(data => data.Id);
        SpawnRules = resourceService.LoadJson<SpawnRulesData>(SpawnRulesPath);
    }

    public MonsterData Get(string id)
    {
        if (!_monsterDict.TryGetValue(id, out var data))
        {
            GameLogger.LogError($"[MonsterRepository] No monster {id}");
        }

        return data;
    }
}
