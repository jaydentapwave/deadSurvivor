using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using Kit3D;

namespace DeadSurvivor.Editor
{
    // 051: Deadsurvivor's part of the 3dkit asset pipeline (3dkit/unity, docs/workflows/asset-standard.md). The kit knows no
    // game types; this connects built assets to the build scene's actor set (Art/Characters/ActorSet_Main.asset).
    // Moved from Tools/Art/AssetBuild.cs (050): actor-set entries + save/re-read check, actor measurements, line-up list.
    // Request data: hooks.actor_set (catalog project.hook_data), items[].project.actor = {list: Heroes|Monsters, id}.
    public class DSKit3DProjectHooks : IKit3DProjectHooks
    {
        static string SetPath(JObject req) => (string)req["hooks"]["actor_set"];

        static DSMapActorSet LoadSet(JObject req) =>
            AssetDatabase.LoadAssetAtPath<DSMapActorSet>(SetPath(req)) ?? throw new Exception("NO_ACTOR_SET:" + SetPath(req));

        static IEnumerable<(string id, string list, string actorId)> Actors(JObject req)
        {
            foreach (var it in req["items"])
            {
                var a = it["project"]?["actor"];
                if (a == null || a.Type == JTokenType.Null) continue;
                yield return ((string)it["id"], (string)a["list"], (string)a["id"]);
            }
        }

        static DSMapActorSet.Entry[] List(DSMapActorSet set, string list) =>
            list == "Heroes" ? set.Heroes : list == "Monsters" ? set.Monsters : throw new Exception("UNKNOWN_ACTOR_LIST:" + list);

        public void Prepare(JObject req)
        {
            var set = LoadSet(req);
            foreach (var (_, list, aid) in Actors(req))
                if (!List(set, list).Any(e => e.id == aid)) throw new Exception("NO_ACTOR_ENTRY:" + list + "/" + aid);
        }

        // heroes keep the shared base prefab and get their palette material; monsters get the new prefab (its own material)
        // and yawOffset 0 because the front is baked into the model
        public object Register(JObject req, IReadOnlyDictionary<string, Material> mats, IReadOnlyDictionary<string, GameObject> prefabs)
        {
            var path = SetPath(req);
            var set = LoadSet(req);
            var so = new SerializedObject(set);
            foreach (var (id, list, aid) in Actors(req))
            {
                var arr = so.FindProperty(list);
                int found = -1;
                for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == aid) found = i;
                if (found < 0) throw new Exception("NO_ACTOR_ENTRY:" + list + "/" + aid);
                var e = arr.GetArrayElementAtIndex(found);
                if (list == "Heroes") e.FindPropertyRelative("material").objectReferenceValue = mats[id];
                else
                {
                    e.FindPropertyRelative("prefab").objectReferenceValue = prefabs[id];
                    e.FindPropertyRelative("material").objectReferenceValue = null;
                    e.FindPropertyRelative("yawOffset").floatValue = 0;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            // verify what was saved (046-12: re-read, do not trust the in-memory object)
            Resources.UnloadAsset(set);
            var check = AssetDatabase.LoadAssetAtPath<DSMapActorSet>(path);
            var bad = new List<string>();
            foreach (var (id, list, aid) in Actors(req))
            {
                var e = List(check, list).First(x => x.id == aid);
                if (list == "Heroes" ? e.material != mats[id] : (e.prefab != prefabs[id] || e.material || e.yawOffset != 0)) bad.Add(list + "/" + aid);
            }
            if (bad.Count > 0) throw new Exception("ACTOR_SET_NOT_SAVED:" + string.Join(",", bad));
            return new { actorSet = path, entries = Actors(req).Count() };
        }

        public object Measure(JObject req, List<string> problems)
        {
            var set = AssetDatabase.LoadAssetAtPath<DSMapActorSet>(SetPath(req));
            if (!set) { problems.Add("actor set missing: " + SetPath(req)); return null; }
            foreach (var e in set.Monsters)
                if (e.yawOffset != 0) problems.Add($"actor set monster {e.id}: yawOffset {e.yawOffset} (front must be baked, +Z)");
            object Row(DSMapActorSet.Entry e) => new { e.id, prefab = AssetDatabase.GetAssetPath(e.prefab), material = e.material ? AssetDatabase.GetAssetPath(e.material) : null, e.yawOffset };
            return new
            {
                heroes = set.Heroes.Select(Row).ToArray(), monsters = set.Monsters.Select(Row).ToArray(),
                hit = set.HitMaterial ? AssetDatabase.GetAssetPath(set.HitMaterial) : null,
                blob = set.MonsterBlobShadow ? AssetDatabase.GetAssetPath(set.MonsterBlobShadow) : null
            };
        }

        // note: rigged heroes stay in the edit-mode bind pose (faces sideways, S3-21); judge heroes from a Play capture
        public IEnumerable<Kit3DLineupItem> Lineup(JObject req)
        {
            var set = LoadSet(req);
            foreach (var e in set.Heroes) yield return new Kit3DLineupItem { prefab = e.prefab, material = e.material, scale = set.HeroHeight * 2f, visualYaw = e.yawOffset };
            foreach (var e in set.Monsters) yield return new Kit3DLineupItem { prefab = e.prefab, material = e.material, scale = set.MonsterHeight * 2f, visualYaw = e.yawOffset };
        }
    }
}
