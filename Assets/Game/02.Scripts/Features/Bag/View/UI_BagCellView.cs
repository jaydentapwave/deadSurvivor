using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 가방 칸 하나(기획서 C절 V3). 그리기와 입력 사실 보고만 한다: 누름(칸의 Uid, 빈 칸 −1), 드래그 없이 떼면 탭, 드래그 중·드래그 놓기는 손가락 밑 대상
public class UI_BagCellView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image border;
    [SerializeField] private Text labelText;
    [SerializeField] private Color emptyColor = new(0.55f, 0.45f, 0.32f, 0.6f);
    [SerializeField] private Color itemColor = new(0.85f, 0.76f, 0.58f, 1f);
    [SerializeField] private Color pickedColor = new(1f, 0.93f, 0.6f, 1f);
    [SerializeField] private Color validColor = new(0.35f, 0.8f, 0.35f, 1f);
    [SerializeField] private Color invalidColor = new(0.85f, 0.3f, 0.25f, 1f);
    [SerializeField] private Color downTint = new(0.45f, 0.45f, 0.45f, 1f);

    private UI_BagView _owner;
    private int _uid = -1;

    public BagTarget Target { get; private set; }

    public void Bind(UI_BagView owner, int member, int x, int y)
    {
        _owner = owner;
        Target = BagTarget.Cell(member, x, y);
    }

    public void UpdateUI(BagCellDisplay display, bool isDown)
    {
        _uid = display.Uid;
        var hasItem = display.Uid >= 0;
        var color = display.Mark == BagCellMark.Valid ? validColor
            : display.Mark == BagCellMark.Invalid ? invalidColor
            : display.IsPicked ? pickedColor
            : hasItem ? itemColor : emptyColor;
        background.color = isDown ? color * downTint : color;
        border.gameObject.SetActive(hasItem);
        border.color = isDown ? display.GradeColor * downTint : display.GradeColor;
        labelText.text = display.Label;
    }

    // 빈 칸이면 −1(Presenter가 이번 누름의 뜻을 새로 정한다)
    public void OnPointerDown(PointerEventData eventData) => _owner.ReportPressed(_uid);

    public void OnDrag(PointerEventData eventData) => _owner.ReportDragged(BagPointerUtils.FindTarget(eventData));

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.dragging)
        {
            _owner.ReportDropped(BagPointerUtils.FindTarget(eventData));
            return;
        }

        _owner.ReportTapped(Target);
    }
}

public enum BagCellMark
{
    None,
    // 미리보기 가능(초록)
    Valid,
    // 미리보기 불가(빨강)
    Invalid,
}

// 칸 하나의 표시 값. Uid −1 = 빈 칸. Label은 아이템의 첫 칸에만(아이콘이 없으면 이름 2자)
public struct BagCellDisplay
{
    public int Uid;
    public string Label;
    public Color GradeColor;
    public BagCellMark Mark;
    public bool IsPicked;
}
