using System;
using UnityEngine.EventSystems;

public static class PointerExtension
{
    public static int GetButtonAsInt(this PointerEventData eventData)
    {
        switch (eventData.button)
        {
            case PointerEventData.InputButton.Left:
                return 1;
            case PointerEventData.InputButton.Right:
                return -1;
            case PointerEventData.InputButton.Middle:
                return 0;
            default:
                throw new ArgumentOutOfRangeException(nameof(eventData.button), eventData, $"Pointer button click {eventData.button} unhandled.");
        }
    } 
}