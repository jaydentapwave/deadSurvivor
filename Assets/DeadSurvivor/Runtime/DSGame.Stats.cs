using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSGame
    {
        public CombatStats GearStats(Survivor c) => Equipment.Stats(c);
        public CombatStats Stats(Survivor c) => Equipment.Stats(c);
        public void RefreshProviders()
        {
            for (int n = 0; n < Team.Count; n++)
            {
                var c = Team[n];
                int provider = Equipment.Provider(Team, n);
                float period = provider < 0 ? 0 : 3 / (1 + Stats(Team[provider]).haste);
                if (c.shareProvider != provider || !Mathf.Approximately(c.sharePeriod, period))
                {
                    c.shareProvider = provider;
                    c.sharePeriod = period;
                    c.shareClock = 0;
                }
            }
        }
    }
}
