using System;
using UnityEngine;

[Serializable]
public class ViewRulesData
{
    [SerializeField] private float tiltDegrees;
    [SerializeField] private float distance;
    [SerializeField] private float nearClip;
    [SerializeField] private float farClip;
    [SerializeField] private float targetFraction;
    [SerializeField] private int minScale;
    [SerializeField] private float facingHoldDegrees;
    public float TiltDegrees => tiltDegrees;
    public float SinTilt => Mathf.Sin(tiltDegrees * Mathf.Deg2Rad);
    public float Distance => distance;
    public float NearClip => nearClip;
    public float FarClip => farClip;
    public float TargetFraction => targetFraction;
    public int MinScale => minScale;
    public float FacingHoldDegrees => facingHoldDegrees;
}
