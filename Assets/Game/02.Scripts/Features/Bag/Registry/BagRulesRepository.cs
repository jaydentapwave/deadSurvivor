// 가방 규칙 파일 하나를 읽어 둔다
public class BagRulesRepository
{
    private const string Path = "Data/Bag/bag_rules";

    public BagRulesData Rules { get; }

    public BagRulesRepository(ResourceService resourceService)
    {
        Rules = resourceService.LoadJson<BagRulesData>(Path);
    }
}
