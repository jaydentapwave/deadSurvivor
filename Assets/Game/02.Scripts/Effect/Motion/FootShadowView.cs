using UnityEngine;

// 몸의 움직이는 자식과 별개로 발 피벗에 고정. 풀 재사용에서는 같은 renderer를 다시 설정한다.
public class FootShadowView : MonoBehaviour
{
    private SpriteRenderer _renderer;

    public void Initialize(Sprite sprite, float bodyWidthPixels, int sortingLayerId)
    {
        if (_renderer == null)
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        _renderer.sprite = sprite;
        _renderer.sortingLayerID = sortingLayerId;
        _renderer.sortingOrder = -10;
        var scale = SpriteMotion.GetShadowScale(bodyWidthPixels, sprite.rect.width);
        transform.localScale = new Vector3(scale, scale, 1f);
        // 아래 가운데 피벗을 발 중심에 맞춘다(정수 px, 24×12면 -6px).
        transform.localPosition = new Vector3(0f, -sprite.rect.height * scale * 0.5f / sprite.pixelsPerUnit, 0f);
    }
}
