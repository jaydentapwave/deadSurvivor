using UnityEngine;

public sealed class PixelWalkPreview : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 8f;

    private void Update()
    {
        bodyRenderer.sprite = frames[(int)(Time.time * framesPerSecond) % frames.Length];
    }
}
