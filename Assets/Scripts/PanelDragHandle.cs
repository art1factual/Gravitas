using UnityEngine;
using UnityEngine.EventSystems;

public class PanelDragHandle : MonoBehaviour
{

    public RectTransform panel;
    RectTransform parent;
    Vector2 startPointer;
    Vector2 startPosition;
    bool dragging;

    public void OnBeginDrag(PointerEventData data)
    {
        dragging = false;
        if (data.button != PointerEventData.InputButton.Left || panel == null)
        return;
        parent = panel.parent as RectTransform;
        if (parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out startPointer))
            return;
        startPosition = panel.anchoredPosition;
        dragging = true;
        panel.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData data)
    {
        if (!dragging || panel == null || parent == null)
        return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out Vector2 point))
        panel.anchoredPosition = startPosition + point - startPointer;
    }
}
