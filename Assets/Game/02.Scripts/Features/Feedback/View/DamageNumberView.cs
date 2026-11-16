using TMPro;
using UnityEngine;

// 몬스터 피해 숫자(월드 TextMeshPro, 정렬 60, F6 기획서 B절). 풀에서 꺼낸다. 몬스터를 따라가지 않는다.
public class DamageNumberView : MonoBehaviour
{
    [SerializeField] private TextMeshPro text;

    private Color _color;

    // 프리팹의 글꼴 재질(팩토리가 한 번 복사해 테두리 색을 넣는다)
    public Material FontMaterial => text.fontSharedMaterial;

    public void Initialize(int value, Color color, float fontSize, Material material)
    {
        text.fontSharedMaterial = material;
        text.fontSize = fontSize;
        text.SetText("{0}", value);
        _color = color;
        text.color = color;
    }

    public void UpdateView(Vector2 worldPosition, float alpha)
    {
        transform.localPosition = worldPosition;
        _color.a = alpha;
        text.color = _color;
    }
}
