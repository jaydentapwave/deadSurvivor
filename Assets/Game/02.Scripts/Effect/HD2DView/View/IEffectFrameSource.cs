using UnityEngine;

public interface IEffectFrameSource
{
    Renderer ActiveRenderer { get; }
    Sprite SourceSprite { get; }
    int FrameNumber { get; }
    float Alpha { get; }
    bool IsShown { get; }
}
