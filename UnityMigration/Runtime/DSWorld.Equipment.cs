using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSWorld
    {
        public static bool HasItemCooldown(ItemDef d) => !string.IsNullOrEmpty(d.attackMode) || d.bonusEffect == "pulse";
        public float ItemCooldownRemaining(Survivor c, Gear item)
        {
            var d = g.Items[item.type];
            if (!HasItemCooldown(d)) return 0;
            float period = item.lastPeriod > 0 ? item.lastPeriod : g.Equipment.IsAction(item) ? d.interval / (1 + g.Equipment.Stats(c, item).haste) : d.effectInterval;
            return Mathf.Clamp01(item.cd / Mathf.Max(.001f, period));
        }
    }
}
