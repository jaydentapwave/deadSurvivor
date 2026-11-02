using UnityEngine;
namespace DeadSurvivor
{
    // 045: presentation-only settings for one map scene. Lives next to DSMapSession on purpose:
    // DSMapSession's field set is pinned by the full-snapshot contract (DSGame.MapSnapshot.cs).
    // Absent component = legacy behaviour (sprites, camera distance 150, camera-point occlusion rays).
    [DisallowMultipleComponent]
    public sealed class DSMapPresentation : MonoBehaviour
    {
        public DSMapActorSet Actors;
        [Min(5)] public float CameraDistance=30;
    }
}
