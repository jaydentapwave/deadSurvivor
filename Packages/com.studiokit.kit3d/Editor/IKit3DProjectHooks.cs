using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Kit3D
{
    /// <summary>One character of a line-up capture.</summary>
    public class Kit3DLineupItem
    {
        public GameObject prefab;
        public Material material;      // null: keep the prefab's own materials
        public float scale = 1f;       // world scale of the instance
        public float visualYaw;        // local Y rotation of the instance's first child ("Visual")
    }

    /// <summary>
    /// Project-specific editor work the kit must not know (game types such as an actor set). The project implements it
    /// in exactly one non-abstract class with a parameterless constructor (found with TypeCache). The request's "hooks"
    /// object carries the project's hook data (catalog project.hook_data); items carry their catalog "project" block.
    /// </summary>
    public interface IKit3DProjectHooks
    {
        /// <summary>Build, before anything is written: validate the project data and throw to stop.</summary>
        void Prepare(JObject request);

        /// <summary>Build, after textures, materials and prefabs exist: connect them to project data, save, re-read and
        /// verify (throw on failure). Returns a small summary for the result file.</summary>
        object Register(JObject request, IReadOnlyDictionary<string, Material> materials, IReadOnlyDictionary<string, GameObject> prefabs);

        /// <summary>Check: project measurements for the report; add judged problems to the list.</summary>
        object Measure(JObject request, List<string> problems);

        /// <summary>Capture: what to line up, in order.</summary>
        IEnumerable<Kit3DLineupItem> Lineup(JObject request);
    }
}
