using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;

// 보관함(기획서 C절 V3): 가로 스크롤 목록. 빈 곳을 탭하면 보관함 빈 곳 대상. 항목 View가 모자라면 Presenter가 템플릿으로 늘린다
public class UI_StashView : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    // 항목 템플릿(목록 안 첫 항목, 장면에 하나)
    [SerializeField] private UI_StashItemView itemTemplate;
    [SerializeField] private RectTransform content;

    private readonly List<UI_StashItemView> _itemViewList = new();
    private readonly Subject<int> _pressed = new();
    private readonly Subject<BagTarget> _tapped = new();
    private readonly Subject<BagTarget?> _dragged = new();
    private readonly Subject<BagTarget?> _dropped = new();

    // 누름(항목 Uid, 빈 곳 −1)
    public Observable<int> Pressed => _pressed;
    public Observable<BagTarget> Tapped => _tapped;
    public Observable<BagTarget?> Dragged => _dragged;
    public Observable<BagTarget?> Dropped => _dropped;
    public UI_StashItemView ItemTemplate => itemTemplate;
    public RectTransform Content => content;
    public int ItemViewCount => _itemViewList.Count;
    public UnityEngine.UI.Graphic GetItemGraphic(int index) => _itemViewList[index].Background;

    // 템플릿을 첫 항목으로 쓴다(Presenter.Initialize가 한 번 부른다)
    public void Initialize() => AddItemView(itemTemplate);

    private void OnDestroy()
    {
        _pressed.Dispose();
        _tapped.Dispose();
        _dragged.Dispose();
        _dropped.Dispose();
    }

    // 빈 곳 누름(아이템 없음 = Uid −1)도 올려 Presenter가 이번 누름의 뜻을 새로 정하게 한다
    public void OnPointerDown(PointerEventData eventData) => _pressed.OnNext(-1);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!eventData.dragging)
        {
            _tapped.OnNext(BagTarget.StashEmpty());
        }
    }

    public void AddItemView(UI_StashItemView itemView)
    {
        itemView.Bind(this);
        _itemViewList.Add(itemView);
    }

    public void ReportPressed(int uid) => _pressed.OnNext(uid);

    public void ReportTapped(BagTarget target) => _tapped.OnNext(target);

    public void ReportDragged(BagTarget? target) => _dragged.OnNext(target);

    public void ReportDropped(BagTarget? target) => _dropped.OnNext(target);

    // 항목 View 수 ≥ itemList 수여야 한다(Presenter가 먼저 늘린다)
    public void UpdateUI(IReadOnlyList<StashItemDisplay> itemList)
    {
        for (var i = 0; i < _itemViewList.Count; i++)
        {
            var hasItem = i < itemList.Count;
            _itemViewList[i].SetVisible(hasItem);
            if (hasItem)
            {
                _itemViewList[i].UpdateUI(itemList[i]);
            }
        }
    }
}
