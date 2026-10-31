using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSGame
    {
        public int Formation, PendingFormation;
        public float RealBattle, RealPrep, RealPaused, WaveReal, WavePaused, LobbyTime, ResultTime;
        public bool TestControlsUsed, VerificationRun;
        public static readonly string[] FormationNames = { "균형형", "쐐기형", "횡대형" };
        public static Vector2 FormationOffset(int formation, int index, int count)
        {
            count = Mathf.Clamp(count, 1, 4);
            index = Mathf.Clamp(index, 0, count - 1);
            Vector2[] points = formation == 1 ? new[] { new Vector2(0, -48), new Vector2(-48, 0), new Vector2(48, 0), new Vector2(0, 48) } : formation == 2 ? new[] { new Vector2(-72, 0), new Vector2(-24, 0), new Vector2(24, 0), new Vector2(72, 0) } : new[] { new Vector2(-24, -24), new Vector2(24, -24), new Vector2(-24, 24), new Vector2(24, 24) };
            Vector2 center = Vector2.zero;
            for (int n = 0; n < count; n++)
                center += points[n];
            return points[index] - center / count;
        }
        public void SelectFormation(int value)
        {
            if (RejectMovementFixtureCommand("SelectFormation")) return;
            if (State != "prep" || value < 0 || value > 2)
                return;
            PendingFormation = value;
            UI.Prep();
        }
        void RecordTime(float dt)
        {
            if (State == "lobby")
            {
                LobbyTime += dt;
                return;
            }
            if (State == "result")
            {
                ResultTime += dt;
                return;
            }
            if (VerificationRun)
                return;
            if (State == "prep")
            {
                RealPrep += dt;
                if (History.Count > 0)
                    History[History.Count - 1].prepTime += dt;
            }
            else if (State == "battle")
            {
                if (Paused)
                {
                    RealPaused += dt;
                    WavePaused += dt;
                }
                else
                {
                    RealBattle += dt;
                    WaveReal += dt;
                }
            }
            TestControlsUsed |= Config.speed != Data.settings.speed || Config.duration != Data.settings.duration || Config.drops != Data.settings.drops || Config.countScale != Data.settings.countScale;
        }
    }
}
