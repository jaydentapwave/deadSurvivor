// 047-H1: Tripo animation check — samples each clip of an imported humanoid FBX every 1/30 s and reports, per 0.25 s,
// the max hand speed and the min hips height (finds the strike segment and crouch/prone parts before cropping).
// Run: unity command script run --project-path <root> -- --file Tools/Map/clip_scan_047.cs --entry ClipScan047.Run
using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;
public static class ClipScan047{
 public static object Run(){
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeadSurvivor/Art/Characters/Heroes/hero_base/Hero045v2.prefab");
  var clips=AssetDatabase.LoadAllAssetsAtPath("Assets/DeadSurvivor/Art/Characters/Hero047/hero_attack.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
  var h=(GameObject)Object.Instantiate(prefab);var an=h.GetComponentInChildren<Animator>();var lines=new List<string>();
  AnimationMode.StartAnimationMode();
  try{
   foreach(var clip in clips){
    var rows=new List<(float t,float rs,float ls,float hip,float rh,float lh)>();Vector3 pr=default,pl=default;
    for(float t=0;t<=clip.length;t+=1/30f){
     AnimationMode.SampleAnimationClip(an.gameObject,clip,t);
     var root=h.transform;var r=root.InverseTransformPoint(an.GetBoneTransform(HumanBodyBones.RightHand).position);var l=root.InverseTransformPoint(an.GetBoneTransform(HumanBodyBones.LeftHand).position);var hip=root.InverseTransformPoint(an.GetBoneTransform(HumanBodyBones.Hips).position).y;
     rows.Add((t,t>0?(r-pr).magnitude*30:0,t>0?(l-pl).magnitude*30:0,hip,r.y,l.y));pr=r;pl=l;}
    // 0.25 s buckets: max hand speed, min hip height
    var b=rows.GroupBy(x=>(int)(x.t/.25f)).Select(g=>$"{g.Key*.25f:F2}:{g.Max(x=>Mathf.Max(x.rs,x.ls)):F2}/{g.Min(x=>x.hip):F2}");
    lines.Add(clip.name+" len="+clip.length.ToString("F2")+" | "+string.Join(" ",b));
   }
  }finally{AnimationMode.StopAnimationMode();Object.DestroyImmediate(h);}
  return string.Join("\n",lines);
 }}
