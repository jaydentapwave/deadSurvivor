using UnityEngine;
using System.Linq;
namespace DeadSurvivor
{
    public partial class DSUI
    {
        void FormationControls()
        {
            float left = W / 2 - 250;
            for (int n = 0; n < 3; n++)
            {
                int index = n;
                Button(page, DSGame.FormationNames[n], left + n * 170, H - 171, 160, 34, () => g.SelectFormation(index), g.PendingFormation == n);
            }
            var preview = Box("FormationPreview", page, W / 2 - 250, H - 330, 500, 150);
            for (int n = 0; n < g.Team.Count; n++)
            {
                var c = g.Team[n];
                Vector2 point = DSGame.FormationOffset(g.PendingFormation, n, g.Team.Count);
                var actions = g.Equipment.Actions(c).ToArray();
                for (int a = 0; a < Mathf.Min(3, actions.Length); a++)
                    Image(preview, g.World.ItemIcon(actions[a].type), point.x * 2.5f + 194 + a * 9, point.y * .9f + 54, 18, 24);
                Text(preview, g.Def(c).name + "\n발동 " + actions.Length + "개", point.x * 2.5f + 226, point.y * .9f + 50, 80, 40, 14, c.hp > 0 ? "#c7ed83" : "#888888");
            }
        }
    }
}
