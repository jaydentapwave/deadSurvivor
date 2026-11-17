using TMPro;
using UnityEngine;

// F6의 풀·이동·수명은 유지. 높이9px 원본 숫자를 F6 fontSizePx 높이로 그린다.
public class DamageNumberView : MonoBehaviour
{
    [SerializeField] private TextMeshPro text;

    private SpriteRenderer[] _digits;
    private Color _color;

    public void Initialize(int value, Color color, float fontSizePx, Sprite[] sprites)
    {
        text.enabled = false;
        if (_digits == null)
        {
            // int 피해의 최대 10자리. 풀 인스턴스가 최초로 쓰일 때만 만들고 재사용한다.
            _digits = new SpriteRenderer[10];
            for (var i = 0; i < _digits.Length; i++)
            {
                var child = new GameObject($"Digit{i}");
                child.transform.SetParent(transform, false);
                _digits[i] = child.AddComponent<SpriteRenderer>();
                _digits[i].sortingOrder = 60;
            }
        }

        var digits = UiArtLayout.Digits(value);
        var scale = fontSizePx / 9f;
        var width = (digits.Length * 8 - 1) * scale;
        _color = color;
        for (var i = 0; i < _digits.Length; i++)
        {
            var renderer = _digits[i];
            renderer.enabled = i < digits.Length;
            if (!renderer.enabled)
            {
                continue;
            }

            renderer.sprite = sprites[digits[i] - '0'];
            renderer.color = color;
            renderer.transform.localScale = Vector3.one * scale;
            renderer.transform.localPosition = new Vector3((-width / 2 + (i * 8 + 3.5f) * scale) / Const.PixelsPerUnit, 0f, 0f);
        }
    }

    public void UpdateView(Vector2 worldPosition, float alpha)
    {
        transform.localPosition = worldPosition;
        _color.a = alpha;
        foreach (var digit in _digits)
        {
            digit.color = _color;
        }
    }
}
