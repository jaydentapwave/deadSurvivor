using UnityEngine;
using UnityEngine.EventSystems;
namespace DeadSurvivor
{
    public class DSPointer : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public DSUI ui; public int item = -1, character = -1, x, y; public bool stash, joystick; bool dragging;
        public void OnPointerClick(PointerEventData e)
        {
            if (dragging)
            {
                dragging = false;
                return;
            }
            if (!joystick)
                ui.ClickTarget(this);
        }
        public void OnBeginDrag(PointerEventData e)
        {
            dragging = true;
            if (!joystick && item >= 0)
                ui.BeginItem(item, e.position);
        }
        public void OnDrag(PointerEventData e)
        {
            if (joystick)
                ui.MoveJoystick(e.position);
            else
                ui.DragItem(e.position);
        }
        public void OnEndDrag(PointerEventData e)
        {
            if (!joystick)
                ui.EndItem(e.position);
        }
        public void OnPointerEnter(PointerEventData e)
        {
            if (!joystick)
                ui.HoverTarget(this);
        }
        public void OnPointerExit(PointerEventData e)
        {
            if (ui.Held < 0)
                ui.HideTooltip();
        }
        public void OnPointerDown(PointerEventData e)
        {
            dragging = false;
            if (joystick)
                ui.StartJoystick(e.position);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (joystick)
                ui.StopJoystick();
        }
    }
}
