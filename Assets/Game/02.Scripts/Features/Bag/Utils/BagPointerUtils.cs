using UnityEngine.EventSystems;

// 정비 화면 입력의 대상 찾기(기획서 C절): 이벤트 레이캐스트가 맞힌 오브젝트에서 가방 칸 → 보관함 항목 → 보관함 빈 곳 순으로 찾는다
public static class BagPointerUtils
{
    // 손가락 밑 대상. 없으면 null(드래그를 대상 없는 곳에 놓음 = 취소)
    public static BagTarget? FindTarget(PointerEventData eventData)
    {
        var hit = eventData.pointerCurrentRaycast.gameObject;
        if (hit == null)
        {
            return null;
        }

        var cellView = hit.GetComponentInParent<UI_BagCellView>();
        if (cellView != null)
        {
            return cellView.Target;
        }

        var stashItemView = hit.GetComponentInParent<UI_StashItemView>();
        if (stashItemView != null && stashItemView.Uid >= 0)
        {
            return BagTarget.StashItem(stashItemView.Uid);
        }

        return hit.GetComponentInParent<UI_StashView>() != null ? BagTarget.StashEmpty() : null;
    }
}
