using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace DeadSurvivor
{
    public partial class DSUI
    {
        sealed class BattleItemView
        {
            public Survivor owner; public Gear item;
            public TextMeshProUGUI clock;
            public UnityEngine.UI.Image icon, bar, background;
        }
        readonly List<BattleItemView> battleItems = new();
        public int BattleItemCount => battleItems.Count;
        public static string BattleItemStatus(Survivor c, Gear item, bool timed)
            => c.hp <= 0 ? "비활성" : !timed ? "지속 효과" : item.activationFlash > 0 ? "발동!" : item.cd > 0 ? $"{item.cd:0.0}초" : "준비";
        void BuildBattleItems(RectTransform panel, Survivor c, float width)
        {
            if (c.items.Count == 0) { Text(panel, "장착 아이템 없음", 12, 70, width - 24, 24, 13, "#93a5a5"); return; }
            float cell = (width - 24) / 4;
            for (int i = 0; i < c.items.Count; i++)
            {
                var item = c.items[i];
                var tile = Box("BattleItem_" + item.id, panel, 12 + (i % 4) * cell, 70 + (i / 4) * 46, cell - 4, 42, "#243238");
                Image(tile, g.World.ItemIcon(item.type), 3, 4, 22, 22);
                var name = Text(tile, g.Items[item.type].name, 28, 2, cell - 34, 18, 11);
                name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
                var clock = Text(tile, "", 28, 19, cell - 34, 18, 11);
                var track = Box("CooldownTrack", tile, 3, 38, cell - 10, 3, "#10191d");
                var fill = Box("CooldownFill", track, 0, 0, cell - 10, 3, "#75c9e8").GetComponent<UnityEngine.UI.Image>();
                // Anchor-based width avoids texture allocations and leaves input to the movement surface.
                fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                battleItems.Add(new BattleItemView { owner = c, item = item, clock = clock, bar = fill,
                    icon = tile.Find("Artwork").GetComponent<UnityEngine.UI.Image>(), background = tile.GetComponent<UnityEngine.UI.Image>() });
            }
        }
        public void RefreshBattleItems()
        {
            foreach (var view in battleItems)
            {
                bool alive = view.owner.hp > 0, timed = DSWorld.HasItemCooldown(g.Items[view.item.type]);
                bool fired = alive && view.item.activationFlash > 0;
                view.clock.text = BattleItemStatus(view.owner, view.item, timed);
                view.icon.color = alive ? Color.white : new Color(.35f, .35f, .35f, 1);
                view.background.color = C(fired ? "#53623a" : "#243238");
                view.bar.gameObject.SetActive(alive && timed);
                float remaining = g.World.ItemCooldownRemaining(view.owner, view.item);
                view.bar.rectTransform.anchorMax = new Vector2(remaining > 0 ? remaining : 1, 1);
                view.bar.color = C(remaining > 0 ? "#75c9e8" : "#c7ed83");
            }
        }
    }
}
