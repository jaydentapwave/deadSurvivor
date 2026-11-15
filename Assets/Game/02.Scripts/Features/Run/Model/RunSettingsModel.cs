// 다음 판의 시작 입력(기획서 C절 RunSettings). 로비 출발이 지금 값을 넘긴다.
public class RunSettingsModel
{
    // 0 = 판 시작 시각에서 뽑는다
    public int Seed { get; private set; }
    public float EnemyCountScale { get; private set; }

    public RunSettingsModel(RunRulesData runRules)
    {
        Seed = runRules.DefaultSeed;
        EnemyCountScale = runRules.DefaultEnemyCountScale;
    }

    public void Set(int seed, float enemyCountScale)
    {
        Seed = seed;
        EnemyCountScale = enemyCountScale;
    }
}
