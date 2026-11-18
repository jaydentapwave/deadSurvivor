// 전투 규칙 파일 하나를 읽어 둔다
public class CombatRulesRepository
{
    private const string Path = "Data/Combat/combat_rules";

    public CombatRulesData Rules { get; }

    public CombatRulesRepository(ResourceService resourceService)
    {
        Rules = resourceService.LoadJson<CombatRulesData>(Path);
    }
}
