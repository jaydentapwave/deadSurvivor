using UnityEngine;

// 몸의 움직이는 자식과 별개로 발 피벗에 고정. 풀 재사용에서는 같은 renderer를 다시 설정한다.
public class FootShadowView : MonoBehaviour
{
    private SpriteRenderer _renderer;

    public void Initialize(Sprite sprite, float bodyWidthPixels, int sortingLayerId, Material material)
    {
        if (_renderer == null)
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        _renderer.sprite = sprite;
        _renderer.sharedMaterial = material;
        _renderer.sortingLayerID = sortingLayerId;
        _renderer.sortingOrder = -10;
        var scale = SpriteMotion.GetShadowScale(bodyWidthPixels, sprite.rect.width);
        transform.localScale = new Vector3(scale, scale, 1f);
        // 아래 가운데 피벗을 발 중심에 맞춘다(정수 px, 24×12면 -6px).
        transform.localRotation = Quaternion.Euler(90,0,0);
        transform.localPosition = new Vector3(0f, .001f, 0f);
    }
}
