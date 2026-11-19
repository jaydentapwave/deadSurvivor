using System.Collections.Generic;
using R3;
using UnityEngine;

// 대원 하나의 4×4 가방(기획서 C절 V3). 칸은 줄 순서(index = y × 폭 + x). 칸의 입력 사실을 모아 Presenter에 올린다
public class UI_BagView : MonoBehaviour
{
    [SerializeField] private UI_BagCellView[] cellViews;

    private readonly Subject<int> _pressed = new();
    private readonly Subject<BagTarget> _tapped = new();
    private readonly Subject<BagTarget?> _dragged = new();
    private readonly Subject<BagTarget?> _dropped = new();

    // 칸을 누름(아이템 Uid, 빈 칸 −1)
    public Observable<int> Pressed => _pressed;
    // 드래그 없이 뗌(그 칸)
    public Observable<BagTarget> Tapped => _tapped;
    // 드래그 중 손가락 밑 대상(없으면 null)
    public Observable<BagTarget?> Dragged => _dragged;
    // 드래그를 끝내고 뗀 곳의 대상(없으면 null)
    public Observable<BagTarget?> Dropped => _dropped;
    public int CellCount => cellViews.Length;
    public UnityEngine.UI.Graphic GetCellGraphic(int index) => cellViews[index].Background;

    public void Initialize(int member, int width)
    {
        for (var i = 0; i < cellViews.Length; i++)
        {
            cellViews[i].Bind(this, member, i % width, i / width);
        }
    }

    private void OnDestroy()
    {
        _pressed.Dispose();
        _tapped.Dispose();
        _dragged.Dispose();
        _dropped.Dispose();
    }

    public void ReportPressed(int uid) => _pressed.OnNext(uid);

    public void ReportTapped(BagTarget target) => _tapped.OnNext(target);

    public void ReportDragged(BagTarget? target) => _dragged.OnNext(target);

    public void ReportDropped(BagTarget? target) => _dropped.OnNext(target);

    // cellList는 칸 순서. isDown = 쓰러진 대원(어둡게)
    public void UpdateUI(IReadOnlyList<BagCellDisplay> cellList, bool isDown)
    {
        for (var i = 0; i < cellViews.Length && i < cellList.Count; i++)
        {
            cellViews[i].UpdateUI(cellList[i], isDown);
        }
    }
}
