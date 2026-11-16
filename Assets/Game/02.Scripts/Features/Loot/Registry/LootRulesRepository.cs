// 전리품 규칙 파일 하나를 읽어 둔다
public class LootRulesRepository
{
    private const string Path = "Data/Loot/loot_rules";

    public LootRulesData Rules { get; }

    public LootRulesRepository(ResourceService resourceService)
    {
        Rules = resourceService.LoadJson<LootRulesData>(Path);
    }
}
