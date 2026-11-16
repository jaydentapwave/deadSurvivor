// 피드백 규칙 파일 하나를 읽어 둔다(CombatRulesRepository와 같은 모양)
public class FeedbackRulesRepository
{
    private const string Path = "Data/Feedback/feedback_rules";

    public FeedbackRulesData Rules { get; }

    public FeedbackRulesRepository(ResourceService resourceService)
    {
        Rules = resourceService.LoadJson<FeedbackRulesData>(Path);
    }
}
