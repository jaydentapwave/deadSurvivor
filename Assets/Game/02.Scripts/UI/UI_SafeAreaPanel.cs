using UnityEngine;

// Canvas 루트 아래 안전 영역 판넬(기획서 I절). 가장자리 UI는 이 판넬에 앵커한다. 화면 크기·safeArea가 바뀌면 다시 맞춘다.
public class UI_SafeAreaPanel : MonoBehaviour
{
    [SerializeField] private RectTransform panel;

    private Rect _appliedSafeArea;
    private Vector2Int _appliedScreenSize;

    private void Awake() => Apply();

    private void Update()
    {
        if (Screen.safeArea != _appliedSafeArea || Screen.width != _appliedScreenSize.x || Screen.height != _appliedScreenSize.y)
        {
            Apply();
        }
    }

    private void Apply()
    {
        _appliedSafeArea = Screen.safeArea;
        _appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
        var (anchorMin, anchorMax) = SafeAreaLayout.Calculate(_appliedScreenSize, _appliedSafeArea);
        panel.anchorMin = anchorMin;
        panel.anchorMax = anchorMax;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
    }
}
