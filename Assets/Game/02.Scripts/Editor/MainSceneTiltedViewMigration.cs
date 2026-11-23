using UnityEditor;

// Existing entry point forwards to the only production camera migration.
public static class MainSceneTiltedViewMigration
{
    [MenuItem("DeadSurvivor/HD2D/Migrate Main View")]
    public static void Apply()
    {
        MainSceneDQScreenMigration.Apply();
    }
}
