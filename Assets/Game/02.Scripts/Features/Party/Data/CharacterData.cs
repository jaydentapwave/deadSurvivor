using System;
using UnityEngine;

[Serializable]
public class CharacterData
{
    [SerializeField] private string id;
    [SerializeField] private int order;
    [SerializeField] private string name;
    [SerializeField] private string role;
    [SerializeField] private int hp;
    [SerializeField] private float atk;
    [SerializeField] private float def;
    [SerializeField] private string startItem;
    [SerializeField] private string color;
    [SerializeField] private string sprite;
    // 그림 없는 임시 도형에서는 생략할 수 있다.
    [SerializeField] private MemberMotionData motion;

    public string Id => id;
    public int Order => order;
    public string Name => name;
    public string Role => role;
    public int Hp => hp;
    public float Atk => atk;
    public float Def => def;
    public string StartItem => startItem;
    public string Color => color;
    // 그림 키. 비면 F1 임시 도형(아트 단계가 채운다)
    public string Sprite => sprite;
    public MemberMotionData Motion => motion;
}

// source.txt의 재생 메타데이터. 프레임 이름은 <sprite>_<walk|attack>_<front|back>_<00..>.
[Serializable]
public class MemberMotionData
{
    [SerializeField] private int fps;
    [SerializeField] private int walkFrames;
    [SerializeField] private int attackFrames;
    [SerializeField] private int hitFront;
    [SerializeField] private int hitBack;

    public int Fps => fps;
    public int WalkFrames => walkFrames;
    public int AttackFrames => attackFrames;
    public int HitFront => hitFront;
    public int HitBack => hitBack;

    // Optional per-direction source frame; -1 uses the adopted front/back timing.
    [SerializeField] private int hitDown = -1;
    [SerializeField] private int hitDownRight = -1;
    [SerializeField] private int hitRight = -1;
    [SerializeField] private int hitUpRight = -1;
    [SerializeField] private int hitUp = -1;

    public float HitSeconds(FacingDirection direction)
    {
        var frame = direction switch
        {
            FacingDirection.Down => hitDown,
            FacingDirection.DownRight => hitDownRight,
            FacingDirection.Right => hitRight,
            FacingDirection.UpRight => hitUpRight,
            _ => hitUp,
        };
        if (frame < 0)
        {
            frame = direction == FacingDirection.UpRight || direction == FacingDirection.Up ? hitBack : hitFront;
        }
        return frame / (float)fps;
    }
    public float AttackSeconds => attackFrames / (float)fps;
}
