using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 전투 화면 조이스틱(V2, F2 기획서 D절). 전투 판넬 전체를 덮는 투명 영역에 붙는다(HUD보다 뒤라 HUD 버튼 위 누름은 오지 않는다).
// 처음 누른 포인터 하나의 누른 자리·지금 자리(화면 px)만 알리고, 고리·손잡이는 UpdateUI로 그린다. 입력 계산은 MoveInputPresenter가 한다.
public class UI_JoystickView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const int NoPointer = int.MinValue;

    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform areaRect;
    [SerializeField] private Image ringImage;
    [SerializeField] private Image handleImage;
    // 손잡이가 누른 자리에서 움직이는 최대 거리(캔버스 단위)
    [SerializeField] private float handleTravel = 14f;

    private int _pointerId = NoPointer;

    public bool IsPressed => _pointerId != NoPointer;
    public Vector2 PressPosition { get; private set; }
    public Vector2 CurrentPosition { get; private set; }
    public float CanvasScale => canvas.scaleFactor;
    public bool IsShown => ringImage.gameObject.activeSelf;

    public void Initialize(Sprite ringSprite, Sprite handleSprite)
    {
        ringImage.sprite = ringSprite;
        handleImage.sprite = handleSprite;
    }

    // 판넬이 꺼지면 손을 뗀 것으로 본다(꺼진 동안 PointerUp이 오지 않는다)
    private void OnDisable() => _pointerId = NoPointer;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsPressed)
        {
            return;
        }

        _pointerId = eventData.pointerId;
        PressPosition = eventData.position;
        CurrentPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId)
        {
            return;
        }

        CurrentPosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId)
        {
            return;
        }

        _pointerId = NoPointer;
    }

    // pressPosition: 화면 px, input: 화면 기준 입력(길이 ≤ 1). 고리는 누른 자리, 손잡이는 input × handleTravel만큼 옮긴 자리
    public void UpdateUI(bool isShown, Vector2 pressPosition, Vector2 input)
    {
        ringImage.gameObject.SetActive(isShown);
        handleImage.gameObject.SetActive(isShown);
        if (!isShown)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(areaRect, pressPosition, null, out var localPress);
        ringImage.rectTransform.anchoredPosition = localPress;
        handleImage.rectTransform.anchoredPosition = localPress + input * handleTravel;
    }
}
