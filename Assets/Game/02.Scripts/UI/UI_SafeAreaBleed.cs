using UnityEngine;

// Decorative child only: interactive content stays under the safe area parent.
public class UI_SafeAreaBleed : MonoBehaviour
{
    [SerializeField] private bool left;
    [SerializeField] private bool bottom;
    [SerializeField] private bool right;
    [SerializeField] private bool top;
    private RectTransform _rect;
    private Canvas _canvas;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>().rootCanvas;
    }

    private void LateUpdate()
    {
        var inset = SafeAreaLayout.Bleed(new Vector2(Screen.width, Screen.height),
            UI_SafeAreaPanel.CurrentSafeArea, _canvas.scaleFactor, left, bottom, right, top);
        _rect.offsetMin = new Vector2(-inset.x, -inset.y);
        _rect.offsetMax = new Vector2(inset.z, inset.w);
    }
}
